using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mmzkworks.muShortcutPanel.Editor
{
    internal static class ShortcutItemKind
    {
        public const string Asset = "asset";
        public const string Url = "url";
        public const string Label = "label";
        public const string Menu = "menu";

        /// <summary>Runs the items listed in <see cref="ShortcutPanelItem.steps"/> one after another.</summary>
        public const string Action = "action";

        public static bool IsValid(string kind) =>
            kind == Asset || kind == Url || kind == Label || kind == Menu || kind == Action;
    }

    /// <summary>One element placed on the panel. Position and size are in panel (content) coordinates.</summary>
    [Serializable]
    internal sealed class ShortcutPanelItem
    {
        public string id = "";
        public string kind = ShortcutItemKind.Label;

        /// <summary>Label text, or the display name override for asset / URL items (empty = automatic).</summary>
        public string title = "";

        /// <summary>Asset GUID (asset items). Used first when resolving, so moves/renames are followed.</summary>
        public string guid = "";

        /// <summary>Asset path at the time it was registered (asset items). Fallback when the GUID cannot be resolved.</summary>
        public string path = "";

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

        public ShortcutPanelItem Clone()
        {
            var c = (ShortcutPanelItem)MemberwiseClone();
            c.id = NewId();
            c.steps = steps != null ? new List<string>(steps) : new List<string>();
            return c;
        }

        public static string NewId() => Guid.NewGuid().ToString("N");
    }

    [Serializable]
    internal sealed class ShortcutPanelDocument
    {
        public int version = ShortcutPanelSerializer.CurrentVersion;
        public List<ShortcutPanelItem> items = new List<ShortcutPanelItem>();

        public ShortcutPanelItem Find(string id)
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

    internal static class ShortcutPanelLayout
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
            return new Rect(Mathf.Max(0f, p.x), Mathf.Max(0f, p.y), start.width, start.height);
        }

        public static Rect Resized(Rect start, Vector2 delta)
        {
            var w = Mathf.Max(MinWidth, Snap(start.width + delta.x));
            var h = Mathf.Max(MinHeight, Snap(start.height + delta.y));
            return new Rect(start.x, start.y, w, h);
        }

        public static Rect ResizeHandleRect(Rect itemRect)
        {
            return new Rect(itemRect.xMax - ResizeHandleSize, itemRect.yMax - ResizeHandleSize,
                ResizeHandleSize, ResizeHandleSize);
        }

        public const float ActionKnobSize = 12f;

        /// <summary>Knob at the bottom center of an action item; drag from it to trace the steps.</summary>
        public static Rect ActionKnobRect(Rect itemRect)
        {
            return new Rect(itemRect.center.x - ActionKnobSize * 0.5f, itemRect.yMax - ActionKnobSize * 0.5f,
                ActionKnobSize, ActionKnobSize);
        }

        public static Rect RectFromPoints(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// Offset to apply to every item of a group move. The dragged item (<paramref name="primaryStart"/>) snaps to the grid,
        /// the others keep their relative positions, and no item goes above / left of the origin.
        /// </summary>
        public static Vector2 GroupMoveOffset(Rect primaryStart, IEnumerable<Rect> groupStarts, Vector2 delta)
        {
            var snapped = Snap(primaryStart.position + delta) - primaryStart.position;
            foreach (var r in groupStarts)
            {
                snapped.x = Mathf.Max(snapped.x, -r.x);
                snapped.y = Mathf.Max(snapped.y, -r.y);
            }
            return snapped;
        }

        /// <summary>Returns the topmost item (last in draw order) under <paramref name="p"/>, or null.</summary>
        public static ShortcutPanelItem HitTest(IReadOnlyList<ShortcutPanelItem> items, Vector2 p)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Rect.Contains(p)) return items[i];
            }
            return null;
        }

        /// <summary>Bounding size of all items plus a margin, so there is always room to drop below / to the right.</summary>
        public static Vector2 ContentSize(IReadOnlyList<ShortcutPanelItem> items, float margin)
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

    internal static class ShortcutPanelSerializer
    {
        public const int CurrentVersion = 1;

        public static string ToJson(ShortcutPanelDocument doc) => JsonUtility.ToJson(doc, true);

        /// <summary>Parses JSON leniently. Empty / broken input yields an empty document.</summary>
        public static ShortcutPanelDocument FromJson(string json)
        {
            ShortcutPanelDocument doc = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    doc = JsonUtility.FromJson<ShortcutPanelDocument>(json);
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarning($"[muShortcutPanel] Failed to parse panel JSON: {e.Message}");
                }
            }

            doc ??= new ShortcutPanelDocument();
            Normalize(doc);
            return doc;
        }

        internal static void Normalize(ShortcutPanelDocument doc)
        {
            doc.items ??= new List<ShortcutPanelItem>();
            doc.items.RemoveAll(i => i == null);

            var seen = new HashSet<string>();
            foreach (var item in doc.items)
            {
                if (string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                {
                    item.id = ShortcutPanelItem.NewId();
                    seen.Add(item.id);
                }

                if (!ShortcutItemKind.IsValid(item.kind))
                    item.kind = ShortcutItemKind.Label;

                item.title ??= "";
                item.guid ??= "";
                item.path ??= "";
                item.url ??= "";
                item.menu ??= "";
                item.steps ??= new List<string>();
                item.steps.RemoveAll(string.IsNullOrEmpty);
                item.x = Mathf.Max(0f, item.x);
                item.y = Mathf.Max(0f, item.y);
                item.width = Mathf.Max(ShortcutPanelLayout.MinWidth, item.width);
                item.height = Mathf.Max(ShortcutPanelLayout.MinHeight, item.height);
                if (item.fontSize < 0) item.fontSize = 0;
            }

            if (doc.version <= 0) doc.version = CurrentVersion;
        }
    }

    /// <summary>Naming rules and discovery of <c>*.shortcutpanel.json</c> files.</summary>
    internal static class ShortcutPanelFiles
    {
        public const string Suffix = ".shortcutpanel.json";

        public static bool IsPanelFile(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"Assets/Foo/Work.shortcutpanel.json" -> "Work"</summary>
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
            if (path.EndsWith(".shortcutpanel", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - ".shortcutpanel".Length);
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
                result.Add(rel);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }
}
