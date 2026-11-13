using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmzkworks.muShortcutPanel.Editor
{
    /// <summary>
    /// Free-layout panel of shortcuts (Project assets / URLs) and labels, stored in a <c>*.shortcutpanel.json</c> file.
    /// Use mode: click to select in the Project window / open in the browser.
    /// Edit mode: drag to move, drag the bottom-right corner to resize, drag on empty space to box select,
    /// edit properties at the bottom.
    /// </summary>
    internal sealed class ShortcutPanelWindow : EditorWindow
    {
        private const string WindowTitle = "ShortcutPanel";
        private const string LastFilePrefKey = "Mmzkworks.muShortcutPanel.LastFile";
        private const string ShowFullPathPrefKey = "Mmzkworks.muShortcutPanel.ShowFullPath";
        private const string DragGenericDataKey = "Mmzkworks.muShortcutPanel.Item";
        private const double SaveDelaySeconds = 0.5;
        private const float ContentMargin = 64f;
        private const float DragOutThreshold = 6f;


        [SerializeField] private string _filePath = "";
        [SerializeField] private string _fileGuid = "";
        [SerializeField] private bool _editMode;
        [SerializeField] private Vector2 _scroll;
        [SerializeField] private List<string> _selectedIds = new List<string>();

        private ShortcutPanelDocument _doc;
        private DateTime _loadedWriteTime;
        private bool _dirty;
        private double _saveDueTime;

        private enum PointerOp
        {
            None,
            Move,
            Resize,
            Press,
            Box
        }

        private PointerOp _op;
        private string _opItemId;
        private Vector2 _opStartMouse;
        private Rect _opStartRect;
        private readonly Dictionary<string, Rect> _opStartRects = new Dictionary<string, Rect>();
        private List<string> _boxBase = new List<string>();
        private Vector2 _boxCurrent;
        private Rect _lastCanvasRect;
        private bool _pendingMenuPicker;
        private Vector2 _pendingMenuPickerPos;
        private string _hoverId;
        private bool _showFullPath = true;
        private GUIStyle _fullPathStyle;
        private Texture2D _gridDotTexture;
        private bool _gridDotTextureIsPro;
        private Vector2 _lastCanvasMouse = new Vector2(16f, 16f);

        private GUIStyle _itemLabelStyle;
        private GUIStyle _itemSubStyle;
        private GUIStyle _labelTextStyle;
        private GUIStyle _tileLabelStyle;

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        // ------------------------------------------------------------------ open / lifecycle

        [MenuItem("Tools/muShortcutPanel/ShortcutPanel", false, 2040)]
        private static void OpenFromMenu() => Open(null);

        public static ShortcutPanelWindow Open(string panelPath)
        {
            var window = GetWindow<ShortcutPanelWindow>();
            window.ApplyTitle();
            if (!string.IsNullOrEmpty(panelPath)) window.LoadFile(panelPath);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            ApplyTitle();
            wantsMouseMove = true;

            if (string.IsNullOrEmpty(_filePath))
                _filePath = EditorPrefs.GetString(LastFilePrefKey, "");

            if (!string.IsNullOrEmpty(_filePath))
                LoadFile(_filePath, silent: true);

            _showFullPath = EditorPrefs.GetBool(ShowFullPathPrefKey, true);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Flush();
            if (_gridDotTexture != null)
            {
                DestroyImmediate(_gridDotTexture);
                _gridDotTexture = null;
            }
        }

        private void OnFocus() => ReloadIfChangedExternally();

        private void OnProjectChange()
        {
            if (string.IsNullOrEmpty(_fileGuid)) return;

            // Follow renames / moves of the panel file itself.
            var current = AssetDatabase.GUIDToAssetPath(_fileGuid);
            if (!string.IsNullOrEmpty(current) && current != _filePath && File.Exists(FullPath(current)))
            {
                _filePath = current;
                EditorPrefs.SetString(LastFilePrefKey, _filePath);
                ApplyTitle();
            }
            else if (!File.Exists(FullPath(_filePath)))
            {
                _doc = null;
                _dirty = false;
            }

            Repaint();
        }

        private void ApplyTitle()
        {
            var name = _doc != null ? ShortcutPanelFiles.DisplayName(_filePath) : "";
            titleContent = new GUIContent(WindowTitle, string.IsNullOrEmpty(name) ? WindowTitle : $"{WindowTitle} - {name}");
        }

        // ------------------------------------------------------------------ file IO

        private static string FullPath(string projectRelative) =>
            string.IsNullOrEmpty(projectRelative) ? "" : Path.Combine(ProjectRoot, projectRelative);

        internal void LoadFile(string path, bool silent = false)
        {
            Flush();

            var full = FullPath(path);
            if (!File.Exists(full))
            {
                if (!silent) Debug.LogWarning($"[muShortcutPanel] Panel file not found: {path}");
                _doc = null;
                ApplyTitle();
                Repaint();
                return;
            }

            _filePath = path;
            _fileGuid = AssetDatabase.AssetPathToGUID(path);
            _doc = ShortcutPanelSerializer.FromJson(File.ReadAllText(full, Encoding.UTF8));
            _loadedWriteTime = File.GetLastWriteTimeUtc(full);
            _dirty = false;
            _op = PointerOp.None;
            PruneSelection();

            EditorPrefs.SetString(LastFilePrefKey, _filePath);
            ApplyTitle();
            Repaint();
        }

        private void ReloadIfChangedExternally()
        {
            if (_doc == null || _dirty || string.IsNullOrEmpty(_filePath)) return;
            var full = FullPath(_filePath);
            if (File.Exists(full) && File.GetLastWriteTimeUtc(full) != _loadedWriteTime)
                LoadFile(_filePath, silent: true);
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveDueTime = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            Repaint();
        }

        private void OnEditorUpdate()
        {
            // Don't write while the user is still dragging; the drag ends with MarkDirty anyway.
            if (_dirty && _op == PointerOp.None && EditorApplication.timeSinceStartup >= _saveDueTime)
                Flush();
        }

        private void Flush()
        {
            if (!_dirty || _doc == null || string.IsNullOrEmpty(_filePath)) return;
            _dirty = false;

            var full = FullPath(_filePath);
            try
            {
                File.WriteAllText(full, ShortcutPanelSerializer.ToJson(_doc), new UTF8Encoding(false));
                _loadedWriteTime = File.GetLastWriteTimeUtc(full);
                AssetDatabase.ImportAsset(_filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[muShortcutPanel] Failed to save {_filePath}: {e.Message}");
            }
        }

        private void CreateNewPanel()
        {
            var dir = string.IsNullOrEmpty(_filePath) ? "Assets" : Path.GetDirectoryName(_filePath)?.Replace('\\', '/');
            var path = EditorUtility.SaveFilePanelInProject("New Shortcut Panel", "NewPanel.shortcutpanel", "json",
                "Choose where to save the shortcut panel.", dir);
            if (string.IsNullOrEmpty(path)) return;

            path = ShortcutPanelFiles.EnsureSuffix(path);
            var doc = File.Exists(FullPath(path))
                ? null // keep existing content, just open it
                : new ShortcutPanelDocument();

            if (doc != null)
            {
                File.WriteAllText(FullPath(path), ShortcutPanelSerializer.ToJson(doc), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path);
            }

            LoadFile(path);
        }

        // ------------------------------------------------------------------ GUI

        private void EnsureStyles()
        {
            if (_itemLabelStyle != null) return;

            _itemLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            _itemSubStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _tileLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                clipping = TextClipping.Clip,
                wordWrap = true
            };
            _labelTextStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = true,
                richText = false
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (_doc == null)
            {
                DrawNoFile();
                return;
            }

            var selected = SingleSelected();
            var multiCount = _editMode && selected == null ? _selectedIds.Count : 0;
            var canvasRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCanvas(canvasRect);

            if (selected != null)
                DrawPropertyPanel(selected);
            else if (multiCount > 1)
                EditorGUILayout.HelpBox($"{multiCount} items selected. Drag to move them together, Delete to remove.", MessageType.None);

            // Pickers requested from a context menu are opened here, where a GUI context exists.
            if (_pendingMenuPicker && Event.current.type == EventType.Layout)
            {
                _pendingMenuPicker = false;
                var canvasPos = _pendingMenuPickerPos;
                var windowPos = canvasPos - _scroll + _lastCanvasRect.position;
                MenuCommandDropdown.Show(new Rect(windowPos, new Vector2(1f, 1f)),
                    path => AddItem(NewMenuItem(path, canvasPos)));
            }
        }

        // ------------------------------------------------------------------ selection

        private bool IsSelected(string id) => !string.IsNullOrEmpty(id) && _selectedIds.Contains(id);

        private void SelectOnly(string id)
        {
            _selectedIds.Clear();
            if (!string.IsNullOrEmpty(id)) _selectedIds.Add(id);
        }

        /// <summary>Selected items in draw order.</summary>
        private List<ShortcutPanelItem> SelectedItems() => _doc.items.Where(i => _selectedIds.Contains(i.id)).ToList();

        /// <summary>The selected item when exactly one is selected (edit mode only).</summary>
        private ShortcutPanelItem SingleSelected() =>
            _editMode && _doc != null && _selectedIds.Count == 1 ? _doc.Find(_selectedIds[0]) : null;

        private void PruneSelection()
        {
            if (_doc == null) _selectedIds.Clear();
            else _selectedIds.RemoveAll(id => _doc.Find(id) == null);
        }

        /// <summary>The selection when <paramref name="hit"/> is part of it, otherwise just <paramref name="hit"/>.</summary>
        private List<ShortcutPanelItem> TargetsOf(ShortcutPanelItem hit) =>
            IsSelected(hit.id) && _selectedIds.Count > 1 ? SelectedItems() : new List<ShortcutPanelItem> { hit };

        private void SelectAll()
        {
            _selectedIds.Clear();
            _selectedIds.AddRange(_doc.items.Select(i => i.id));
            Repaint();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var fileLabel = _doc != null ? ShortcutPanelFiles.DisplayName(_filePath) : "(no panel)";
                var fileContent = new GUIContent(fileLabel, _doc != null ? _filePath : "Select a .shortcutpanel.json");
                var fileRect = GUILayoutUtility.GetRect(fileContent, EditorStyles.toolbarDropDown, GUILayout.MinWidth(120f), GUILayout.MaxWidth(260f));
                if (EditorGUI.DropdownButton(fileRect, fileContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    ShowFileMenu(fileRect);

                GUILayout.FlexibleSpace();

                if (_doc != null && _editMode)
                {
                    if (GUILayout.Button(new GUIContent("+ Label", "Add a text label"), EditorStyles.toolbarButton))
                        AddLabel(NextFreePosition());
                    if (GUILayout.Button(new GUIContent("+ URL", "Add a URL shortcut (uses the clipboard if it contains a URL)"), EditorStyles.toolbarButton))
                        AddUrlFromClipboardOrBlank(NextFreePosition());
                    var menuContent = new GUIContent("+ Menu", "Add a menu command shortcut (Editor menu / Project window / Hierarchy window)");
                    var menuRect = GUILayoutUtility.GetRect(menuContent, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(menuRect, menuContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var pos = NextFreePosition();
                        MenuCommandDropdown.Show(menuRect, path => AddItem(NewMenuItem(path, pos)));
                    }
                    GUILayout.Space(8f);
                }

                var newShowPath = GUILayout.Toggle(_showFullPath, new GUIContent("Path",
                    "Show the full path / URL / menu path under each shortcut"), EditorStyles.toolbarButton);
                if (newShowPath != _showFullPath)
                {
                    _showFullPath = newShowPath;
                    EditorPrefs.SetBool(ShowFullPathPrefKey, _showFullPath);
                }

                using (new EditorGUI.DisabledScope(_doc == null))
                {
                    var newEdit = GUILayout.Toggle(_editMode, new GUIContent(_editMode ? "Edit Mode" : "Use Mode",
                        "Toggle between using shortcuts and editing the layout"), EditorStyles.toolbarButton, GUILayout.Width(80f));
                    if (newEdit != _editMode)
                    {
                        _editMode = newEdit;
                        if (_op != PointerOp.None) EndOp();
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void ShowFileMenu(Rect rect)
        {
            var menu = new GenericMenu();
            var files = ShortcutPanelFiles.FindAll(ProjectRoot);

            if (files.Count == 0)
                menu.AddDisabledItem(new GUIContent("No .shortcutpanel.json in Assets"));

            foreach (var f in files)
            {
                var path = f;
                var dir = Path.GetDirectoryName(f)?.Replace('/', '\\');
                // '/' would create submenus in GenericMenu, so show the folder with backslashes.
                var label = $"{ShortcutPanelFiles.DisplayName(f)}    ({dir})";
                menu.AddItem(new GUIContent(label), _doc != null && path == _filePath, () => LoadFile(path));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("New Panel..."), false, CreateNewPanel);
            if (_doc != null)
            {
                menu.AddItem(new GUIContent("Reload"), false, () => LoadFile(_filePath));
                menu.AddItem(new GUIContent("Show Panel File in Project"), false, () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(_filePath);
                    if (asset != null) SelectInProject(asset);
                });
            }

            menu.DropDown(rect);
        }

        private void DrawNoFile()
        {
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "No shortcut panel is open.\nPick a *.shortcutpanel.json from the dropdown above, or create a new one.",
                MessageType.Info);
            if (GUILayout.Button("New Panel...", GUILayout.Height(24f)))
                CreateNewPanel();
        }

        // ------------------------------------------------------------------ canvas

        private void DrawCanvas(Rect canvasRect)
        {
            var e = Event.current;
            var items = _doc.items;
            _lastCanvasRect = canvasRect;

            if (e.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.78f, 0.78f, 0.78f);
                EditorGUI.DrawRect(canvasRect, bg);
            }

            var contentSize = ShortcutPanelLayout.ContentSize(items, ContentMargin);
            var viewRect = new Rect(0f, 0f,
                Mathf.Max(contentSize.x, canvasRect.width - 16f),
                Mathf.Max(contentSize.y, canvasRect.height - 16f));

            _scroll = GUI.BeginScrollView(canvasRect, _scroll, viewRect);
            {
                if (e.type == EventType.Repaint)
                {
                    if (_editMode) DrawGrid(viewRect);
                    foreach (var item in items) DrawItem(item);
                    if (_showFullPath)
                    {
                        foreach (var item in items) DrawFullPathLabel(item);
                    }
                    if (_op == PointerOp.Box) DrawSelectionBox();
                }

                // Invisible labels give us hover tooltips (path / URL).
                if (!_editMode)
                {
                    foreach (var item in items)
                    {
                        var tip = Tooltip(item);
                        if (!string.IsNullOrEmpty(tip)) GUI.Label(item.Rect, new GUIContent("", tip), GUIStyle.none);
                    }
                }
            }
            GUI.EndScrollView();

            HandleDragAndDrop(canvasRect);
            HandlePointer(canvasRect);
            HandleKeyboard();
        }

        private Vector2 ToCanvas(Rect canvasRect, Vector2 windowPos) => windowPos - canvasRect.position + _scroll;

        /// <summary>Dot grid: a small dot at every snap point (8px).</summary>
        private void DrawGrid(Rect viewRect)
        {
            var tex = GetGridDotTexture();
            var tile = tex.width; // = 1 grid cell
            // Round the size up to whole tiles so the pattern starts exactly at the top-left (the scroll view clips the rest).
            var cols = Mathf.Ceil(viewRect.width / tile);
            var rows = Mathf.Ceil(viewRect.height / tile);
            GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, cols * tile, rows * tile), tex, new Rect(0f, 0f, cols, rows));
        }

        private Texture2D GetGridDotTexture()
        {
            var pro = EditorGUIUtility.isProSkin;
            if (_gridDotTexture != null && _gridDotTextureIsPro == pro) return _gridDotTexture;

            if (_gridDotTexture != null) DestroyImmediate(_gridDotTexture);

            var cell = (int)ShortcutPanelLayout.GridSize;
            var size = cell;
            var dot = pro ? new Color(1f, 1f, 1f, 0.16f) : new Color(0f, 0f, 0f, 0.18f);

            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // One dot per grid cell. Texture rows go bottom-up, so the GUI top-left is (x = 0, y = size - 1).
            pixels[(size - 1) * size] = dot;

            _gridDotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _gridDotTexture.SetPixels(pixels);
            _gridDotTexture.Apply();
            _gridDotTextureIsPro = pro;
            return _gridDotTexture;
        }

        private void DrawSelectionBox()
        {
            var box = ShortcutPanelLayout.RectFromPoints(_opStartMouse, _boxCurrent);
            EditorGUI.DrawRect(box, new Color(0.3f, 0.65f, 1f, 0.15f));
            DrawOutline(box, new Color(0.3f, 0.65f, 1f, 0.9f), 1f);
        }

        private void DrawItem(ShortcutPanelItem item)
        {
            var r = item.Rect;
            var isSelected = _editMode && IsSelected(item.id);
            var isHover = item.id == _hoverId;

            switch (item.kind)
            {
                case ShortcutItemKind.Label:
                    DrawLabelItem(item, r);
                    break;
                default:
                    DrawShortcutItem(item, r, isHover && !_editMode);
                    break;
            }

            if (isSelected)
            {
                DrawRoundedOutline(r, new Color(0.3f, 0.65f, 1f, 1f), 2f);
            }
            else if (_editMode)
            {
                DrawRoundedOutline(r, new Color(0.5f, 0.5f, 0.5f, 0.6f), 1f);
            }

            if (_editMode)
            {
                var h = ShortcutPanelLayout.ResizeHandleRect(r);
                var hc = isSelected ? new Color(0.3f, 0.65f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);
                // Small stepped triangle in the corner.
                EditorGUI.DrawRect(new Rect(h.xMax - 3f, h.y + 2f, 2f, h.height - 3f), hc);
                EditorGUI.DrawRect(new Rect(h.x + 2f, h.yMax - 3f, h.width - 3f, 2f), hc);
                EditorGUI.DrawRect(new Rect(h.xMax - 6f, h.yMax - 6f, 3f, 3f), hc);
            }
        }

        private void DrawLabelItem(ShortcutPanelItem item, Rect r)
        {
            var pro = EditorGUIUtility.isProSkin;
            DrawRoundedRect(r, pro ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.72f, 0.72f, 0.72f));
            var style = _labelTextStyle;
            style.fontSize = item.fontSize > 0 ? item.fontSize : 0;
            style.normal.textColor = pro ? new Color(0.9f, 0.9f, 0.9f) : Color.black;
            var text = string.IsNullOrEmpty(item.title) && _editMode ? "(empty label)" : item.title;
            style.Draw(Inset(r, 6f, 2f), new GUIContent(text), false, false, false, false);
        }

        private void DrawShortcutItem(ShortcutPanelItem item, Rect r, bool hover)
        {
            var pro = EditorGUIUtility.isProSkin;
            var bg = pro ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.90f, 0.90f, 0.90f);
            if (hover) bg = pro ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.97f, 0.97f, 0.97f);
            DrawRoundedRect(r, bg);

            var name = DisplayName(item, out var missing);
            var icon = Icon(item, missing);
            var textColor = missing ? new Color(0.9f, 0.35f, 0.3f) : (pro ? new Color(0.85f, 0.85f, 0.85f) : Color.black);

            var labelStyle = _itemLabelStyle;
            labelStyle.fontSize = item.fontSize > 0 ? item.fontSize : 0;
            labelStyle.normal.textColor = textColor;

            var inner = Inset(r, 4f, 2f);
            var tile = r.height >= 56f && r.width >= 48f;

            if (tile)
            {
                // Big icon on top, name below (Project window icon view style).
                var lineH = Mathf.Max(14f, (item.fontSize > 0 ? item.fontSize : 11f) + 4f);
                var iconSize = Mathf.Min(inner.width, inner.height - lineH * 2f);
                iconSize = Mathf.Max(16f, iconSize);
                var iconRect = new Rect(inner.center.x - iconSize * 0.5f, inner.y + 2f, iconSize, iconSize);
                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

                _tileLabelStyle.fontSize = item.fontSize > 0 ? item.fontSize : 0;
                _tileLabelStyle.normal.textColor = textColor;
                var textRect = new Rect(inner.x, iconRect.yMax + 2f, inner.width, inner.yMax - iconRect.yMax - 2f);
                _tileLabelStyle.Draw(textRect, new GUIContent(name), false, false, false, false);
            }
            else
            {
                var iconSize = Mathf.Min(16f, inner.height);
                var x = inner.x;
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(x, inner.center.y - iconSize * 0.5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit);
                    x += iconSize + 4f;
                }

                var textRect = new Rect(x, inner.y, inner.xMax - x, inner.height);
                // Second line (host / folder) when there is enough height.
                if (inner.height >= 34f)
                {
                    var half = inner.height * 0.5f;
                    labelStyle.Draw(new Rect(textRect.x, textRect.y, textRect.width, half), new GUIContent(name), false, false, false, false);
                    _itemSubStyle.normal.textColor = pro ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.35f, 0.35f, 0.35f);
                    _itemSubStyle.Draw(new Rect(textRect.x, textRect.y + half, textRect.width, half),
                        new GUIContent(SubText(item)), false, false, false, false);
                }
                else
                {
                    labelStyle.Draw(textRect, new GUIContent(name), false, false, false, false);
                }
            }
        }

        /// <summary>Corner radius of item frames (a gentle rounding; clamped for very small items).</summary>
        private const float ItemCornerRadius = 4f;

        private static float CornerRadius(Rect r) => Mathf.Min(ItemCornerRadius, Mathf.Min(r.width, r.height) * 0.5f);

        private static void DrawRoundedRect(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, CornerRadius(r));
        }

        private static void DrawRoundedOutline(Rect r, Color c, float t)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, t, CornerRadius(r));
        }

        private static void DrawOutline(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static Rect Inset(Rect r, float h, float v) =>
            new Rect(r.x + h, r.y + v, Mathf.Max(0f, r.width - h * 2f), Mathf.Max(0f, r.height - v * 2f));

        // ------------------------------------------------------------------ item info

        internal static string ResolveAssetPath(ShortcutPanelItem item)
        {
            if (!string.IsNullOrEmpty(item.guid))
            {
                var p = AssetDatabase.GUIDToAssetPath(item.guid);
                if (!string.IsNullOrEmpty(p) && AssetExists(p)) return p;
            }
            return !string.IsNullOrEmpty(item.path) && AssetExists(item.path) ? item.path : null;
        }

        private static bool AssetExists(string path) =>
            AssetDatabase.IsValidFolder(path) || File.Exists(FullPath(path));

        private static string DisplayName(ShortcutPanelItem item, out bool missing)
        {
            missing = false;
            if (item.kind == ShortcutItemKind.Url)
                return !string.IsNullOrEmpty(item.title) ? item.title : ShortcutPanelDropParser.DefaultUrlTitle(item.url);
            if (item.kind == ShortcutItemKind.Menu)
                return !string.IsNullOrEmpty(item.title) ? item.title : ShortcutPanelMenuPath.DefaultTitle(item.menu);

            var path = ResolveAssetPath(item);
            missing = path == null;
            if (!string.IsNullOrEmpty(item.title)) return item.title;
            var shown = path ?? item.path;
            var name = AssetDatabase.IsValidFolder(shown ?? "") ? Path.GetFileName(shown) : Path.GetFileNameWithoutExtension(shown);
            return missing ? $"{name} (missing)" : name;
        }

        private static string SubText(ShortcutPanelItem item)
        {
            if (item.kind == ShortcutItemKind.Url) return item.url;
            if (item.kind == ShortcutItemKind.Menu)
            {
                var target = ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu));
                return $"{target}: {ShortcutPanelMenuPath.ParentPath(item.menu)}";
            }
            var path = ResolveAssetPath(item) ?? item.path;
            return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
        }

        private static string Tooltip(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Url: return item.url;
                case ShortcutItemKind.Menu:
                    return $"{ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu))} command: {item.menu}";
                case ShortcutItemKind.Asset: return ResolveAssetPath(item) ?? $"Missing: {item.path}";
                default: return null;
            }
        }

        /// <summary>Asset path / URL / menu path of a shortcut, or null for labels.</summary>
        private static string FullPathText(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Asset:
                    return ResolveAssetPath(item) ?? $"(missing) {item.path}";
                case ShortcutItemKind.Url:
                    return item.url;
                case ShortcutItemKind.Menu:
                    return $"[{ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu))}] {item.menu}";
                default:
                    return null;
            }
        }

        /// <summary>Small one-line label just below the shortcut, drawn over whatever is there.</summary>
        private void DrawFullPathLabel(ShortcutPanelItem item)
        {
            var text = FullPathText(item);
            if (string.IsNullOrEmpty(text)) return;

            if (_fullPathStyle == null)
            {
                _fullPathStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    padding = new RectOffset(3, 3, 0, 0)
                };
            }

            var pro = EditorGUIUtility.isProSkin;
            _fullPathStyle.normal.textColor = pro ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.25f, 0.25f, 0.25f);

            var content = new GUIContent(text);
            var size = _fullPathStyle.CalcSize(content);
            // At least as wide as the item; longer paths may extend to the right up to a limit.
            var width = Mathf.Min(Mathf.Max(size.x, item.width), Mathf.Max(item.width, 480f));
            var r = new Rect(item.x, item.y + item.height + 1f, width, size.y);

            var bg = pro ? new Color(0.16f, 0.16f, 0.16f, 0.85f) : new Color(0.78f, 0.78f, 0.78f, 0.85f);
            EditorGUI.DrawRect(new Rect(r.x, r.y, Mathf.Min(size.x, r.width), r.height), bg);
            _fullPathStyle.Draw(r, content, false, false, false, false);
        }

        private static Texture Icon(ShortcutPanelItem item, bool missing)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Asset:
                    if (missing) return EditorGUIUtility.IconContent("console.warnicon.sml").image;
                    return AssetDatabase.GetCachedIcon(ResolveAssetPath(item));
                case ShortcutItemKind.Url:
                    return KindIcon(UrlIconNames);
                case ShortcutItemKind.Menu:
                    switch (ShortcutPanelMenuPath.TargetOf(item.menu))
                    {
                        case ShortcutMenuTarget.Project: return KindIcon(ProjectIconNames);
                        case ShortcutMenuTarget.Hierarchy: return KindIcon(HierarchyIconNames);
                        default: return KindIcon(MenuIconNames);
                    }
                default:
                    return null;
            }
        }

        // Built-in editor icons per shortcut kind. Names differ between Unity versions, so the first one that exists is used.
        private static readonly string[] UrlIconNames = { "BuildSettings.Web.Small", "BuildSettings.WebGL.Small", "CloudConnect" };
        private static readonly string[] MenuIconNames = { "_Menu", "Settings", "SettingsIcon" };
        private static readonly string[] ProjectIconNames = { "Project", "Folder Icon" };
        private static readonly string[] HierarchyIconNames = { "UnityEditor.SceneHierarchyWindow", "UnityEditor.HierarchyWindow" };

        private static readonly Dictionary<string[], Texture> KindIconCache = new Dictionary<string[], Texture>();
        private static bool _kindIconCacheIsPro;

        private static Texture KindIcon(string[] names)
        {
            var pro = EditorGUIUtility.isProSkin;
            if (_kindIconCacheIsPro != pro)
            {
                KindIconCache.Clear();
                _kindIconCacheIsPro = pro;
            }

            if (KindIconCache.TryGetValue(names, out var cached)) return cached;

            Texture found = null;
            foreach (var name in names)
            {
                // FindTexture returns null quietly for unknown names (IconContent would log an error).
                if (pro) found = EditorGUIUtility.FindTexture("d_" + name);
                if (found == null) found = EditorGUIUtility.FindTexture(name);
                if (found != null) break;
            }

            KindIconCache[names] = found;
            return found;
        }

        // ------------------------------------------------------------------ input

        private void HandlePointer(Rect canvasRect)
        {
            var e = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var local = ToCanvas(canvasRect, e.mousePosition);
            var inCanvas = canvasRect.Contains(e.mousePosition);

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseMove:
                {
                    var hover = inCanvas ? ShortcutPanelLayout.HitTest(_doc.items, local) : null;
                    var hoverId = hover?.id;
                    if (hoverId != _hoverId)
                    {
                        _hoverId = hoverId;
                        Repaint();
                    }
                    if (inCanvas)
                    {
                        _lastCanvasMouse = local;
                    }
                    break;
                }

                case EventType.MouseDown when inCanvas && e.button == 0:
                {
                    _lastCanvasMouse = local;
                    var hit = ShortcutPanelLayout.HitTest(_doc.items, local);

                    if (_editMode)
                    {
                        GUI.FocusControl(null);
                        var additive = e.shift || EditorGUI.actionKey;
                        if (hit == null)
                        {
                            // Box select (Shift / Ctrl / Cmd adds to the current selection).
                            _boxBase = additive ? new List<string>(_selectedIds) : new List<string>();
                            if (!additive) _selectedIds.Clear();
                            _op = PointerOp.Box;
                            _opItemId = null;
                            _opStartMouse = local;
                            _boxCurrent = local;
                            _opStartRects.Clear();
                            GUIUtility.hotControl = controlId;
                        }
                        else if (additive)
                        {
                            if (IsSelected(hit.id)) _selectedIds.Remove(hit.id);
                            else _selectedIds.Add(hit.id);
                        }
                        else
                        {
                            var onHandle = ShortcutPanelLayout.ResizeHandleRect(hit.Rect).Contains(local);
                            // Pressing an already selected item keeps the group so it can be moved together.
                            if (onHandle || !IsSelected(hit.id)) SelectOnly(hit.id);
                            _op = onHandle ? PointerOp.Resize : PointerOp.Move;
                            BeginOp(hit, local, controlId);
                        }
                        e.Use();
                        Repaint();
                    }
                    else if (hit != null && hit.kind != ShortcutItemKind.Label)
                    {
                        _op = PointerOp.Press;
                        BeginOp(hit, local, controlId);
                        e.Use();
                    }
                    break;
                }

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                {
                    if (_op == PointerOp.Box)
                    {
                        _boxCurrent = local;
                        UpdateBoxSelection();
                        e.Use();
                        Repaint();
                        break;
                    }

                    var item = _doc.Find(_opItemId);
                    if (item == null)
                    {
                        EndOp();
                        break;
                    }

                    var delta = local - _opStartMouse;
                    switch (_op)
                    {
                        case PointerOp.Move:
                            MoveGroup(delta);
                            break;
                        case PointerOp.Resize:
                            item.Rect = ShortcutPanelLayout.Resized(_opStartRect, delta);
                            break;
                        case PointerOp.Press when delta.magnitude > DragOutThreshold && item.kind != ShortcutItemKind.Menu:
                            // Drag a shortcut out of the panel (e.g. into the Inspector or the Hierarchy).
                            StartDragOut(item);
                            EndOp();
                            break;
                    }
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                {
                    var item = _doc.Find(_opItemId);
                    var op = _op;
                    EndOp();
                    e.Use();

                    if (op == PointerOp.Box)
                    {
                        Repaint();
                        break;
                    }
                    if (item == null) break;
                    if (op == PointerOp.Move || op == PointerOp.Resize)
                    {
                        var changed = _opStartRects.Any(kv => _doc.Find(kv.Key) is { } it && it.Rect != kv.Value);
                        if (changed) MarkDirty();
                        // A plain click (no drag) on a member of a group selection narrows it to that item.
                        else if (op == PointerOp.Move && _selectedIds.Count > 1) SelectOnly(item.id);
                    }
                    else if (op == PointerOp.Press && item.Rect.Contains(local))
                    {
                        Activate(item);
                    }
                    Repaint();
                    break;
                }

                case EventType.ContextClick when inCanvas:
                {
                    _lastCanvasMouse = local;
                    var hit = ShortcutPanelLayout.HitTest(_doc.items, local);
                    if (hit != null && _editMode && !IsSelected(hit.id)) SelectOnly(hit.id);
                    ShowContextMenu(hit, local);
                    e.Use();
                    break;
                }
            }
        }

        private void BeginOp(ShortcutPanelItem item, Vector2 local, int controlId)
        {
            _opItemId = item.id;
            _opStartMouse = local;
            _opStartRect = item.Rect;
            _opStartRects.Clear();
            _opStartRects[item.id] = item.Rect;
            if (_op == PointerOp.Move)
            {
                foreach (var selected in SelectedItems()) _opStartRects[selected.id] = selected.Rect;
            }
            GUIUtility.hotControl = controlId;
        }

        private void MoveGroup(Vector2 delta)
        {
            var offset = ShortcutPanelLayout.GroupMoveOffset(_opStartRect, _opStartRects.Values, delta);
            foreach (var kv in _opStartRects)
            {
                var item = _doc.Find(kv.Key);
                if (item != null) item.Rect = new Rect(kv.Value.position + offset, kv.Value.size);
            }
        }

        private void UpdateBoxSelection()
        {
            var box = ShortcutPanelLayout.RectFromPoints(_opStartMouse, _boxCurrent);
            _selectedIds.Clear();
            _selectedIds.AddRange(_boxBase);
            foreach (var item in _doc.items)
            {
                if (item.Rect.Overlaps(box) && !_selectedIds.Contains(item.id)) _selectedIds.Add(item.id);
            }
        }

        private void EndOp()
        {
            _op = PointerOp.None;
            GUIUtility.hotControl = 0;
        }

        // Delete / Paste arrive as Validate/ExecuteCommand events (Delete key, Cmd+Backspace, Ctrl/Cmd+V).
        private void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return;
            if (EditorGUIUtility.editingTextField) return;

            var execute = e.type == EventType.ExecuteCommand;
            switch (e.commandName)
            {
                case "Delete":
                case "SoftDelete":
                {
                    if (!_editMode || _selectedIds.Count == 0) return;
                    if (execute) RemoveItems(SelectedItems());
                    e.Use();
                    break;
                }
                case "Paste":
                {
                    var clip = EditorGUIUtility.systemCopyBuffer;
                    var pos = ShortcutPanelLayout.Snap(_lastCanvasMouse);
                    if (ShortcutPanelDropParser.TryGetWebUrl(clip, out var url))
                    {
                        if (execute) AddItem(NewUrlItem(url, pos));
                    }
                    else if (_editMode && ShortcutPanelMenuPath.TryParse(clip, out var menuPath)
                                       && ShortcutPanelMenuCatalog.Exists(menuPath))
                    {
                        // e.g. "Window/General/Console", "Assets/Create/Folder", "GameObject/Create Empty"
                        if (execute) AddItem(NewMenuItem(menuPath, pos));
                    }
                    else
                    {
                        return;
                    }
                    e.Use();
                    break;
                }
                case "Duplicate":
                {
                    if (!_editMode || _selectedIds.Count == 0) return;
                    if (execute) DuplicateItems(SelectedItems());
                    e.Use();
                    break;
                }
                case "SelectAll":
                {
                    if (!_editMode) return;
                    if (execute) SelectAll();
                    e.Use();
                    break;
                }
            }
        }

        private void HandleDragAndDrop(Rect canvasRect)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!canvasRect.Contains(e.mousePosition)) return;

            // Ignore our own drag-out coming back onto the panel.
            if (DragAndDrop.GetGenericData(DragGenericDataKey) != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                e.Use();
                return;
            }

            var entries = CollectDropEntries();
            if (entries.Count == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var pos = ShortcutPanelLayout.Snap(ToCanvas(canvasRect, e.mousePosition));
                AddDropped(entries, pos);
            }
            e.Use();
        }

        private static List<DropEntry> CollectDropEntries()
        {
            var objectPaths = (DragAndDrop.objectReferences ?? Array.Empty<Object>())
                .Where(o => o != null && EditorUtility.IsPersistent(o))
                .Select(AssetDatabase.GetAssetPath);

            return ShortcutPanelDropParser.Collect(
                objectPaths,
                DragAndDrop.paths,
                p => (p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal) || p == "Assets")
                     && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p)),
                p =>
                {
                    try
                    {
                        var fi = new FileInfo(p);
                        return fi.Exists && fi.Length < 64 * 1024 ? File.ReadAllText(p) : null;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
        }

        // ------------------------------------------------------------------ actions

        private void Activate(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Asset:
                {
                    var path = ResolveAssetPath(item);
                    var obj = path != null ? AssetDatabase.LoadAssetAtPath<Object>(path) : null;
                    if (obj == null)
                    {
                        ShowNotification(new GUIContent($"Missing: {item.path}"));
                        return;
                    }
                    SelectInProject(obj);
                    break;
                }
                case ShortcutItemKind.Url:
                    if (ShortcutPanelDropParser.TryGetWebUrl(item.url, out var url))
                        Application.OpenURL(url);
                    else
                        ShowNotification(new GUIContent("Invalid URL"));
                    break;
                case ShortcutItemKind.Menu:
                    ExecuteMenuCommand(item.menu);
                    break;
            }
        }

        private void ExecuteMenuCommand(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath))
            {
                ShowNotification(new GUIContent("No menu command set"));
                return;
            }

            // Run outside OnGUI: menu commands may open dialogs or change the window layout.
            EditorApplication.delayCall += () =>
            {
                // Project / Hierarchy commands act on that window's selection, so bring the window to the front first.
                switch (ShortcutPanelMenuPath.TargetOf(menuPath))
                {
                    case ShortcutMenuTarget.Project:
                        EditorUtility.FocusProjectWindow();
                        break;
                    case ShortcutMenuTarget.Hierarchy:
                        var hierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                        if (hierarchyType != null) EditorWindow.FocusWindowIfItsOpen(hierarchyType);
                        break;
                }

                if (!EditorApplication.ExecuteMenuItem(menuPath) && this != null)
                    ShowNotification(new GUIContent($"Menu command not available: {menuPath}"));
            };
        }

        private static void SelectInProject(Object obj)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        private void StartDragOut(ShortcutPanelItem item)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(DragGenericDataKey, item.id);

            if (item.kind == ShortcutItemKind.Asset)
            {
                var path = ResolveAssetPath(item);
                var obj = path != null ? AssetDatabase.LoadAssetAtPath<Object>(path) : null;
                if (obj == null) return;
                DragAndDrop.objectReferences = new[] { obj };
                DragAndDrop.paths = new[] { path };
            }
            else if (item.kind == ShortcutItemKind.Url)
            {
                DragAndDrop.objectReferences = Array.Empty<Object>();
                DragAndDrop.paths = new[] { item.url };
            }
            else
            {
                return;
            }

            DragAndDrop.StartDrag(DisplayName(item, out _));
        }

        private void ShowContextMenu(ShortcutPanelItem hit, Vector2 local)
        {
            var menu = new GenericMenu();
            var pos = ShortcutPanelLayout.Snap(local);

            if (hit != null && hit.kind != ShortcutItemKind.Label)
            {
                var activateLabel = hit.kind == ShortcutItemKind.Url ? "Open URL"
                    : hit.kind == ShortcutItemKind.Menu ? "Execute Command"
                    : "Select in Project";
                menu.AddItem(new GUIContent(activateLabel), false, () => Activate(hit));
                if (hit.kind == ShortcutItemKind.Asset)
                {
                    menu.AddItem(new GUIContent("Open Asset"), false, () =>
                    {
                        var p = ResolveAssetPath(hit);
                        var obj = p != null ? AssetDatabase.LoadAssetAtPath<Object>(p) : null;
                        if (obj != null) AssetDatabase.OpenAsset(obj);
                    });
                    menu.AddItem(new GUIContent("Reveal in Finder/Explorer"), false, () =>
                    {
                        var p = ResolveAssetPath(hit);
                        if (p != null) EditorUtility.RevealInFinder(p);
                    });
                }
                switch (hit.kind)
                {
                    case ShortcutItemKind.Url:
                        menu.AddItem(new GUIContent("Copy URL"), false, () => EditorGUIUtility.systemCopyBuffer = hit.url);
                        break;
                    case ShortcutItemKind.Menu:
                        menu.AddItem(new GUIContent("Copy Menu Path"), false, () => EditorGUIUtility.systemCopyBuffer = hit.menu);
                        break;
                    default:
                        menu.AddItem(new GUIContent("Copy Path"), false, () => EditorGUIUtility.systemCopyBuffer = ResolveAssetPath(hit) ?? hit.path);
                        break;
                }
            }

            if (_editMode)
            {
                if (hit != null)
                {
                    var targets = TargetsOf(hit);
                    var suffix = targets.Count > 1 ? $" ({targets.Count} items)" : "";
                    if (menu.GetItemCount() > 0) menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Duplicate" + suffix), false, () => DuplicateItems(targets));
                    menu.AddItem(new GUIContent("Bring to Front" + suffix), false, () => Reorder(targets, toFront: true));
                    menu.AddItem(new GUIContent("Send to Back" + suffix), false, () => Reorder(targets, toFront: false));
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Delete" + suffix), false, () => RemoveItems(targets));
                }
                else
                {
                    menu.AddItem(new GUIContent("Add Label Here"), false, () => AddLabel(pos));
                    menu.AddItem(new GUIContent("Add URL Here"), false, () => AddUrlFromClipboardOrBlank(pos));
                    menu.AddItem(new GUIContent("Add Menu Command Here..."), false, () =>
                    {
                        _pendingMenuPicker = true;
                        _pendingMenuPickerPos = pos;
                        Repaint();
                    });
                    if (_doc.items.Count > 0)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Select All"), false, SelectAll);
                    }
                }
            }
            else if (hit == null)
            {
                if (ShortcutPanelDropParser.TryGetWebUrl(EditorGUIUtility.systemCopyBuffer, out var clipUrl))
                    menu.AddItem(new GUIContent("Paste URL"), false, () => AddItem(NewUrlItem(clipUrl, pos)));
                menu.AddItem(new GUIContent("Switch to Edit Mode"), false, () => _editMode = true);
            }

            if (menu.GetItemCount() > 0) menu.ShowAsContext();
        }

        private void Reorder(List<ShortcutPanelItem> targets, bool toFront)
        {
            // Keep the relative draw order of the moved items.
            var ordered = _doc.items.Where(targets.Contains).ToList();
            _doc.items.RemoveAll(ordered.Contains);
            if (toFront) _doc.items.AddRange(ordered);
            else _doc.items.InsertRange(0, ordered);
            MarkDirty();
        }

        private void AddItem(ShortcutPanelItem item)
        {
            _doc.items.Add(item);
            if (_editMode) SelectOnly(item.id);
            MarkDirty();
        }

        private void DuplicateItems(List<ShortcutPanelItem> items)
        {
            var clones = new List<ShortcutPanelItem>();
            foreach (var item in _doc.items.Where(items.Contains))
            {
                var c = item.Clone();
                c.x += ShortcutPanelLayout.GridSize * 2f;
                c.y += ShortcutPanelLayout.GridSize * 2f;
                clones.Add(c);
            }
            if (clones.Count == 0) return;

            _doc.items.AddRange(clones);
            if (_editMode)
            {
                _selectedIds.Clear();
                _selectedIds.AddRange(clones.Select(c => c.id));
            }
            MarkDirty();
        }

        private void RemoveItems(List<ShortcutPanelItem> items)
        {
            if (items.Count == 0) return;
            _doc.items.RemoveAll(items.Contains);
            PruneSelection();
            GUI.FocusControl(null);
            MarkDirty();
        }

        private void AddDropped(List<DropEntry> entries, Vector2 pos)
        {
            var y = pos.y;
            var added = new List<string>();
            foreach (var entry in entries)
            {
                var p = new Vector2(pos.x, y);
                var item = entry.Kind == ShortcutItemKind.Url ? NewUrlItem(entry.Value, p) : NewAssetItem(entry.Value, p);
                _doc.items.Add(item);
                added.Add(item.id);
                y += item.height + ShortcutPanelLayout.GridSize;
            }
            if (_editMode && added.Count > 0)
            {
                _selectedIds.Clear();
                _selectedIds.AddRange(added);
            }
            MarkDirty();
        }

        private void AddLabel(Vector2 pos)
        {
            var size = ShortcutPanelLayout.DefaultLabelSize;
            AddItem(new ShortcutPanelItem
            {
                id = ShortcutPanelItem.NewId(),
                kind = ShortcutItemKind.Label,
                title = "Label",
                Rect = new Rect(pos, size)
            });
        }

        private void AddUrlFromClipboardOrBlank(Vector2 pos)
        {
            var url = ShortcutPanelDropParser.TryGetWebUrl(EditorGUIUtility.systemCopyBuffer, out var clip) ? clip : "https://";
            AddItem(NewUrlItem(url, pos));
        }

        private static ShortcutPanelItem NewAssetItem(string path, Vector2 pos) => new ShortcutPanelItem
        {
            id = ShortcutPanelItem.NewId(),
            kind = ShortcutItemKind.Asset,
            guid = AssetDatabase.AssetPathToGUID(path),
            path = path,
            Rect = new Rect(pos, ShortcutPanelLayout.DefaultShortcutSize)
        };

        private static ShortcutPanelItem NewUrlItem(string url, Vector2 pos) => new ShortcutPanelItem
        {
            id = ShortcutPanelItem.NewId(),
            kind = ShortcutItemKind.Url,
            url = url,
            Rect = new Rect(pos, ShortcutPanelLayout.DefaultShortcutSize)
        };

        private static ShortcutPanelItem NewMenuItem(string menuPath, Vector2 pos) => new ShortcutPanelItem
        {
            id = ShortcutPanelItem.NewId(),
            kind = ShortcutItemKind.Menu,
            menu = menuPath,
            Rect = new Rect(pos, ShortcutPanelLayout.DefaultShortcutSize)
        };

        /// <summary>Below the lowest item, left aligned — where toolbar-added items go.</summary>
        private Vector2 NextFreePosition()
        {
            var y = 0f;
            foreach (var item in _doc.items) y = Mathf.Max(y, item.y + item.height);
            return ShortcutPanelLayout.Snap(new Vector2(ShortcutPanelLayout.GridSize, y + ShortcutPanelLayout.GridSize));
        }

        // ------------------------------------------------------------------ property panel (edit mode)

        private void DrawPropertyPanel(ShortcutPanelItem item)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 64f;
                EditorGUI.BeginChangeCheck();

                switch (item.kind)
                {
                    case ShortcutItemKind.Label:
                        item.title = EditorGUILayout.TextField("Text", item.title);
                        break;

                    case ShortcutItemKind.Url:
                        item.url = EditorGUILayout.TextField("URL", item.url);
                        item.title = EditorGUILayout.TextField(new GUIContent("Title", "Empty = automatic"), item.title);
                        break;

                    case ShortcutItemKind.Menu:
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            item.menu = EditorGUILayout.TextField(new GUIContent("Menu", "Menu item path, e.g. Window/General/Console"), item.menu);
                            var pickContent = new GUIContent("...", "Pick a menu command");
                            var pickRect = GUILayoutUtility.GetRect(pickContent, EditorStyles.miniButton, GUILayout.Width(28f));
                            if (GUI.Button(pickRect, pickContent, EditorStyles.miniButton))
                            {
                                var id = item.id;
                                MenuCommandDropdown.Show(pickRect, path =>
                                {
                                    var picked = _doc?.Find(id);
                                    if (picked == null) return;
                                    picked.menu = path;
                                    GUI.FocusControl(null);
                                    MarkDirty();
                                });
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            item.title = EditorGUILayout.TextField(new GUIContent("Title", "Empty = last part of the menu path"), item.title);
                            var targetLabel = ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu));
                            var exists = ShortcutPanelMenuCatalog.Exists(item.menu);
                            GUILayout.Label(exists ? targetLabel : $"{targetLabel} (not found)", EditorStyles.miniLabel, GUILayout.Width(110f));
                        }
                        break;

                    case ShortcutItemKind.Asset:
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var path = ResolveAssetPath(item);
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.TextField("Asset", path ?? $"(missing) {item.path}");
                        }
                        item.title = EditorGUILayout.TextField(new GUIContent("Title", "Empty = asset name"), item.title);
                        break;
                }

                item.fontSize = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Font", "0 = default"), item.fontSize, GUILayout.Width(108f)), 0, 64);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.RectField("Rect", item.Rect);
                    r.x = Mathf.Max(0f, r.x);
                    r.y = Mathf.Max(0f, r.y);
                    r.width = Mathf.Max(ShortcutPanelLayout.MinWidth, r.width);
                    r.height = Mathf.Max(ShortcutPanelLayout.MinHeight, r.height);
                    item.Rect = r;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    // Keep the in-memory model consistent with what was saved before (null-safe strings).
                    item.title ??= "";
                    item.url ??= "";
                    item.menu ??= "";
                    MarkDirty();
                }

                EditorGUIUtility.labelWidth = prevLabelWidth;
            }
        }
    }
}
