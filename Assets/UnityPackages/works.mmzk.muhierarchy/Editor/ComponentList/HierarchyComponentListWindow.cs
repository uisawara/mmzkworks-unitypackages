using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;

namespace Mmzkworks.muHierarchy.Editor
{
    // Lists the components used in the open scenes (or the open Prefab stage),
    // grouped by assembly (= asmdef name for user scripts) as a tag cloud.
    // Clicking a tag tints the matching rows in the Hierarchy window.
    public sealed class HierarchyComponentListWindow : EditorWindow
    {
        private const string MissingScriptKey = "<missing>";
        private const string MissingScriptSection = "(Missing Script)";
        private const string PrefsKeyShowUnityEngine = "muHierarchy.ComponentList.ShowUnityEngine";
        private const string PrefsKeyIncludeInactive = "muHierarchy.ComponentList.IncludeInactive";
        private const float TagHeight = 20f;
        private const float TagIconSize = 14f;
        private const float TagSpacing = 4f;

        private static readonly Color[] Palette =
        {
            new Color(1.00f, 0.60f, 0.10f),
            new Color(0.20f, 0.70f, 1.00f),
            new Color(0.40f, 0.90f, 0.30f),
            new Color(1.00f, 0.35f, 0.55f),
            new Color(0.70f, 0.45f, 1.00f),
            new Color(1.00f, 0.90f, 0.20f),
            new Color(0.20f, 0.90f, 0.85f),
            new Color(0.95f, 0.30f, 0.25f),
        };

        private sealed class Entry
        {
            public string Key;
            public string Name;
            public string FullName;
            public Texture Icon;
            public Component FirstComponent;
            public int ComponentCount;
            public readonly List<GameObject> GameObjects = new List<GameObject>();
            public readonly HashSet<int> GameObjectIds = new HashSet<int>();
        }

        private sealed class Section
        {
            public string Name;
            public bool IsUnityEngine;
            public int ComponentCount;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        private readonly List<Section> _sections = new List<Section>();
        private readonly Dictionary<string, Entry> _entriesByKey = new Dictionary<string, Entry>();
        private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
        private GUIStyle _tagStyle;
        private int _objectCount;
        private string _scopeLabel = string.Empty;
        private bool _dirty = true;
        private Vector2 _scroll;
        private SearchField _searchField;
        private GUIStyle _sectionFoldoutStyle;

        // Highlighted entries survive domain reloads via serialization.
        [SerializeField] private List<string> _activeKeys = new List<string>();
        [SerializeField] private List<Color> _activeColors = new List<Color>();
        [SerializeField] private int _nextPaletteIndex;
        [SerializeField] private string _filter = string.Empty;

        private static bool ShowUnityEngine
        {
            get => EditorPrefs.GetBool(PrefsKeyShowUnityEngine, true);
            set => EditorPrefs.SetBool(PrefsKeyShowUnityEngine, value);
        }

        private static bool IncludeInactive
        {
            get => EditorPrefs.GetBool(PrefsKeyIncludeInactive, true);
            set => EditorPrefs.SetBool(PrefsKeyIncludeInactive, value);
        }

        [MenuItem("Tools/muHierarchy/Component List...", false, 2040)]
        public static void Open()
        {
            var window = GetWindow<HierarchyComponentListWindow>();
            window.titleContent = new GUIContent("Component List", EditorGUIUtility.IconContent("UnityEditor.SceneHierarchyWindow").image);
            window.Show();
        }

        // Shown in the Hierarchy window's right-click menu. Unity invokes GameObject menu items
        // once per selected object, which is harmless here since GetWindow reuses the window.
        [MenuItem("GameObject/muHierarchy/Component List...", false, 21100 + 20)]
        private static void OpenFromContextMenu() => Open();

