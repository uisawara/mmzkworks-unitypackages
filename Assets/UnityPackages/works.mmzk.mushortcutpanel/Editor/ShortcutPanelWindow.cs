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
            Box,
            Link,
            Pan
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
        private bool _pendingCommandPicker;
        private Vector2 _pendingMenuPickerPos;
        private string _hoverId;
        private bool _showFullPath = true;
        private GUIStyle _fullPathStyle;
        private Texture2D _gridDotTexture;
        private bool _gridDotTextureIsPro;
        private Vector2 _lastCanvasMouse = new Vector2(16f, 16f);

        // Action tracing (edit mode): the cursor end of the rubber-band line, and the steps before the drag started.
        private Vector2 _linkCursor;

        // Middle-button drag scrolls the canvas: mouse position (window coordinates) and scroll at the start.
        private Vector2 _panStartMouse;
        private Vector2 _panStartScroll;
        private List<string> _linkStartSteps = new List<string>();

        // Action run (use mode). Steps are run one per delayCall so the editor can process each command in between.
        private const double FailedHighlightSeconds = 3.0;
        private string _runActionId;
        private List<ShortcutPanelItem> _runSteps;
        private int _runIndex;
        private string _failedId;
        private double _failedUntil;
        private int _runUndoGroup = -1;

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
            StopRun();
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

            if (_failedId != null && EditorApplication.timeSinceStartup >= _failedUntil)
            {
                _failedId = null;
                Repaint();
            }
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

            using (new EditorGUILayout.HorizontalScope())
            {
                var canvasRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCanvas(canvasRect);

                // Properties of the selected element, docked on the right in edit mode.
                if (_editMode) DrawSidePanel();
            }

            // Pickers requested from a context menu are opened here, where a GUI context exists.
            if (_pendingMenuPicker && Event.current.type == EventType.Layout)
            {
                _pendingMenuPicker = false;
                var canvasPos = _pendingMenuPickerPos;
                var windowPos = canvasPos - _scroll + _lastCanvasRect.position;
                MenuCommandDropdown.Show(new Rect(windowPos, new Vector2(1f, 1f)),
                    path => AddItem(NewMenuItem(path, canvasPos)));
            }
            if (_pendingCommandPicker && Event.current.type == EventType.Layout)
            {
                _pendingCommandPicker = false;
                var canvasPos = _pendingMenuPickerPos;
                var windowPos = canvasPos - _scroll + _lastCanvasRect.position;
                ShortcutCommandDropdown.Show(new Rect(windowPos, new Vector2(1f, 1f)),
                    id => AddItem(NewCommandItem(id, canvasPos)));
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
                    var commandContent = new GUIContent("+ Command", "Add an automation command (Prefab / GameObject / Scene / Asset)");
                    var commandRect = GUILayoutUtility.GetRect(commandContent, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(commandRect, commandContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var pos = NextFreePosition();
                        ShortcutCommandDropdown.Show(commandRect, id => AddItem(NewCommandItem(id, pos)));
                    }
                    if (GUILayout.Button(new GUIContent("+ Action",
                            "Add an action. Drag from its bottom knob over other elements to run them in that order"), EditorStyles.toolbarButton))
                        AddItem(NewActionItem(NextFreePosition()));
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
                        if (_op == PointerOp.Link) MarkDirty(); // keep what was traced so far
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

            if (_op == PointerOp.Pan) EditorGUIUtility.AddCursorRect(canvasRect, MouseCursor.Pan);

            // An unbounded canvas: _scroll is the canvas point shown at the top-left, and may be anything (negative too).
            // Everything inside the clip is drawn in canvas coordinates.
            var visible = new Rect(_scroll, canvasRect.size);
            GUI.BeginClip(canvasRect, -_scroll, Vector2.zero, false);
            {
                if (e.type == EventType.Repaint)
                {
                    if (_editMode) DrawGrid(visible);
                    DrawActionLinks();
                    foreach (var item in items) DrawItem(item);
                    if (_showFullPath)
                    {
                        foreach (var item in items) DrawFullPathLabel(item);
                    }
                    DrawStepBadges();
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
            GUI.EndClip();

            HandleDragAndDrop(canvasRect);
            HandlePointer(canvasRect);
            HandleKeyboard();
        }

        private Vector2 ToCanvas(Rect canvasRect, Vector2 windowPos) => windowPos - canvasRect.position + _scroll;

        /// <summary>Dot grid: a small dot at every snap point (8px).</summary>
        private void DrawGrid(Rect visible)
        {
            var tex = GetGridDotTexture();
            var tile = tex.width; // = 1 grid cell
            // Start on a grid line at or before the visible top-left so the dots stay on the snap points while panning
            // (the clip cuts off the rest).
            var x0 = Mathf.Floor(visible.x / tile) * tile;
            var y0 = Mathf.Floor(visible.y / tile) * tile;
            var cols = Mathf.Ceil((visible.xMax - x0) / tile);
            var rows = Mathf.Ceil((visible.yMax - y0) / tile);
            GUI.DrawTextureWithTexCoords(new Rect(x0, y0, cols * tile, rows * tile), tex, new Rect(0f, 0f, cols, rows));
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
                case ShortcutItemKind.Image:
                    DrawImageItem(item, r);
                    break;
                default:
                    DrawShortcutItem(item, r, isHover && !_editMode);
                    break;
            }

            if (item.id == _failedId)
            {
                DrawRoundedOutline(r, FailedColor, 2f);
            }
            else if (item.id == _runActionId || item.id == CurrentRunStepId)
            {
                DrawRoundedOutline(r, RunColor, 2f);
            }
            else if (isSelected)
            {
                DrawRoundedOutline(r, new Color(0.3f, 0.65f, 1f, 1f), 2f);
            }
            else if (_editMode)
            {
                DrawRoundedOutline(r, new Color(0.5f, 0.5f, 0.5f, 0.6f), 1f);
            }

            if (_editMode && item.kind == ShortcutItemKind.Action)
            {
                var k = ShortcutPanelLayout.ActionKnobRect(r);
                var linking = _op == PointerOp.Link && _opItemId == item.id;
                DrawRoundedRect(k, linking || isSelected ? LinkActiveColor : LinkColor, k.width * 0.5f);
                DrawRoundedOutline(k, EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : Color.white, 1f, k.width * 0.5f);
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

        private void DrawImageItem(ShortcutPanelItem item, Rect r)
        {
            var texture = LoadImage(item);
            if (texture != null)
            {
                GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit, true);
                return;
            }

            // Missing image: a placeholder so the element can still be found and fixed.
            var pro = EditorGUIUtility.isProSkin;
            DrawRoundedRect(r, pro ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.85f, 0.85f, 0.85f));
            var style = _labelTextStyle;
            style.fontSize = 0;
            style.normal.textColor = new Color(0.9f, 0.35f, 0.3f);
            style.Draw(Inset(r, 6f, 2f), new GUIContent($"(missing image) {Path.GetFileName(item.path)}"), false, false, false, false);
        }

        private static Texture LoadImage(ShortcutPanelItem item)
        {
            var path = ResolveAssetPath(item);
            return path != null ? AssetDatabase.LoadAssetAtPath<Texture>(path) : null;
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

        /// <summary>
        /// Background per kind, so things that "do" something stand out from plain links:
        /// assets / URLs are blue-grey, menu commands and commands are a dark neutral grey (#222 in the dark skin), actions are magenta-tinted.
        /// </summary>
        private static Color ItemBackground(string kind, bool pro, bool hover)
        {
            Color bg;
            switch (kind)
            {
                case ShortcutItemKind.Menu:
                case ShortcutItemKind.Command:
                    bg = pro ? new Color32(0x22, 0x22, 0x22, 0xFF) : new Color(0.95f, 0.95f, 0.95f);
                    break;
                case ShortcutItemKind.Action:
                    bg = pro ? new Color(0.31f, 0.18f, 0.26f) : new Color(0.95f, 0.82f, 0.90f);
                    break;
                default:
                    bg = pro ? new Color(0.22f, 0.24f, 0.275f) : new Color(0.87f, 0.89f, 0.93f);
                    break;
            }
            return hover ? Color.Lerp(bg, Color.white, pro ? 0.08f : 0.45f) : bg;
        }

        private void DrawShortcutItem(ShortcutPanelItem item, Rect r, bool hover)
        {
            var pro = EditorGUIUtility.isProSkin;
            DrawRoundedRect(r, ItemBackground(item.kind, pro, hover));

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

        private static void DrawRoundedRect(Rect r, Color c) => DrawRoundedRect(r, c, CornerRadius(r));

        private static void DrawRoundedRect(Rect r, Color c, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        private static void DrawRoundedOutline(Rect r, Color c, float t) => DrawRoundedOutline(r, c, t, CornerRadius(r));

        private static void DrawRoundedOutline(Rect r, Color c, float t, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, t, radius);
        }

        // ------------------------------------------------------------------ action links

        private static readonly Color RunColor = new Color(0.30f, 0.80f, 0.45f, 1f);
        private static readonly Color FailedColor = new Color(0.95f, 0.30f, 0.25f, 1f);
        private static readonly Color LinkActiveColor = new Color(0.3f, 0.65f, 1f, 1f);
        /// <summary>Lines are drawn fainter than the knob / badges so they stay in the background.</summary>
        private const float LinkLineAlpha = 0.5f;
        private static Color LinkColor =>EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f, 0.55f) : new Color(0.2f, 0.2f, 0.2f, 0.5f);

        private string CurrentRunStepId =>
            _runSteps != null && _runIndex >= 0 && _runIndex < _runSteps.Count ? _runSteps[_runIndex].id : null;

        /// <summary>The action whose step numbers are shown: the one being traced, run, hovered or selected.</summary>
        private ShortcutPanelItem FocusedAction()
        {
            string id = null;
            if (_op == PointerOp.Link) id = _opItemId;
            else if (_runActionId != null) id = _runActionId;
            else if (_hoverId != null && _doc.Find(_hoverId) is { kind: ShortcutItemKind.Action }) id = _hoverId;
            else if (_editMode && SingleSelected() is { kind: ShortcutItemKind.Action } sel) id = sel.id;
            var item = _doc.Find(id);
            return item != null && item.kind == ShortcutItemKind.Action ? item : null;
        }

        /// <summary>The action and its existing steps, in order.</summary>
        private List<ShortcutPanelItem> LinkNodes(ShortcutPanelItem action)
        {
            var nodes = new List<ShortcutPanelItem> { action };
            foreach (var id in action.steps)
            {
                var step = _doc.Find(id);
                if (step != null) nodes.Add(step);
            }
            return nodes;
        }

        /// <summary>Lines from each action through its steps. Drawn behind the items.</summary>
        private void DrawActionLinks()
        {
            var focused = FocusedAction();
            var prev = Handles.color;
            foreach (var action in _doc.items)
            {
                if (action.kind != ShortcutItemKind.Action) continue;
                var linking = _op == PointerOp.Link && _opItemId == action.id;
                var nodes = LinkNodes(action);
                if (nodes.Count < 2 && !linking) continue;

                var active = action == focused;
                var color = action.id == _runActionId ? RunColor : active ? LinkActiveColor : LinkColor;
                color.a *= LinkLineAlpha;
                Handles.color = color;
                var width = active ? 4f : 3f;
                for (var i = 1; i < nodes.Count; i++)
                {
                    // A line into a nested action gets finer dots, so "runs that action" reads differently from a plain step.
                    var toAction = nodes[i].kind == ShortcutItemKind.Action;
                    DrawDottedArc(nodes[i - 1].Rect.center, nodes[i].Rect.center, width, toAction ? NestedLinkDots : LinkDots);
                }
                if (linking) DrawDottedArc(nodes[nodes.Count - 1].Rect.center, _linkCursor, width, LinkDots);
            }
            Handles.color = prev;
        }

        /// <summary>Dash pattern of a link line: dash and gap lengths along the curve, and a width factor.</summary>
        private readonly struct DotStyle
        {
            public readonly float Dash;
            public readonly float Gap;
            public readonly float WidthScale;

            public DotStyle(float dash, float gap, float widthScale)
            {
                Dash = dash;
                Gap = gap;
                WidthScale = widthScale;
            }
        }

        private static readonly DotStyle LinkDots = new DotStyle(5f, 4f, 1f);
        private static readonly DotStyle NestedLinkDots = new DotStyle(2f, 3f, 0.6f);

        private const float LinkBend = 0.2f;      // arc height relative to the segment length
        private const float LinkMaxBend = 40f;

        /// <summary>
        /// Dotted arc from <paramref name="a"/> to <paramref name="b"/> in the current <see cref="Handles.color"/>.
        /// It always bows to the same side of the travel direction, so the order stays readable even for items in a row.
        /// </summary>
        private static void DrawDottedArc(Vector2 a, Vector2 b, float width, DotStyle dots)
        {
            width *= dots.WidthScale;
            var d = b - a;
            var len = d.magnitude;
            if (len < 1f) return;

            var normal = new Vector2(-d.y, d.x) / len;
            var ctrl = (a + b) * 0.5f + normal * Mathf.Min(len * LinkBend, LinkMaxBend);

            // Sample the quadratic Bezier finely enough for the dash, then emit it in dash / gap pieces by arc length.
            var step = Mathf.Clamp(dots.Dash * 0.5f, 0.5f, 2f);
            var samples = Mathf.Max(8, Mathf.CeilToInt(len / step));
            var dash = new List<Vector3>();
            var on = true;
            var acc = 0f;
            var last = a;
            dash.Add(a);
            for (var i = 1; i <= samples; i++)
            {
                var t = i / (float)samples;
                var u = 1f - t;
                var p = u * u * a + 2f * u * t * ctrl + t * t * b;
                acc += Vector2.Distance(last, p);
                last = p;
                if (on) dash.Add(p);

                if (acc < (on ? dots.Dash : dots.Gap)) continue;
                if (on && dash.Count >= 2) Handles.DrawAAPolyLine(width, dash.ToArray());
                dash.Clear();
                on = !on;
                acc = 0f;
                if (on) dash.Add(p);
            }
            if (on && dash.Count >= 2) Handles.DrawAAPolyLine(width, dash.ToArray());
        }

        /// <summary>Step numbers on the items of the focused action. Drawn on top of the items.</summary>
        private void DrawStepBadges()
        {
            var action = FocusedAction();
            if (action == null) return;

            var style = EditorStyles.miniBoldLabel;
            var n = 0;
            foreach (var id in action.steps)
            {
                var step = _doc.Find(id);
                if (step == null) continue;
                n++;
                var content = new GUIContent(n.ToString());
                var w = Mathf.Max(14f, style.CalcSize(content).x + 6f);
                var badge = new Rect(step.x - 5f, step.y - 6f, w, 14f);
                DrawRoundedRect(badge, action.id == _runActionId ? RunColor : LinkActiveColor, 7f);
                var prevColor = style.normal.textColor;
                var prevAlign = style.alignment;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                style.Draw(badge, content, false, false, false, false);
                style.normal.textColor = prevColor;
                style.alignment = prevAlign;
            }
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
            if (item.kind == ShortcutItemKind.Action)
                return !string.IsNullOrEmpty(item.title) ? item.title : "Action";
            if (item.kind == ShortcutItemKind.Command)
            {
                var command = ShortcutCommandRegistry.Find(item.command);
                missing = command == null;
                if (!string.IsNullOrEmpty(item.title)) return item.title;
                return command != null ? command.Name : $"{item.command} (unknown)";
            }
            if (item.kind == ShortcutItemKind.Label)
                return item.title;

            var path = ResolveAssetPath(item);
            missing = path == null;
            if (!string.IsNullOrEmpty(item.title)) return item.title;
            var shown = path ?? item.path;
            var name = AssetDatabase.IsValidFolder(shown ?? "") ? Path.GetFileName(shown) : Path.GetFileNameWithoutExtension(shown);
            return missing ? $"{name} (missing)" : name;
        }

        private string SubText(ShortcutPanelItem item)
        {
            if (item.kind == ShortcutItemKind.Url) return item.url;
            if (item.kind == ShortcutItemKind.Action) return StepCountText(item);
            if (item.kind == ShortcutItemKind.Command) return CommandSummary(item);
            if (item.kind == ShortcutItemKind.Menu)
            {
                var target = ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu));
                return $"{target}: {ShortcutPanelMenuPath.ParentPath(item.menu)}";
            }
            var path = ResolveAssetPath(item) ?? item.path;
            return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
        }

        private string Tooltip(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Url: return item.url;
                case ShortcutItemKind.Command:
                {
                    var command = ShortcutCommandRegistry.Find(item.command);
                    if (command == null) return $"Unknown command: {item.command}";
                    var summary = command.Summary(item);
                    return string.IsNullOrEmpty(summary) ? command.Description : $"{command.Description}{Environment.NewLine}{summary}";
                }
                case ShortcutItemKind.Action:
                    return item.steps.Count == 0
                        ? "No steps. In Edit Mode, drag from the bottom knob over other elements."
                        : string.Join(Environment.NewLine, StepNames(item).Select((n, i) => $"{i + 1}. {n}"));
                case ShortcutItemKind.Menu:
                    return $"{ShortcutPanelMenuPath.TargetLabel(ShortcutPanelMenuPath.TargetOf(item.menu))} command: {item.menu}";
                case ShortcutItemKind.Asset: return ResolveAssetPath(item) ?? $"Missing: {item.path}";
                default: return null;
            }
        }

        /// <summary>Asset path / URL / menu path of a shortcut, the step names of an action, or null for labels.</summary>
        private string FullPathText(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Action:
                    return item.steps.Count == 0 ? "(no steps)" : string.Join(" → ", StepNames(item));
                case ShortcutItemKind.Command:
                {
                    var summary = CommandSummary(item);
                    return string.IsNullOrEmpty(summary) ? item.command : $"{item.command}: {summary}";
                }
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

        private static string CommandSummary(ShortcutPanelItem item)
        {
            var command = ShortcutCommandRegistry.Find(item.command);
            return command == null ? $"Unknown command: {item.command}" : command.Summary(item);
        }

        private static string StepCountText(ShortcutPanelItem action) =>
            action.steps.Count == 1 ? "1 step" : $"{action.steps.Count} steps";

        /// <summary>Display names of an action's direct steps, in order (missing ones included as "(missing)").</summary>
        private IEnumerable<string> StepNames(ShortcutPanelItem action)
        {
            foreach (var id in action.steps)
            {
                var step = _doc?.Find(id);
                yield return step != null ? DisplayName(step, out _) : "(missing)";
            }
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
                case ShortcutItemKind.Action:
                    return KindIcon(ActionIconNames);
                case ShortcutItemKind.Command:
                    return missing ? EditorGUIUtility.IconContent("console.warnicon.sml").image
                        : ShortcutCommandIcons.For(ShortcutCommandRegistry.Find(item.command));
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
        private static readonly string[] ActionIconNames = { "PlayButton", "Animation.Play", "PlayButtonProfile" };
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

                case EventType.MouseDown when inCanvas && e.button == 2 && _op == PointerOp.None:
                {
                    _op = PointerOp.Pan;
                    _opItemId = null;
                    _panStartMouse = e.mousePosition;
                    _panStartScroll = _scroll;
                    GUIUtility.hotControl = controlId;
                    e.Use();
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
                        var knobAction = HitActionKnob(local);
                        if (knobAction != null)
                        {
                            // Trace a new step list: it replaces the previous one.
                            SelectOnly(knobAction.id);
                            _linkStartSteps = new List<string>(knobAction.steps);
                            knobAction.steps.Clear();
                            _linkCursor = local;
                            _op = PointerOp.Link;
                            BeginOp(knobAction, local, controlId);
                        }
                        else if (hit == null)
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
                    else if (hit != null && ShortcutItemKind.IsRunnable(hit.kind))
                    {
                        _op = PointerOp.Press;
                        BeginOp(hit, local, controlId);
                        e.Use();
                    }
                    break;
                }

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                {
                    if (_op == PointerOp.Pan)
                    {
                        _scroll = _panStartScroll - (e.mousePosition - _panStartMouse);
                        e.Use();
                        Repaint();
                        break;
                    }

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
                        case PointerOp.Link:
                        {
                            // Sample along the moved segment so a quick stroke does not skip small items.
                            var from = _linkCursor;
                            var samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, local) / 4f));
                            for (var i = 1; i <= samples; i++)
                            {
                                var p = Vector2.Lerp(from, local, i / (float)samples);
                                ShortcutPanelActionTrace.TryAdd(item.steps, item, ShortcutPanelLayout.HitTest(_doc.items, p));
                            }
                            _linkCursor = local;
                            break;
                        }
                        case PointerOp.Move:
                            MoveGroup(delta);
                            break;
                        case PointerOp.Resize:
                            item.Rect = ShortcutPanelLayout.Resized(_opStartRect, delta);
                            break;
                        case PointerOp.Press when delta.magnitude > DragOutThreshold
                                                  && (item.kind == ShortcutItemKind.Asset || item.kind == ShortcutItemKind.Url):
                            // Drag a shortcut out of the panel (e.g. into the Inspector or the Hierarchy).
                            StartDragOut(item);
                            EndOp();
                            break;
                    }
                    e.Use();
                    Repaint();
                    break;
                }

                // The middle button ends only a pan, and the other buttons end only the other operations.
                case EventType.MouseUp when GUIUtility.hotControl == controlId && (e.button == 2) == (_op == PointerOp.Pan):
                {
                    var item = _doc.Find(_opItemId);
                    var op = _op;
                    EndOp();
                    e.Use();

                    if (op == PointerOp.Box || op == PointerOp.Pan)
                    {
                        Repaint();
                        break;
                    }
                    if (item == null) break;
                    if (op == PointerOp.Link)
                    {
                        // A plain click on the knob (no stroke over anything) keeps the previous steps.
                        if (item.steps.Count == 0 && (local - _opStartMouse).magnitude < DragOutThreshold)
                            item.steps.AddRange(_linkStartSteps);
                        if (!item.steps.SequenceEqual(_linkStartSteps)) MarkDirty();
                    }
                    else if (op == PointerOp.Move || op == PointerOp.Resize)
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

        /// <summary>The topmost action whose knob is under <paramref name="p"/>, or null.</summary>
        private ShortcutPanelItem HitActionKnob(Vector2 p)
        {
            for (var i = _doc.items.Count - 1; i >= 0; i--)
            {
                var item = _doc.items[i];
                if (item.kind == ShortcutItemKind.Action && ShortcutPanelLayout.ActionKnobRect(item.Rect).Contains(p))
                    return item;
            }
            return null;
        }

        private void MoveGroup(Vector2 delta)
        {
            var offset = ShortcutPanelLayout.GroupMoveOffset(_opStartRect, delta);
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
                case "FrameSelected":
                {
                    // F key: center the view on all items.
                    if (_doc.items.Count == 0) return;
                    if (execute) FrameItems(_doc.items);
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

            var entries = CollectDropEntries(e.alt);
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

        private static List<DropEntry> CollectDropEntries(bool assetImagesAsPictures)
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
                },
                assetImagesAsPictures);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Runs a shortcut clicked by the user.</summary>
        private void Activate(ShortcutPanelItem item)
        {
            switch (item.kind)
            {
                case ShortcutItemKind.Action:
                    RunAction(item);
                    break;
                case ShortcutItemKind.Menu:
                case ShortcutItemKind.Command:
                    // Run outside OnGUI: commands may open dialogs, change scenes or the window layout.
                    EditorApplication.delayCall += () =>
                    {
                        if (this == null) return;
                        bool ok;
                        string runError;
                        try
                        {
                            Undo.IncrementCurrentGroup();
                            Undo.SetCurrentGroupName(DisplayName(item, out _));
                            ok = TryActivateNow(item, out runError);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            ok = false;
                            runError = ex.Message;
                        }
                        if (ok) return;
                        Debug.LogWarning($"[muShortcutPanel] {DisplayName(item, out _)}: {runError}");
                        ShowNotification(new GUIContent(runError));
                    };
                    break;
                default:
                    if (!TryActivateNow(item, out var error))
                        ShowNotification(new GUIContent(error));
                    break;
            }
        }

        /// <summary>
        /// Runs one shortcut synchronously (asset: select in Project, URL: open, menu: execute).
        /// Returns false with a reason when it could not be run.
        /// </summary>
        private static bool TryActivateNow(ShortcutPanelItem item, out string error)
        {
            error = null;
            switch (item.kind)
            {
                case ShortcutItemKind.Asset:
                {
                    var path = ResolveAssetPath(item);
                    var obj = path != null ? AssetDatabase.LoadAssetAtPath<Object>(path) : null;
                    if (obj == null)
                    {
                        error = $"Missing: {item.path}";
                        return false;
                    }
                    SelectInProject(obj);
                    return true;
                }
                case ShortcutItemKind.Url:
                    if (!ShortcutPanelDropParser.TryGetWebUrl(item.url, out var url))
                    {
                        error = $"Invalid URL: {item.url}";
                        return false;
                    }
                    Application.OpenURL(url);
                    return true;
                case ShortcutItemKind.Menu:
                    return TryExecuteMenuCommand(item.menu, out error);
                case ShortcutItemKind.Command:
                    return TryRunCommand(item, out error);
                default:
                    error = $"Cannot run: {DisplayName(item, out _)}";
                    return false;
            }
        }

        private static bool TryRunCommand(ShortcutPanelItem item, out string error)
        {
            var command = ShortcutCommandRegistry.Find(item.command);
            if (command == null)
            {
                error = $"Unknown command: {item.command}";
                return false;
            }

            var ctx = new ShortcutCommandContext(command, item);
            if (command.Run(ctx))
            {
                error = null;
                return true;
            }
            error = string.IsNullOrEmpty(ctx.Error) ? $"{command.Name} failed" : ctx.Error;
            return false;
        }

        private static bool TryExecuteMenuCommand(string menuPath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(menuPath))
            {
                error = "No menu command set";
                return false;
            }

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

            if (EditorApplication.ExecuteMenuItem(menuPath)) return true;
            error = $"Menu command not available: {menuPath}";
            return false;
        }

        // ------------------------------------------------------------------ action run

        private void RunAction(ShortcutPanelItem action)
        {
            if (_runActionId != null)
            {
                ShowNotification(new GUIContent("An action is already running"));
                return;
            }

            var steps = ShortcutPanelActionPlan.Expand(_doc, action.id, out var error);
            if (steps == null)
            {
                ShowNotification(new GUIContent(error));
                return;
            }
            if (steps.Count == 0)
            {
                ShowNotification(new GUIContent("This action has no steps"));
                return;
            }

            _runActionId = action.id;
            _runSteps = steps;
            _runIndex = 0;
            _failedId = null;

            // Everything the steps record goes into one Undo entry ("Action: <name>"), collapsed in StopRun.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Action: {DisplayName(action, out _)}");
            _runUndoGroup = Undo.GetCurrentGroup();
            Repaint();
            EditorApplication.delayCall += RunNextStep;
        }

        /// <summary>Runs the current step, then schedules the next one. Stops at the first failure.</summary>
        private void RunNextStep()
        {
            if (this == null || _runSteps == null) return;
            if (_runIndex >= _runSteps.Count)
            {
                StopRun();
                return;
            }

            var step = _runSteps[_runIndex];
            bool ok;
            string error;
            try
            {
                if (_doc?.Find(step.id) == null)
                {
                    ok = false;
                    error = "Step was removed";
                }
                else
                {
                    ok = TryActivateNow(step, out error);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ok = false;
                error = ex.Message;
            }

            if (!ok)
            {
                var message = $"Action stopped at {_runIndex + 1}/{_runSteps.Count} ({DisplayName(step, out _)}): {error}";
                Debug.LogWarning($"[muShortcutPanel] {message}");
                _failedId = step.id;
                _failedUntil = EditorApplication.timeSinceStartup + FailedHighlightSeconds;
                StopRun();
                ShowNotification(new GUIContent(message));
                return;
            }

            _runIndex++;
            Repaint();
            EditorApplication.delayCall += RunNextStep;
        }

        private void StopRun()
        {
            if (_runActionId == null && _runSteps == null) return;
            if (_runUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_runUndoGroup);
                _runUndoGroup = -1;
            }
            _runActionId = null;
            _runSteps = null;
            _runIndex = 0;
            Repaint();
        }

        private static void SelectInProject(Object obj)
        {
            EditorUtility.FocusProjectWindow();

            // Folder in a two-column Project window: open it so the right column lists its contents.
            if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)) && ProjectWindowFolder.TryShowContents(obj))
                return;

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

            if (hit != null && hit.kind == ShortcutItemKind.Image)
            {
                menu.AddItem(new GUIContent("Select Image in Project"), false, () =>
                {
                    var p = ResolveAssetPath(hit);
                    var obj = p != null ? AssetDatabase.LoadAssetAtPath<Object>(p) : null;
                    if (obj != null) SelectInProject(obj);
                    else ShowNotification(new GUIContent($"Missing: {hit.path}"));
                });
            }

            if (hit != null && ShortcutItemKind.IsRunnable(hit.kind))
            {
                var activateLabel = hit.kind == ShortcutItemKind.Url ? "Open URL"
                    : hit.kind == ShortcutItemKind.Menu ? "Execute Command"
                    : hit.kind == ShortcutItemKind.Action ? "Run Action"
                    : hit.kind == ShortcutItemKind.Command ? "Run Command"
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
                    case ShortcutItemKind.Command:
                        break;
                    case ShortcutItemKind.Action:
                        if (_editMode && hit.steps.Count > 0)
                            menu.AddItem(new GUIContent("Clear Steps"), false, () => ClearSteps(hit));
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
                    menu.AddItem(new GUIContent("Add Command Here..."), false, () =>
                    {
                        _pendingCommandPicker = true;
                        _pendingMenuPickerPos = pos;
                        Repaint();
                    });
                    menu.AddItem(new GUIContent("Add Action Here"), false, () => AddItem(NewActionItem(pos)));
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

            if (hit == null && _doc.items.Count > 0)
            {
                if (menu.GetItemCount() > 0) menu.AddSeparator("");
                menu.AddItem(new GUIContent("Frame All  (F)"), false, () => FrameItems(_doc.items));
            }

            if (menu.GetItemCount() > 0) menu.ShowAsContext();
        }

        /// <summary>Scrolls so the center of the items is at the center of the canvas.</summary>
        private void FrameItems(IReadOnlyList<ShortcutPanelItem> items)
        {
            var bounds = ShortcutPanelLayout.Bounds(items);
            if (bounds == null) return;
            var scroll = bounds.Value.center - _lastCanvasRect.size * 0.5f;
            _scroll = new Vector2(Mathf.Round(scroll.x), Mathf.Round(scroll.y));
            Repaint();
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
            _doc.RemoveStepReferences(items.Select(i => i.id).ToList());
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
                ShortcutPanelItem item;
                switch (entry.Kind)
                {
                    case ShortcutItemKind.Url:
                        item = NewUrlItem(entry.Value, p);
                        break;
                    case ShortcutItemKind.Image:
                        item = NewImageItem(entry.Value, p);
                        break;
                    case ShortcutPanelDropParser.ExternalImage:
                    {
                        var imported = ImportExternalImage(entry.Value);
                        if (imported == null) continue;
                        item = NewImageItem(imported, p);
                        break;
                    }
                    default:
                        item = NewAssetItem(entry.Value, p);
                        break;
                }
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

        private const float ImageMaxInitialSize = 256f;

        private static ShortcutPanelItem NewImageItem(string path, Vector2 pos)
        {
            var item = new ShortcutPanelItem
            {
                id = ShortcutPanelItem.NewId(),
                kind = ShortcutItemKind.Image,
                guid = AssetDatabase.AssetPathToGUID(path),
                path = path,
                Rect = new Rect(pos, Vector2.one * ShortcutPanelLayout.GridSize * 8f)
            };
            FitImageSize(item);
            return item;
        }

        /// <summary>Sets the size to the image's aspect ratio, at most <see cref="ImageMaxInitialSize"/> px, on the grid.</summary>
        private static void FitImageSize(ShortcutPanelItem item)
        {
            var texture = LoadImage(item);
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;

            var scale = Mathf.Min(1f, ImageMaxInitialSize / Mathf.Max(texture.width, texture.height));
            var w = Mathf.Max(ShortcutPanelLayout.MinWidth, ShortcutPanelLayout.Snap(texture.width * scale));
            var h = Mathf.Max(ShortcutPanelLayout.MinHeight, ShortcutPanelLayout.Snap(texture.height * scale));
            item.width = w;
            item.height = h;
        }

        /// <summary>
        /// Copies an image file from outside the project into "&lt;panel folder&gt;/&lt;panel name&gt; Images/" and imports it.
        /// Returns the new asset path, or null on failure.
        /// </summary>
        private string ImportExternalImage(string absolutePath)
        {
            try
            {
                var panelDir = Path.GetDirectoryName(_filePath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(panelDir) || !panelDir.StartsWith("Assets", StringComparison.Ordinal)) panelDir = "Assets";
                var folder = $"{panelDir}/{ShortcutPanelFiles.DisplayName(_filePath)} Images";
                if (!ShortcutCommandUtil.EnsureFolder(folder))
                {
                    ShowNotification(new GUIContent($"Cannot create folder: {folder}"));
                    return null;
                }

                var target = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Path.GetFileName(absolutePath)}");
                File.Copy(absolutePath, FullPath(target));
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                return target;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[muShortcutPanel] Could not import {absolutePath}: {ex.Message}");
                ShowNotification(new GUIContent($"Could not import {Path.GetFileName(absolutePath)}"));
                return null;
            }
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

        private static ShortcutPanelItem NewActionItem(Vector2 pos) => new ShortcutPanelItem
        {
            id = ShortcutPanelItem.NewId(),
            kind = ShortcutItemKind.Action,
            Rect = new Rect(pos, ShortcutPanelLayout.DefaultShortcutSize)
        };

        private static ShortcutPanelItem NewCommandItem(string commandId, Vector2 pos) => new ShortcutPanelItem
        {
            id = ShortcutPanelItem.NewId(),
            kind = ShortcutItemKind.Command,
            command = commandId,
            Rect = new Rect(pos, ShortcutPanelLayout.DefaultShortcutSize)
        };

        private void ClearSteps(ShortcutPanelItem action)
        {
            if (action.steps.Count == 0) return;
            action.steps.Clear();
            MarkDirty();
        }

        /// <summary>Below the lowest item, left aligned — where toolbar-added items go.</summary>
        private Vector2 NextFreePosition()
        {
            var y = 0f;
            foreach (var item in _doc.items) y = Mathf.Max(y, item.y + item.height);
            return ShortcutPanelLayout.Snap(new Vector2(ShortcutPanelLayout.GridSize, y + ShortcutPanelLayout.GridSize));
        }

        // ------------------------------------------------------------------ property panel (edit mode)

        private const string SidePanelWidthPrefKey = "Mmzkworks.muShortcutPanel.SidePanelWidth";
        private const float SidePanelMinWidth = 200f;
        private const float SidePanelDefaultWidth = 280f;
        private const float SplitterWidth = 4f;

        private float _sidePanelWidth = -1f;
        private Vector2 _sidePanelScroll;
        private float _splitterStartMouseX;
        private float _splitterStartWidth;

        /// <summary>Right-hand panel: properties of the single selected element (or a hint). The left edge drags to resize.</summary>
        private void DrawSidePanel()
        {
            if (_sidePanelWidth < 0f) _sidePanelWidth = EditorPrefs.GetFloat(SidePanelWidthPrefKey, SidePanelDefaultWidth);
            // Leave the canvas at least a little room in a narrow window.
            var maxWidth = Mathf.Max(SidePanelMinWidth, position.width - 160f);
            var width = Mathf.Clamp(_sidePanelWidth, SidePanelMinWidth, maxWidth);

            var splitter = GUILayoutUtility.GetRect(SplitterWidth, 0f, GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));
            HandleSplitter(splitter);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                _sidePanelScroll = EditorGUILayout.BeginScrollView(_sidePanelScroll);

                var selected = SingleSelected();
                if (selected != null)
                    DrawPropertyPanel(selected);
                else if (_selectedIds.Count > 1)
                    EditorGUILayout.HelpBox($"{_selectedIds.Count} items selected. Drag to move them together, Delete to remove.", MessageType.None);
                else
                    EditorGUILayout.HelpBox("Select an element to edit its properties.", MessageType.None);

                EditorGUILayout.EndScrollView();
            }
        }

        private void HandleSplitter(Rect splitter)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);

            switch (e.GetTypeForControl(id))
            {
                case EventType.Repaint:
                {
                    var line = new Rect(splitter.center.x - 0.5f, splitter.y, 1f, splitter.height);
                    EditorGUI.DrawRect(line, EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.6f, 0.6f, 0.6f));
                    break;
                }
                case EventType.MouseDown when e.button == 0 && splitter.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    _splitterStartMouseX = e.mousePosition.x;
                    _splitterStartWidth = _sidePanelWidth;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    // Dragging left widens the panel.
                    _sidePanelWidth = Mathf.Clamp(_splitterStartWidth - (e.mousePosition.x - _splitterStartMouseX),
                        SidePanelMinWidth, Mathf.Max(SidePanelMinWidth, position.width - 160f));
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    EditorPrefs.SetFloat(SidePanelWidthPrefKey, _sidePanelWidth);
                    e.Use();
                    break;
            }
        }

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

                    case ShortcutItemKind.Image:
                    {
                        var current = LoadImage(item);
                        var picked = (Texture)EditorGUILayout.ObjectField("Image", current, typeof(Texture), false);
                        if (picked != current && picked != null)
                        {
                            item.path = AssetDatabase.GetAssetPath(picked);
                            item.guid = AssetDatabase.AssetPathToGUID(item.path);
                        }
                        if (GUILayout.Button(new GUIContent("Fit to Image", "Resize to the image's aspect ratio (max 256 px)"), EditorStyles.miniButton))
                        {
                            FitImageSize(item);
                            GUI.changed = true;
                        }
                        break;
                    }

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

                    case ShortcutItemKind.Action:
                        item.title = EditorGUILayout.TextField(new GUIContent("Title", "Empty = \"Action\""), item.title);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PrefixLabel(new GUIContent("Steps", "Drag from the knob at the bottom of the action over other elements"));
                            var text = item.steps.Count == 0
                                ? "(none) Drag from the bottom knob over elements"
                                : string.Join("  →  ", StepNames(item).Select((n, i) => $"{i + 1}. {n}"));
                            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
                            using (new EditorGUI.DisabledScope(item.steps.Count == 0))
                            {
                                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                                    ClearSteps(item);
                            }
                        }
                        break;

                    case ShortcutItemKind.Command:
                        DrawCommandFields(item);
                        break;
                }

                item.fontSize = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Font", "0 = default"), item.fontSize, GUILayout.Width(108f)), 0, 64);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.RectField("Rect", item.Rect);
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

        private string _typeCheckName;
        private string _typeCheckError;

        /// <summary>Title plus one field per command parameter (generated from the parameter kinds).</summary>
        private void DrawCommandFields(ShortcutPanelItem item)
        {
            var command = ShortcutCommandRegistry.Find(item.command);
            if (command == null)
            {
                EditorGUILayout.HelpBox($"Unknown command: {item.command}", MessageType.Warning);
                return;
            }

            GUILayout.Label($"{command.Category} / {command.Name}", EditorStyles.miniBoldLabel);
            GUILayout.Label(command.Description, EditorStyles.wordWrappedMiniLabel);

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 84f;
            item.title = EditorGUILayout.TextField(new GUIContent("Title", $"Empty = \"{command.Name}\""), item.title);

            foreach (var p in command.Params)
            {
                if (!command.IsVisible(p, item)) continue;
                var label = new GUIContent(p.Label, p.Tooltip);
                var current = ShortcutCommandContext.Value(command, item, p.Key);
                var next = current;

                switch (p.Kind)
                {
                    case ShortcutParamKind.Bool:
                        next = EditorGUILayout.Toggle(label, current == "true") ? "true" : "false";
                        break;

                    case ShortcutParamKind.Choice:
                    {
                        var index = Mathf.Max(0, Array.IndexOf(p.Choices, current));
                        next = p.Choices[EditorGUILayout.Popup(label, index, p.Choices)];
                        break;
                    }

                    case ShortcutParamKind.Asset:
                    {
                        var path = string.IsNullOrEmpty(current) ? null : AssetDatabase.GUIDToAssetPath(current);
                        var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, p.AssetType);
                        var picked = EditorGUILayout.ObjectField(label, asset, p.AssetType, false);
                        if (picked != asset)
                            next = picked != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked)) : "";
                        break;
                    }

                    case ShortcutParamKind.Vector3:
                    {
                        ShortcutCommandUtil.TryParseVector3(current, out var v);
                        var edited = EditorGUILayout.Vector3Field(label, v);
                        if (edited != v) next = ShortcutCommandUtil.FormatVector3(edited);
                        break;
                    }

                    case ShortcutParamKind.Folder:
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            next = EditorGUILayout.TextField(label, current);
                            if (GUILayout.Button(new GUIContent("...", "Pick a folder"), EditorStyles.miniButton, GUILayout.Width(28f)))
                            {
                                var start = ShortcutCommandUtil.NormalizeAssetFolder(current) ?? "Assets";
                                var abs = EditorUtility.OpenFolderPanel(p.Label, FullPath(start), "");
                                var rel = ShortcutCommandUtil.ToProjectRelative(abs, ProjectRoot);
                                if (rel != null)
                                {
                                    item.SetArg(p.Key, rel);
                                    MarkDirty();
                                }
                                else if (!string.IsNullOrEmpty(abs))
                                {
                                    ShowNotification(new GUIContent("Pick a folder inside this project"));
                                }
                                // The modal dialog broke this layout pass; end it here (the value is already stored).
                                GUIUtility.ExitGUI();
                            }
                        }
                        break;

                    case ShortcutParamKind.TypeName:
                        next = EditorGUILayout.TextField(label, current);
                        if (!string.IsNullOrWhiteSpace(next))
                        {
                            if (next != _typeCheckName)
                            {
                                _typeCheckName = next;
                                ShortcutCommandUtil.ResolveComponentType(next, out _typeCheckError);
                            }
                            if (_typeCheckError != null) EditorGUILayout.HelpBox(_typeCheckError, MessageType.Warning);
                        }
                        break;

                    default:
                        next = EditorGUILayout.TextField(label, current);
                        break;
                }

                if (next != current)
                {
                    item.SetArg(p.Key, next);
                    GUI.changed = true;
                }
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }
    }
}
