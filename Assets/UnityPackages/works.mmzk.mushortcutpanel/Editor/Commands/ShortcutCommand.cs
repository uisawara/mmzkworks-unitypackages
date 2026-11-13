using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmzkworks.muShortcutPanel.Editor
{
    internal static class ShortcutCommandCategory
    {
        public const string Prefab = "Prefab";
        public const string GameObject = "GameObject";
        public const string Scene = "Scene";
        public const string Asset = "Asset";

        public static readonly string[] All = { Prefab, GameObject, Scene, Asset };
    }

    internal enum ShortcutParamKind
    {
        /// <summary>Free text.</summary>
        Text,

        /// <summary>Project folder path ("Assets/..."), with a folder picker.</summary>
        Folder,
        Bool,

        /// <summary>One of <see cref="ShortcutCommandParam.Choices"/>.</summary>
        Choice,

        /// <summary>An asset reference, stored as its GUID.</summary>
        Asset,
        Vector3,

        /// <summary>Component type name, e.g. "Rigidbody" or "MyNamespace.Foo".</summary>
        TypeName
    }

    /// <summary>One argument a command takes; shown as a field in the property panel.</summary>
    internal sealed class ShortcutCommandParam
    {
        public string Key;
        public string Label;
        public string Tooltip = "";
        public ShortcutParamKind Kind = ShortcutParamKind.Text;
        public string Default = "";
        public string[] Choices = Array.Empty<string>();

        /// <summary>Asset type accepted by an <see cref="ShortcutParamKind.Asset"/> parameter.</summary>
        public Type AssetType = typeof(Object);

        /// <summary>When set, the field is shown only while the parameter <c>ShowWhenKey</c> has the value <c>ShowWhenValue</c>.</summary>
        public string ShowWhenKey;
        public string ShowWhenValue;

        /// <summary>Shown in the shortcut's summary when the value is empty, e.g. "(Project folder)".</summary>
        public string EmptyText;

        public ShortcutCommandParam WithEmptyText(string text)
        {
            EmptyText = text;
            return this;
        }

        public static ShortcutCommandParam Text(string key, string label, string def = "", string tooltip = "") =>
            new ShortcutCommandParam { Key = key, Label = label, Default = def, Tooltip = tooltip };

        public static ShortcutCommandParam Folder(string key, string label, string def = "", string tooltip = "") =>
            new ShortcutCommandParam { Key = key, Label = label, Kind = ShortcutParamKind.Folder, Default = def, Tooltip = tooltip };

        public static ShortcutCommandParam Bool(string key, string label, bool def, string tooltip = "") =>
            new ShortcutCommandParam { Key = key, Label = label, Kind = ShortcutParamKind.Bool, Default = def ? "true" : "false", Tooltip = tooltip };

        public static ShortcutCommandParam Choice(string key, string label, string[] choices, string def = null, string tooltip = "") =>
            new ShortcutCommandParam
            {
                Key = key, Label = label, Kind = ShortcutParamKind.Choice, Choices = choices, Default = def ?? choices[0], Tooltip = tooltip
            };

        public static ShortcutCommandParam Asset<T>(string key, string label, string tooltip = "") where T : Object =>
            new ShortcutCommandParam { Key = key, Label = label, Kind = ShortcutParamKind.Asset, AssetType = typeof(T), Tooltip = tooltip };

        public static ShortcutCommandParam Vector3(string key, string label, string tooltip = "") =>
            new ShortcutCommandParam { Key = key, Label = label, Kind = ShortcutParamKind.Vector3, Default = "0,0,0", Tooltip = tooltip };

        public static ShortcutCommandParam TypeName(string key, string label, string tooltip = "") =>
            new ShortcutCommandParam { Key = key, Label = label, Kind = ShortcutParamKind.TypeName, Tooltip = tooltip };

        public ShortcutCommandParam When(string key, string value)
        {
            ShowWhenKey = key;
            ShowWhenValue = value;
            return this;
        }
    }

    /// <summary>A built-in automation command. It works on the current Selection and leaves its result selected.</summary>
    internal sealed class ShortcutCommand
    {
        public string Id;
        public string Category;
        public string Name;
        public string Description;
        public ShortcutCommandParam[] Params = Array.Empty<ShortcutCommandParam>();

        /// <summary>Runs the command. Return false (usually via <see cref="ShortcutCommandContext.Fail"/>) to stop an action.</summary>
        public Func<ShortcutCommandContext, bool> Run;

        public ShortcutCommandParam FindParam(string key) => Params.FirstOrDefault(p => p.Key == key);

        public bool IsVisible(ShortcutCommandParam p, ShortcutPanelItem item)
        {
            if (string.IsNullOrEmpty(p.ShowWhenKey)) return true;
            return ShortcutCommandContext.Value(this, item, p.ShowWhenKey) == p.ShowWhenValue;
        }

        /// <summary>Short description of the arguments for the shortcut's second line, e.g. "Assets/Prefabs, Unique".</summary>
        public string Summary(ShortcutPanelItem item)
        {
            var parts = new List<string>();
            foreach (var p in Params)
            {
                if (!IsVisible(p, item)) continue;
                var v = ShortcutCommandContext.Value(this, item, p.Key);
                switch (p.Kind)
                {
                    case ShortcutParamKind.Bool:
                        if (v == "true") parts.Add(p.Label);
                        break;
                    case ShortcutParamKind.Asset:
                        if (!string.IsNullOrEmpty(v))
                        {
                            var path = AssetDatabase.GUIDToAssetPath(v);
                            parts.Add(string.IsNullOrEmpty(path) ? "(missing asset)" : System.IO.Path.GetFileNameWithoutExtension(path));
                        }
                        break;
                    case ShortcutParamKind.Vector3:
                        if (v != p.Default && !string.IsNullOrEmpty(v)) parts.Add($"({v})");
                        break;
                    default:
                        if (!string.IsNullOrEmpty(v)) parts.Add(v);
                        else if (!string.IsNullOrEmpty(p.EmptyText)) parts.Add(p.EmptyText);
                        break;
                }
            }
            return string.Join(", ", parts);
        }
    }

    /// <summary>Arguments and Selection helpers passed to a running command.</summary>
    internal sealed class ShortcutCommandContext
    {
        public readonly ShortcutCommand Command;
        public readonly ShortcutPanelItem Item;
        public string Error { get; private set; }

        public ShortcutCommandContext(ShortcutCommand command, ShortcutPanelItem item)
        {
            Command = command;
            Item = item;
        }

        /// <summary>The stored value, or the parameter's default when it was never set.</summary>
        internal static string Value(ShortcutCommand command, ShortcutPanelItem item, string key)
        {
            var v = item?.GetArg(key);
            if (v != null) return v;
            return command?.FindParam(key)?.Default ?? "";
        }

        public string GetString(string key) => Value(Command, Item, key).Trim();

        public bool GetBool(string key) => string.Equals(Value(Command, Item, key), "true", StringComparison.OrdinalIgnoreCase);

        public Vector3 GetVector3(string key) =>
            ShortcutCommandUtil.TryParseVector3(Value(Command, Item, key), out var v) ? v : UnityEngine.Vector3.zero;

        /// <summary>
        /// Reads an asset parameter. <paramref name="asset"/> is null when the parameter is empty (then true is returned);
        /// false means the parameter points to an asset that no longer exists (and <see cref="Error"/> is set).
        /// </summary>
        public bool TryGetAsset<T>(string key, out T asset) where T : Object
        {
            asset = null;
            var guid = Value(Command, Item, key);
            if (string.IsNullOrEmpty(guid)) return true;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return true;
            return Fail($"{Command.FindParam(key)?.Label ?? key}: asset not found");
        }

        public bool Fail(string message)
        {
            Error = message;
            return false;
        }

        /// <summary>Selected GameObjects in a scene or prefab stage (not assets).</summary>
        public static List<GameObject> SelectedSceneObjects() =>
            Selection.gameObjects.Where(g => g != null && !EditorUtility.IsPersistent(g)).ToList();

        /// <summary>Selected objects that are assets.</summary>
        public static List<Object> SelectedAssets() =>
            Selection.objects.Where(o => o != null && EditorUtility.IsPersistent(o)).ToList();

        /// <summary>Selected prefab assets (root GameObjects of .prefab files).</summary>
        public static List<GameObject> SelectedPrefabAssets() =>
            SelectedAssets().OfType<GameObject>().Where(g => PrefabUtility.IsPartOfPrefabAsset(g) && g.transform.parent == null).ToList();

        public static void Select(IEnumerable<Object> objects)
        {
            Selection.objects = objects.Where(o => o != null).ToArray();
        }
    }

    internal static class ShortcutCommandRegistry
    {
        private static List<ShortcutCommand> _all;
        private static Dictionary<string, ShortcutCommand> _byId;

        public static IReadOnlyList<ShortcutCommand> All
        {
            get
            {
                Build();
                return _all;
            }
        }

        public static ShortcutCommand Find(string id)
        {
            Build();
            return id != null && _byId.TryGetValue(id, out var c) ? c : null;
        }

        private static void Build()
        {
            if (_all != null) return;
            _all = new List<ShortcutCommand>();
            _all.AddRange(PrefabCommands.Create());
            _all.AddRange(GameObjectCommands.Create());
            _all.AddRange(SceneAssetCommands.Create());

            _byId = new Dictionary<string, ShortcutCommand>(StringComparer.Ordinal);
            foreach (var c in _all)
            {
                if (_byId.ContainsKey(c.Id))
                    Debug.LogError($"[muShortcutPanel] Duplicate command id: {c.Id}");
                else
                    _byId.Add(c.Id, c);
            }
        }
    }
}