        private void OnEnable()
        {
            _searchField = new SearchField();
            wantsMouseMove = true;
            EditorApplication.hierarchyChanged += MarkDirty;
            Undo.undoRedoPerformed += MarkDirty;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
            _dirty = true;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkDirty;
            Undo.undoRedoPerformed -= MarkDirty;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            ComponentListHighlighter.Clear();
        }

        private void OnPrefabStageChanged(PrefabStage _) => MarkDirty();

        private void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            // Rescan only on Layout so the control set stays consistent within a frame.
            if (_dirty && Event.current.type == EventType.Layout)
            {
                _dirty = false;
                Scan();
                ApplyHighlight();
            }

            DrawToolbar();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                var width = position.width - 24f;
                var anyDrawn = false;
                foreach (var section in _sections)
                {
                    if (section.IsUnityEngine && !ShowUnityEngine)
                        continue;
                    var entries = FilterEntries(section);
                    if (entries.Count == 0)
                        continue;
                    anyDrawn = true;
                    DrawSection(section, entries, width);
                }
                if (!anyDrawn)
                    EditorGUILayout.HelpBox("No components found.", MessageType.Info);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    MarkDirty();

                EditorGUI.BeginChangeCheck();
                var showUnity = GUILayout.Toggle(ShowUnityEngine, new GUIContent("UnityEngine", "Show UnityEngine.* assemblies"), EditorStyles.toolbarButton);
                var includeInactive = GUILayout.Toggle(IncludeInactive, new GUIContent("Inactive", "Include inactive GameObjects"), EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck())
                {
                    ShowUnityEngine = showUnity;
                    if (includeInactive != IncludeInactive)
                    {
                        IncludeInactive = includeInactive;
                        MarkDirty();
                    }
                }

                using (new EditorGUI.DisabledScope(_activeKeys.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Clear Hierarchy highlight"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                        ClearActive();
                }

                GUILayout.FlexibleSpace();
                _filter = _searchField.OnToolbarGUI(_filter, GUILayout.MaxWidth(220f));
            }

            var componentCount = _sections.Sum(s => s.ComponentCount);
            EditorGUILayout.LabelField(
                $"{_scopeLabel}  —  {_entriesByKey.Count} types / {componentCount} components / {_objectCount} objects",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Click: highlight in Hierarchy   Shift/Ctrl+Click: add   Right-click: more", EditorStyles.centeredGreyMiniLabel);
        }

