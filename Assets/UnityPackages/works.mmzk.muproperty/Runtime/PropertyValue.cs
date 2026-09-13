using System;
using UnityEngine;

namespace Mmzkworks.muProperty
{
    public enum PropertyValueKind
    {
        Bool,
        Int,
        Float,
        Vector2,
        Vector3,
        Vector4,
        Color,
        String
    }

    /// <summary>
    /// A typed leaf value in a <see cref="PropertyTree"/>.
    /// </summary>
    public readonly struct PropertyValue : IEquatable<PropertyValue>
    {
        readonly bool _bool;
        readonly int _int;
        readonly float _float;
        readonly Vector4 _vector;
        readonly string _string;

        PropertyValue(PropertyValueKind kind, bool b, int i, float f, Vector4 vector, string s)
        {
            Kind = kind;
            _bool = b;
            _int = i;
            _float = f;
            _vector = vector;
            _string = s;
        }

        public PropertyValueKind Kind { get; }

        public static PropertyValue From(bool value)
        {
            return new PropertyValue(PropertyValueKind.Bool, value, 0, 0f, default, null);
        }

        public static PropertyValue From(int value)
        {
            return new PropertyValue(PropertyValueKind.Int, false, value, 0f, default, null);
        }

        public static PropertyValue From(float value)
        {
            return new PropertyValue(PropertyValueKind.Float, false, 0, value, default, null);
        }

        public static PropertyValue From(Vector2 value)
        {
            return new PropertyValue(PropertyValueKind.Vector2, false, 0, 0f, new Vector4(value.x, value.y, 0f, 0f), null);
        }

        public static PropertyValue From(Vector3 value)
        {
            return new PropertyValue(PropertyValueKind.Vector3, false, 0, 0f, new Vector4(value.x, value.y, value.z, 0f), null);
        }

        public static PropertyValue From(Vector4 value)
        {
            return new PropertyValue(PropertyValueKind.Vector4, false, 0, 0f, value, null);
        }

        public static PropertyValue From(Color value)
        {
            return new PropertyValue(PropertyValueKind.Color, false, 0, 0f, new Vector4(value.r, value.g, value.b, value.a), null);
        }

        public static PropertyValue From(string value)
        {
            return new PropertyValue(PropertyValueKind.String, false, 0, 0f, default, value ?? string.Empty);
        }

        public bool AsBool()
        {
            EnsureKind(PropertyValueKind.Bool);
            return _bool;
        }

        public int AsInt()
        {
            EnsureKind(PropertyValueKind.Int);
            return _int;
        }

        public float AsFloat()
        {
            EnsureKind(PropertyValueKind.Float);
            return _float;
        }

        public Vector2 AsVector2()
        {
            EnsureKind(PropertyValueKind.Vector2);
            return new Vector2(_vector.x, _vector.y);
        }

        public Vector3 AsVector3()
        {
            EnsureKind(PropertyValueKind.Vector3);
            return new Vector3(_vector.x, _vector.y, _vector.z);
        }

        public Vector4 AsVector4()
        {
            EnsureKind(PropertyValueKind.Vector4);
            return _vector;
        }

        public Color AsColor()
        {
            EnsureKind(PropertyValueKind.Color);
            return new Color(_vector.x, _vector.y, _vector.z, _vector.w);
        }

        public string AsString()
        {
            EnsureKind(PropertyValueKind.String);
            return _string ?? string.Empty;
        }

        public bool Equals(PropertyValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }

            switch (Kind)
            {
                case PropertyValueKind.Bool:
                    return _bool == other._bool;
                case PropertyValueKind.Int:
                    return _int == other._int;
                case PropertyValueKind.Float:
                    return _float.Equals(other._float);
                case PropertyValueKind.Vector2:
                case PropertyValueKind.Vector3:
                case PropertyValueKind.Vector4:
                case PropertyValueKind.Color:
                    return _vector.Equals(other._vector);
                case PropertyValueKind.String:
                    return string.Equals(_string, other._string, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind * 397;
                switch (Kind)
                {
                    case PropertyValueKind.Bool:
                        hash = (hash * 397) ^ _bool.GetHashCode();
                        break;
                    case PropertyValueKind.Int:
                        hash = (hash * 397) ^ _int;
                        break;
                    case PropertyValueKind.Float:
                        hash = (hash * 397) ^ _float.GetHashCode();
                        break;
                    case PropertyValueKind.Vector2:
                    case PropertyValueKind.Vector3:
                    case PropertyValueKind.Vector4:
                    case PropertyValueKind.Color:
                        hash = (hash * 397) ^ _vector.GetHashCode();
                        break;
                    case PropertyValueKind.String:
                        hash = (hash * 397) ^ (_string != null ? _string.GetHashCode() : 0);
                        break;
                }

                return hash;
            }
        }

        public static bool operator ==(PropertyValue left, PropertyValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PropertyValue left, PropertyValue right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case PropertyValueKind.Bool:
                    return _bool ? "true" : "false";
                case PropertyValueKind.Int:
                    return _int.ToString();
                case PropertyValueKind.Float:
                    return _float.ToString();
                case PropertyValueKind.Vector2:
                    return AsVector2().ToString();
                case PropertyValueKind.Vector3:
                    return AsVector3().ToString();
                case PropertyValueKind.Vector4:
                    return AsVector4().ToString();
                case PropertyValueKind.Color:
                    return AsColor().ToString();
                case PropertyValueKind.String:
                    return _string ?? string.Empty;
                default:
                    return Kind.ToString();
            }
        }

        void EnsureKind(PropertyValueKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException($"Property value is {Kind}, not {expected}.");
            }
        }
    }
}
