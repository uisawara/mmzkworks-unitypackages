using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mmzkworks.muDashboard.Editor
{
    internal static class ShortcutItemKind
    {
        public const string Asset = "asset";
        public const string Url = "url";
        public const string Label = "label";
        public const string Menu = "menu";

        /// <summary>Runs the items listed in <see cref="DashboardItem.steps"/> one after another.</summary>
        public const string Action = "action";

        /// <summary>Runs a built-in automation command (<see cref="DashboardItem.command"/>) with arguments.</summary>
        public const string Command = "command";

        /// <summary>A picture (a texture asset referenced by <see cref="DashboardItem.guid"/> / path). Not clickable.</summary>
        public const string Image = "image";

        /// <summary>
        /// A GameObject in a scene (<see cref="DashboardItem.globalId"/>; the scene asset in guid / path,
        /// the hierarchy path in <see cref="DashboardItem.hierarchy"/>).
        /// </summary>
        public const string SceneObject = "scene-object";

        public static bool IsValid(string kind) =>
            kind == Asset || kind == Url || kind == Label || kind == Menu || kind == Action || kind == Command || kind == Image
            || kind == SceneObject;

        /// <summary>Kinds that do something when clicked / run as an action step (labels and images are decoration).</summary>
        public static bool IsRunnable(string kind) =>
            kind == Asset || kind == Url || kind == Menu || kind == Action || kind == Command || kind == SceneObject;
    }

    /// <summary>One argument of a command item. A list of these stands in for a dictionary (JsonUtility cannot store one).</summary>
    [Serializable]
    internal sealed class ShortcutCommandArg
    {
        public string key = "";
        public string value = "";

        public ShortcutCommandArg()
        {
        }

        public ShortcutCommandArg(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>One element placed on the panel. Position and size are in panel (content) coordinates.</summary>
    [Serializable]
    internal sealed class DashboardItem
    {
        public string id = "";
        public string kind = ShortcutItemKind.Label;

        /// <summary>Label text, or the display name override for asset / URL items (empty = automatic).</summary>
        public string title = "";

        /// <summary>Asset GUID (asset / image items; the scene's GUID for scene objects). The only key used to resolve the asset, so moves/renames are followed.</summary>
        public string guid = "";

        /// <summary>Asset path at the time it was registered (the scene's path for scene objects). Display only: links resolve by GUID, never by this path.</summary>
        public string path = "";

        /// <summary>GlobalObjectId of a scene object (scene object items).</summary>
        public string globalId = "";

        /// <summary>Hierarchy path of a scene object when it was registered, e.g. "Env/Trees/Oak" (scene object items).</summary>
        public string hierarchy = "";

        public string url = "";

        /// <summary>Menu item path (menu items), e.g. "Window/General/Console" or "Assets/Create/Folder".</summary>
        public string menu = "";

        public float x;
        public float y;
        public float width;
        public float height;

        public int fontSize;

        /// <summary>Item ids run in order (action items). Nested actions are expanded at run time.</summary>
        public List<string> steps = new List<string>();

        /// <summary>Command id (command items), e.g. "prefab.create".</summary>
        public string command = "";

        /// <summary>Command arguments (command items). Assets are stored as GUIDs, vectors as "x,y,z", bools as "true"/"false".</summary>
        public List<ShortcutCommandArg> args = new List<ShortcutCommandArg>();

        public string GetArg(string key)
        {
            if (args == null) return null;
            foreach (var a in args)
            {
                if (a != null && a.key == key) return a.value;
            }
            return null;
        }

        public void SetArg(string key, string value)
        {
            args ??= new List<ShortcutCommandArg>();
            foreach (var a in args)
            {
                if (a == null || a.key != key) continue;
                a.value = value ?? "";
                return;
            }
            args.Add(new ShortcutCommandArg(key, value ?? ""));
        }

        public Rect Rect
        {
            get => new Rect(x, y, width, height);
            set
            {
                x = value.x;
                y = value.y;
                width = value.width;
                height = value.height;
            }
        }

        public DashboardItem Clone()
        {
            var c = (DashboardItem)MemberwiseClone();
            c.id = NewId();
            c.steps = steps != null ? new List<string>(steps) : new List<string>();
            c.args = new List<ShortcutCommandArg>();
            if (args != null)
            {
                foreach (var a in args)
                {
                    if (a != null) c.args.Add(new ShortcutCommandArg(a.key, a.value));
                }
            }
            return c;
        }

        public static string NewId() => Guid.NewGuid().ToString("N");
    }

    [Serializable]
    internal sealed class DashboardDocument
    {
        public int version = DashboardSerializer.CurrentVersion;
        public List<DashboardItem> items = new List<DashboardItem>();

        public DashboardItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in items)
            {
                if (item.id == id) return item;
            }
            return null;
        }

        /// <summary>Removes the given item ids from every action's steps (e.g. after those items were deleted).</summary>
        public void RemoveStepReferences(ICollection<string> ids)
        {
            if (ids == null || ids.Count == 0) return;
            foreach (var item in items)
            {
                item.steps?.RemoveAll(ids.Contains);
            }
        }
    }

    internal static class DashboardLayout
    {
        public const float GridSize = 8f;
        public const float MinWidth = 32f;
        public const float MinHeight = 16f;
        public const float ResizeHandleSize = 10f;

        public static readonly Vector2 DefaultShortcutSize = new Vector2(176f, 24f);
        public static readonly Vector2 DefaultLabelSize = new Vector2(176f, 24f);

        public static float Snap(float v) => Mathf.Round(v / GridSize) * GridSize;

        public static Vector2 Snap(Vector2 v) => new Vector2(Snap(v.x), Snap(v.y));

        public static Rect Moved(Rect start, Vector2 delta)
        {
            var p = Snap(start.position + delta);
            return new Rect(p.x, p.y, start.width, start.height);
        }

        public static Rect Resized(Rect start, Vector2 delta)
        {
            var w = Mathf.Max(MinWidth, Snap(start.width + delta.x));
            var h = Mathf.Max(MinHeight, Snap(start.height + delta.y));
            return new Rect(start.x, start.y, w, h);
        }

        /// <param name="zoom">Canvas zoom; the handle keeps the same size on screen.</param>
        public static Rect ResizeHandleRect(Rect itemRect, float zoom = 1f)
        {
            var size = ResizeHandleSize / zoom;
            return new Rect(itemRect.xMax - size, itemRect.yMax - size, size, size);
        }

        public const float ActionKnobSize = 12f;

        /// <summary>Knob at the bottom center of an action item; drag from it to trace the steps.</summary>
        /// <param name="zoom">Canvas zoom; the knob keeps the same size on screen.</param>
        public static Rect ActionKnobRect(Rect itemRect, float zoom = 1f)
        {
            var size = ActionKnobSize / zoom;
            return new Rect(itemRect.center.x - size * 0.5f, itemRect.yMax - size * 0.5f, size, size);
        }

        public static Rect RectFromPoints(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// Offset to apply to every item of a group move. The dragged item (<paramref name="primaryStart"/>) snaps to the grid
        /// and the others keep their relative positions. The canvas is unbounded, so negative positions are fine.
        /// </summary>
        public static Vector2 GroupMoveOffset(Rect primaryStart, Vector2 delta) =>
            Snap(primaryStart.position + delta) - primaryStart.position;

        /// <summary>Bounding rect of all items, or null when there are none.</summary>
        public static Rect? Bounds(IReadOnlyList<DashboardItem> items)
        {
            if (items == null || items.Count == 0) return null;
            var r = items[0].Rect;
            foreach (var item in items) r = Rect.MinMaxRect(Mathf.Min(r.xMin, item.x), Mathf.Min(r.yMin, item.y),
                Mathf.Max(r.xMax, item.x + item.width), Mathf.Max(r.yMax, item.y + item.height));
            return r;
        }

        /// <summary>Returns the topmost item (last in draw order) under <paramref name="p"/>, or null.</summary>
        public static DashboardItem HitTest(IReadOnlyList<DashboardItem> items, Vector2 p)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Rect.Contains(p)) return items[i];
            }
            return null;
        }

        /// <summary>Bounding size of all items plus a margin, so there is always room to drop below / to the right.</summary>
        public static Vector2 ContentSize(IReadOnlyList<DashboardItem> items, float margin)
        {
            var size = Vector2.zero;
            foreach (var item in items)
            {
                size.x = Mathf.Max(size.x, item.x + item.width);
                size.y = Mathf.Max(size.y, item.y + item.height);
            }
            return size + new Vector2(margin, margin);
        }
    }

    internal static class DashboardSerializer
    {
        public const int CurrentVersion = 1;

        public static string ToJson(DashboardDocument doc) => JsonUtility.ToJson(doc, true);

        /// <summary>Parses JSON leniently. Empty / broken input yields an empty document.</summary>
        public static DashboardDocument FromJson(string json)
        {
            DashboardDocument doc = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    doc = JsonUtility.FromJson<DashboardDocument>(json);
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarning($"[muDashboard] Failed to parse panel JSON: {e.Message}");
                }
            }

            doc ??= new DashboardDocument();
            Normalize(doc);
            return doc;
        }

        internal static void Normalize(DashboardDocument doc)
        {
            doc.items ??= new List<DashboardItem>();
            doc.items.RemoveAll(i => i == null);

            var seen = new HashSet<string>();
            foreach (var item in doc.items)
            {
                if (string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                {
                    item.id = DashboardItem.NewId();
                    seen.Add(item.id);
                }

                if (!ShortcutItemKind.IsValid(item.kind))
                    item.kind = ShortcutItemKind.Label;

                item.title ??= "";
                item.guid ??= "";
                item.path ??= "";
                item.globalId ??= "";
                item.hierarchy ??= "";
                item.url ??= "";
                item.menu ??= "";
                item.steps ??= new List<string>();
                item.steps.RemoveAll(string.IsNullOrEmpty);
                item.command ??= "";
                item.args ??= new List<ShortcutCommandArg>();
                item.args.RemoveAll(a => a == null || string.IsNullOrEmpty(a.key));
                foreach (var a in item.args) a.value ??= "";
                item.width = Mathf.Max(DashboardLayout.MinWidth, item.width);
                item.height = Mathf.Max(DashboardLayout.MinHeight, item.height);
                if (item.fontSize < 0) item.fontSize = 0;
            }

            if (doc.version <= 0) doc.version = CurrentVersion;
        }
    }

    /// <summary>Naming rules and discovery of <c>*.dashboard.json</c> files.</summary>
    internal static class DashboardFiles
    {
        public const string Suffix = ".dashboard.json";

        /// <summary>"x.dashboard.json" counts; a bare "dashboard.json" (no panel name) does not.</summary>
        public static bool IsPanelFile(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= Suffix.Length) return false;
            if (!path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)) return false;
            var before = path[path.Length - Suffix.Length - 1];
            return before != '/' && before != '\\';
        }

        /// <summary>"Assets/Foo/Work.dashboard.json" -> "Work"</summary>
        public static string DisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var file = Path.GetFileName(path);
            return IsPanelFile(file) ? file.Substring(0, file.Length - Suffix.Length) : file;
        }

        /// <summary>Makes sure a path chosen in a save dialog ends with <see cref="Suffix"/>.</summary>
        public static string EnsureSuffix(string path)
        {
            if (string.IsNullOrEmpty(path) || IsPanelFile(path)) return path;
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - ".json".Length);
            if (path.EndsWith(".dashboard", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - ".dashboard".Length);
            return path + Suffix;
        }

        /// <summary>Lists panel files under Assets/ as project-relative paths, sorted.</summary>
        public static List<string> FindAll(string projectRoot)
        {
            var result = new List<string>();
            var assetsDir = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsDir)) return result;

            foreach (var full in Directory.EnumerateFiles(assetsDir, "*" + Suffix, SearchOption.AllDirectories))
            {
                var rel = full.Substring(projectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (IsPanelFile(rel)) result.Add(rel);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }
}