        private List<Entry> FilterEntries(Section section)
        {
            if (string.IsNullOrEmpty(_filter))
                return section.Entries;
            return section.Entries
                .Where(e => e.FullName.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0
                            || section.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void DrawSection(Section section, List<Entry> entries, float width)
        {
            if (!_foldouts.TryGetValue(section.Name, out var expanded))
                expanded = true;
            expanded = EditorGUILayout.Foldout(expanded,
                $"{section.Name}   ({section.Entries.Count} types / {section.ComponentCount})", true,
                _sectionFoldoutStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            _foldouts[section.Name] = expanded;
            if (!expanded)
                return;

            // Flow layout: pack tags into rows that fit the window width.
            var rows = new List<List<(Entry entry, GUIContent content, GUIStyle style, float width)>>();
            var row = new List<(Entry, GUIContent, GUIStyle, float)>();
            var x = 0f;
            foreach (var entry in entries)
            {
                var style = GetTagStyle();
                var content = new GUIContent($"{entry.Name}  {entry.ComponentCount}");
                var w = style.CalcSize(content).x + LeadingWidth(entry);
                if (row.Count > 0 && x + w > width)
                {
                    rows.Add(row);
                    row = new List<(Entry, GUIContent, GUIStyle, float)>();
                    x = 0f;
                }
                row.Add((entry, content, style, w));
                x += w + TagSpacing;
            }
            if (row.Count > 0)
                rows.Add(row);

            foreach (var r in rows)
            {
                var rowHeight = TagHeight;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    foreach (var (entry, content, style, w) in r)
                    {
                        var rect = GUILayoutUtility.GetRect(w, rowHeight, GUILayout.Width(w), GUILayout.Height(rowHeight));
                        DrawTag(rect, entry, content, style);
                        GUILayout.Space(TagSpacing);
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(2f);
            }
            GUILayout.Space(4f);
        }

        private void DrawTag(Rect rect, Entry entry, GUIContent content, GUIStyle style)
        {
            var activeIndex = _activeKeys.IndexOf(entry.Key);
            var isActive = activeIndex >= 0;
            var e = Event.current;

            if (e.type == EventType.Repaint)
            {
                Color bg;
                if (isActive)
                    bg = _activeColors[activeIndex];
                else
                    bg = EditorGUIUtility.isProSkin ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.80f, 0.80f, 0.80f);
                if (rect.Contains(e.mousePosition))
                    bg = Color.Lerp(bg, Color.white, 0.15f);
                // Pill: fully rounded ends.
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bg, 0f, rect.height * 0.5f);

                var contentRect = rect;
                if (entry.Icon != null)
                {
                    var iconRect = new Rect(rect.x + 7f, rect.center.y - TagIconSize * 0.5f, TagIconSize, TagIconSize);
                    GUI.DrawTexture(iconRect, entry.Icon, ScaleMode.ScaleToFit);
                }
                contentRect.xMin += LeadingWidth(entry);
                var prevColor = style.normal.textColor;
                if (isActive)
                    style.normal.textColor = IsDark(bg) ? Color.white : Color.black;
                style.Draw(contentRect, content, false, false, false, false);
                style.normal.textColor = prevColor;
            }

            GUI.Label(rect, new GUIContent(string.Empty,
                $"{entry.FullName}\n{entry.ComponentCount} components on {entry.GameObjects.Count} GameObjects"));

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    OnTagClicked(entry, e.shift || EditorGUI.actionKey);
                    e.Use();
                }
                else if (e.button == 1)
                {
                    ShowTagMenu(entry);
                    e.Use();
                }
            }
        }

        private void OnTagClicked(Entry entry, bool additive)
        {
            var index = _activeKeys.IndexOf(entry.Key);
            if (additive)
            {
                if (index >= 0)
                    RemoveActiveAt(index);
                else
                    AddActive(entry.Key);
            }
            else if (index >= 0 && _activeKeys.Count == 1)
            {
                ClearActive();
                return;
            }
            else
            {
                _activeKeys.Clear();
                _activeColors.Clear();
                AddActive(entry.Key);
            }
            ApplyHighlight();
        }

