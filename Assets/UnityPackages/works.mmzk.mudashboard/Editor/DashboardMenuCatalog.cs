using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Mmzkworks.muDashboard.Editor
{
    /// <summary>Where a menu command shortcut comes from (derived from the menu path).</summary>
    internal static class ShortcutMenuTarget
    {
        /// <summary>Unity Editor main menu (File / Edit / Window / Tools / ...).</summary>
        public const string Editor = "editor";

        /// <summary>Project window context menu (<c>Assets/...</c>), acts on the selected assets.</summary>
        public const string Project = "project";

        /// <summary>Hierarchy window context menu (<c>GameObject/...</c>), acts on the selected GameObjects.</summary>
        public const string Hierarchy = "hierarchy";
    }

    /// <summary>Pure helpers for menu item paths (no Unity editor state involved).</summary>
    internal static class DashboardMenuPath
    {
        private const string ProjectRoot = "Assets/";
        private const string HierarchyRoot = "GameObject/";

        public static string TargetOf(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return ShortcutMenuTarget.Editor;
            if (menuPath.StartsWith(ProjectRoot, StringComparison.Ordinal)) return ShortcutMenuTarget.Project;
            if (menuPath.StartsWith(HierarchyRoot, StringComparison.Ordinal)) return ShortcutMenuTarget.Hierarchy;
            return ShortcutMenuTarget.Editor;
        }

        public static string TargetLabel(string target)
        {
            switch (target)
            {
                case ShortcutMenuTarget.Project: return "Project";
                case ShortcutMenuTarget.Hierarchy: return "Hierarchy";
                default: return "Editor";
            }
        }

        /// <summary>"Window/General/Console" -> "Console"</summary>
        public static string DefaultTitle(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return "";
            var i = menuPath.LastIndexOf('/');
            return i >= 0 && i < menuPath.Length - 1 ? menuPath.Substring(i + 1) : menuPath;
        }

        /// <summary>"Window/General/Console" -> "Window/General"</summary>
        public static string ParentPath(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return "";
            var i = menuPath.LastIndexOf('/');
            return i > 0 ? menuPath.Substring(0, i) : "";
        }

        /// <summary>Removes the hotkey suffix of a <see cref="MenuItem"/> path: "Tools/Foo %#g" -> "Tools/Foo".</summary>
        public static string StripHotkey(string menuItemPath)
        {
            if (string.IsNullOrEmpty(menuItemPath)) return menuItemPath ?? "";
            var path = menuItemPath.Trim();
            var space = path.LastIndexOf(' ');
            if (space > 0 && space < path.Length - 2 && "%#&^_".IndexOf(path[space + 1]) >= 0)
            {
                var token = path.Substring(space + 1);
                if (token.IndexOf('/') < 0) path = path.Substring(0, space).TrimEnd();
            }
            return path;
        }

        /// <summary>
        /// Accepts text that looks like a menu path ("Root/Item", single line, no URL scheme).
        /// Whether the menu item really exists is checked separately.
        /// </summary>
        public static bool TryParse(string text, out string menuPath)
        {
            menuPath = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = text.Trim();
            if (t.IndexOfAny(new[] { '\r', '\n' }) >= 0) return false;
            if (t.Contains("://")) return false;

            t = StripHotkey(t);
            var segments = t.Split('/');
            if (segments.Length < 2) return false;
            foreach (var s in segments)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
            }

            menuPath = t;
            return true;
        }
    }

    /// <summary>All menu item paths currently registered in the editor (built-in and [MenuItem]).</summary>
    internal static class DashboardMenuCatalog
    {
        private static readonly string[] BuiltInRoots =
        {
            "File", "Edit", "Assets", "GameObject", "Component", "Window", "Help", "Tools", "Jobs"
        };

        private static List<string> _paths;
        private static HashSet<string> _set;

        public static IReadOnlyList<string> All
        {
            get
            {
                if (_paths == null) Build();
                return _paths;
            }
        }

        public static bool Exists(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return false;
            if (_paths == null) Build();
            return _set.Contains(menuPath);
        }

        public static void Invalidate()
        {
            _paths = null;
            _set = null;
        }

        private static void Build()
        {
            _paths = new List<string>();
            _set = new HashSet<string>(StringComparer.Ordinal);

            void Add(string p)
            {
                if (string.IsNullOrEmpty(p) || p.StartsWith("CONTEXT/", StringComparison.Ordinal) ||
                    p.StartsWith("internal:", StringComparison.Ordinal) || p.IndexOf('/') <= 0) return;
                if (_set.Add(p)) _paths.Add(p);
            }

            // [MenuItem] declared in scripts (also tells us extra root menus such as "Tools" / "MyCompany").
            var scriptPaths = new List<string>();
            var roots = new List<string>(BuiltInRoots);
            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (var attr in method.GetCustomAttributes(typeof(MenuItem), false))
                {
                    var mi = (MenuItem)attr;
                    if (mi.validate) continue;
                    var p = DashboardMenuPath.StripHotkey(mi.menuItem);
                    scriptPaths.Add(p);
                    var slash = p.IndexOf('/');
                    if (slash > 0)
                    {
                        var root = p.Substring(0, slash);
                        if (!roots.Contains(root)) roots.Add(root);
                    }
                }
            }

            // Built-in (native) menu items, in menu order. Menu.ExtractSubmenus is internal, so it is optional.
            var extract = typeof(Menu).GetMethod("ExtractSubmenus",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (extract != null)
            {
                foreach (var root in roots)
                {
                    try
                    {
                        if (extract.Invoke(null, new object[] { root }) is string[] items)
                        {
                            foreach (var p in items) Add(p);
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore: fall back to the [MenuItem] list.
                    }
                }
            }

            foreach (var p in scriptPaths) Add(p);
        }
    }

    /// <summary>Searchable picker of menu commands, grouped into Editor / Project window / Hierarchy window.</summary>
    internal sealed class MenuCommandDropdown : AdvancedDropdown
    {
        private readonly Action<string> _onPicked;

        private sealed class PathItem : AdvancedDropdownItem
        {
            public readonly string Path;

            public PathItem(string name, string path) : base(name)
            {
                Path = path;
            }
        }

        private MenuCommandDropdown(Action<string> onPicked) : base(new AdvancedDropdownState())
        {
            _onPicked = onPicked;
            minimumSize = new Vector2(320f, 420f);
        }

        /// <summary>Must be called from OnGUI. <paramref name="rect"/> is in GUI coordinates.</summary>
        public static void Show(Rect rect, Action<string> onPicked)
        {
            DashboardMenuCatalog.Invalidate(); // pick up menus added since last time
            new MenuCommandDropdown(onPicked).Show(rect);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var nextId = 1;
            var root = new AdvancedDropdownItem("Menu Command") { id = nextId++ };
            var groups = new Dictionary<string, AdvancedDropdownItem>
            {
                { ShortcutMenuTarget.Editor, new AdvancedDropdownItem("Editor Menu") { id = nextId++ } },
                { ShortcutMenuTarget.Project, new AdvancedDropdownItem("Project Window (Assets)") { id = nextId++ } },
                { ShortcutMenuTarget.Hierarchy, new AdvancedDropdownItem("Hierarchy Window (GameObject)") { id = nextId++ } }
            };
            root.AddChild(groups[ShortcutMenuTarget.Editor]);
            root.AddChild(groups[ShortcutMenuTarget.Project]);
            root.AddChild(groups[ShortcutMenuTarget.Hierarchy]);

            var folders = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            foreach (var path in DashboardMenuCatalog.All)
            {
                var target = DashboardMenuPath.TargetOf(path);
                var parent = groups[target];
                var segments = path.Split('/');
                // The Project / Hierarchy groups already stand for "Assets" / "GameObject".
                var start = target == ShortcutMenuTarget.Editor ? 0 : 1;
                var key = target;
                for (var i = start; i < segments.Length - 1; i++)
                {
                    key += "/" + segments[i];
                    if (!folders.TryGetValue(key, out var folder))
                    {
                        folder = new AdvancedDropdownItem(segments[i]) { id = nextId++ };
                        parent.AddChild(folder);
                        folders.Add(key, folder);
                    }
                    parent = folder;
                }

                parent.AddChild(new PathItem(segments[segments.Length - 1], path) { id = nextId++ });
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is PathItem p) _onPicked?.Invoke(p.Path);
        }
    }
}
