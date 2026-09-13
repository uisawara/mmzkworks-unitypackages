using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muProperty
{
    /// <summary>
    /// Nested key-value tree. Paths use <c>/</c> as the separator (for example <c>render/quality</c>).
    /// </summary>
    public sealed class PropertyTree
    {
        readonly Dictionary<string, Node> _children = new Dictionary<string, Node>();

        public int Count => _children.Count;

        public IEnumerable<string> Keys => _children.Keys;

        public void Set(string path, bool value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, int value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, float value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, Vector2 value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, Vector3 value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, Vector4 value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, Color value)
        {
            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Set(path, PropertyValue.From(value));
        }

        public void Set(string path, PropertyValue value)
        {
            var parts = SplitPath(path, requireNonEmpty: true);
            var current = this;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                current = current.EnsureObjectChild(parts[i]);
            }

            current._children[parts[parts.Length - 1]] = Node.FromLeaf(value);
        }

        public void Set(string path, PropertyTree subtree)
        {
            if (subtree == null)
            {
                throw new ArgumentNullException(nameof(subtree));
            }

            var parts = SplitPath(path, requireNonEmpty: true);
            var current = this;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                current = current.EnsureObjectChild(parts[i]);
            }

            current._children[parts[parts.Length - 1]] = Node.FromTree(subtree.Clone());
        }

        public bool Has(string path)
        {
            var parts = SplitPath(path, requireNonEmpty: false);
            if (parts.Length == 0)
            {
                return true;
            }

            return TryWalk(parts, out _);
        }

        public bool TryGet(string path, out PropertyValue value)
        {
            var parts = SplitPath(path, requireNonEmpty: true);
            if (!TryWalk(parts, out var node) || node.IsObject)
            {
                value = default;
                return false;
            }

            value = node.Value;
            return true;
        }

        public bool TryGetObject(string path, out PropertyTree tree)
        {
            var parts = SplitPath(path, requireNonEmpty: false);
            if (parts.Length == 0)
            {
                tree = this;
                return true;
            }

            if (!TryWalk(parts, out var node) || !node.IsObject)
            {
                tree = null;
                return false;
            }

            tree = node.Object;
            return true;
        }

        public bool GetBool(string path, bool defaultValue = false)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Bool
                ? value.AsBool()
                : defaultValue;
        }

        public int GetInt(string path, int defaultValue = 0)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Int
                ? value.AsInt()
                : defaultValue;
        }

        public float GetFloat(string path, float defaultValue = 0f)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Float
                ? value.AsFloat()
                : defaultValue;
        }

        public Vector2 GetVector2(string path, Vector2 defaultValue = default)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Vector2
                ? value.AsVector2()
                : defaultValue;
        }

        public Vector3 GetVector3(string path, Vector3 defaultValue = default)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Vector3
                ? value.AsVector3()
                : defaultValue;
        }

        public Vector4 GetVector4(string path, Vector4 defaultValue = default)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Vector4
                ? value.AsVector4()
                : defaultValue;
        }

        public Color GetColor(string path, Color defaultValue = default)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.Color
                ? value.AsColor()
                : defaultValue;
        }

        public string GetString(string path, string defaultValue = null)
        {
            return TryGet(path, out var value) && value.Kind == PropertyValueKind.String
                ? value.AsString()
                : defaultValue;
        }

        public PropertyTree Clone()
        {
            var copy = new PropertyTree();
            foreach (var pair in _children)
            {
                copy._children[pair.Key] = pair.Value.Clone();
            }

            return copy;
        }

        public bool DeepEquals(PropertyTree other)
        {
            if (other == null || _children.Count != other._children.Count)
            {
                return false;
            }

            foreach (var pair in _children)
            {
                if (!other._children.TryGetValue(pair.Key, out var node))
                {
                    return false;
                }

                if (pair.Value.IsObject != node.IsObject)
                {
                    return false;
                }

                if (pair.Value.IsObject)
                {
                    if (!pair.Value.Object.DeepEquals(node.Object))
                    {
                        return false;
                    }
                }
                else if (!pair.Value.Value.Equals(node.Value))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Overlay(PropertyTree source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            foreach (var pair in source._children)
            {
                if (pair.Value.IsObject
                    && _children.TryGetValue(pair.Key, out var existing)
                    && existing.IsObject)
                {
                    existing.Object.Overlay(pair.Value.Object);
                }
                else
                {
                    _children[pair.Key] = pair.Value.Clone();
                }
            }
        }

        PropertyTree EnsureObjectChild(string key)
        {
            if (_children.TryGetValue(key, out var node) && node.IsObject)
            {
                return node.Object;
            }

            var child = new PropertyTree();
            _children[key] = Node.FromTree(child);
            return child;
        }

        bool TryWalk(string[] parts, out Node node)
        {
            var current = this;
            node = null;
            for (var i = 0; i < parts.Length; i++)
            {
                if (!current._children.TryGetValue(parts[i], out node))
                {
                    return false;
                }

                if (i == parts.Length - 1)
                {
                    return true;
                }

                if (!node.IsObject)
                {
                    return false;
                }

                current = node.Object;
            }

            return false;
        }

        static string[] SplitPath(string path, bool requireNonEmpty)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var trimmed = path.Trim('/');
            if (trimmed.Length == 0)
            {
                if (requireNonEmpty)
                {
                    throw new ArgumentException("Path must not be empty.", nameof(path));
                }

                return Array.Empty<string>();
            }

            var parts = trimmed.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                {
                    throw new ArgumentException("Path contains an empty segment.", nameof(path));
                }
            }

            return parts;
        }

        sealed class Node
        {
            Node(bool isObject, PropertyTree obj, PropertyValue value)
            {
                IsObject = isObject;
                Object = obj;
                Value = value;
            }

            public bool IsObject { get; }
            public PropertyTree Object { get; }
            public PropertyValue Value { get; }

            public static Node FromLeaf(PropertyValue value)
            {
                return new Node(false, null, value);
            }

            public static Node FromTree(PropertyTree tree)
            {
                return new Node(true, tree, default);
            }

            public Node Clone()
            {
                return IsObject ? FromTree(Object.Clone()) : FromLeaf(Value);
            }
        }
    }
}