        private void ShowTagMenu(Entry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Select {entry.GameObjects.Count} GameObjects"), false, () =>
            {
                Selection.objects = entry.GameObjects.Where(go => go != null).Cast<UnityEngine.Object>().ToArray();
            });
            if (entry.GameObjects.Count > 0)
                menu.AddItem(new GUIContent("Ping First GameObject"), false, () => EditorGUIUtility.PingObject(entry.GameObjects[0]));
            var script = entry.FirstComponent is MonoBehaviour mb ? MonoScript.FromMonoBehaviour(mb) : null;
            if (script != null)
                menu.AddItem(new GUIContent("Edit Script"), false, () => AssetDatabase.OpenAsset(script));
            else
                menu.AddDisabledItem(new GUIContent("Edit Script"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy Type Name"), false, () => EditorGUIUtility.systemCopyBuffer = entry.FullName);
            menu.ShowAsContext();
        }

        private void AddActive(string key)
        {
            _activeKeys.Add(key);
            _activeColors.Add(Palette[_nextPaletteIndex % Palette.Length]);
            _nextPaletteIndex++;
        }

        private void RemoveActiveAt(int index)
        {
            _activeKeys.RemoveAt(index);
            _activeColors.RemoveAt(index);
        }

        private void ClearActive()
        {
            _activeKeys.Clear();
            _activeColors.Clear();
            _nextPaletteIndex = 0;
            ApplyHighlight();
        }

        private void ApplyHighlight()
        {
            var items = new List<(GameObject, Color)>();
            for (var i = 0; i < _activeKeys.Count; i++)
            {
                if (!_entriesByKey.TryGetValue(_activeKeys[i], out var entry))
                    continue;
                foreach (var go in entry.GameObjects)
                    items.Add((go, _activeColors[i]));
            }
            if (items.Count == 0)
                ComponentListHighlighter.Clear();
            else
                ComponentListHighlighter.Set(items);
            Repaint();
        }

        private void Scan()
        {
            _sections.Clear();
            _entriesByKey.Clear();
            _objectCount = 0;

            var includeInactive = IncludeInactive;
            var sectionsByName = new Dictionary<string, Section>();
            var components = new List<Component>();

            foreach (var root in EnumerateRoots())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if ((go.hideFlags & HideFlags.HideInHierarchy) != 0)
                        continue;
                    if (!includeInactive && !go.activeInHierarchy)
                        continue;
                    _objectCount++;

                    go.GetComponents(components);
                    foreach (var c in components)
                    {
                        var entry = GetOrCreateEntry(c, sectionsByName);
                        entry.ComponentCount++;
                        if (entry.FirstComponent == null)
                            entry.FirstComponent = c;
                        if (entry.GameObjectIds.Add(go.GetInstanceID()))
                            entry.GameObjects.Add(go);
                    }
                }
            }

            foreach (var section in sectionsByName.Values)
            {
                section.Entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                section.ComponentCount = section.Entries.Sum(e => e.ComponentCount);
                _sections.Add(section);
            }
            // User assemblies first, UnityEngine.* last, missing scripts at the very end.
            _sections.Sort((a, b) =>
            {
                var oa = SectionOrder(a);
                var ob = SectionOrder(b);
                return oa != ob ? oa.CompareTo(ob) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static int SectionOrder(Section s)
        {
            if (s.Name == MissingScriptSection)
                return 2;
            return s.IsUnityEngine ? 1 : 0;
        }

        private Entry GetOrCreateEntry(Component c, Dictionary<string, Section> sectionsByName)
        {
            var type = c != null ? c.GetType() : null;
            var key = type != null ? type.AssemblyQualifiedName : MissingScriptKey;
            if (_entriesByKey.TryGetValue(key, out var entry))
                return entry;

            entry = new Entry
            {
                Key = key,
                Name = type != null ? type.Name : "Missing Script",
                FullName = type != null ? type.FullName : "Missing Script",
                Icon = type != null ? AssetPreview.GetMiniTypeThumbnail(type) : EditorGUIUtility.IconContent("console.warnicon.sml").image,
            };
            _entriesByKey[key] = entry;

            var sectionName = type != null ? type.Assembly.GetName().Name : MissingScriptSection;
            if (!sectionsByName.TryGetValue(sectionName, out var section))
            {
                section = new Section
                {
                    Name = sectionName,
                    IsUnityEngine = sectionName.StartsWith("UnityEngine", StringComparison.Ordinal),
                };
                sectionsByName[sectionName] = section;
            }
            section.Entries.Add(entry);
            return entry;
        }

        private IEnumerable<GameObject> EnumerateRoots()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                _scopeLabel = $"Prefab: {stage.prefabContentsRoot.name}";
                yield return stage.prefabContentsRoot;
                yield break;
            }

            var sceneNames = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                sceneNames.Add(string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name);
            }
            _scopeLabel = $"Scenes: {string.Join(", ", sceneNames)}";

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                    yield return root;
            }
        }

        // Space before the label: the icon, or just enough to clear the rounded end.
        private static float LeadingWidth(Entry entry) => entry.Icon != null ? TagIconSize + 5f : 6f;

        private GUIStyle GetTagStyle()
        {
            return _tagStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 10, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
            };
        }

        private static bool IsDark(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f < 0.5f;
    }
}
