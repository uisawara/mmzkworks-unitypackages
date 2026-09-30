using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Mmzkworks.mushortcut.Editor
{
    [Serializable]
    internal sealed class ShortcutItem
    {
        public ShortcutItemType itemType = ShortcutItemType.Object;
        public string globalId = "";
        public string displayName = "";
        public string labelText = "";
        public Color labelColor = Color.yellow;
    }

    internal enum ShortcutItemType
    {
        Object = 0,
        Label = 1
    }

    internal enum ViewMode
    {
        Icon = 0,
        List = 1
    }

    [Serializable]
    internal sealed class ShortcutPage
    {
        public string pageName = "Page 1";
        public List<ShortcutItem> items = new List<ShortcutItem>();
    }

    [Serializable]
    internal sealed class ShortcutData
    {
        public List<ShortcutPage> pages = new List<ShortcutPage>();
        
        // For backward compatibility
        [Obsolete("Use pages instead")]
        public List<ShortcutItem> items;
    }

    internal static class ShortcutStorage
    {
        private const string RelativeDir = "Assets/Settings/mushortcut";
        private const string FileName = "shortcuts.json";

        // Changes are coalesced and written after this idle period, so rapid edits
        // (typing in a label, dragging the color picker, reordering) don't hit the disk / AssetDatabase each time.
        private const double SaveDelaySeconds = 0.5;

        internal static string RelativePath => Path.Combine(RelativeDir, FileName).Replace('\\', '/');
        private static string FullPath => Path.Combine(Application.dataPath, "Settings/mushortcut", FileName);

        // Single in-memory model shared by all windows
        private static ShortcutData _data;
        private static string _lastSyncedJson;
        private static bool _dirty;
        private static double _saveDueTime;
        private static bool _updateHooked;

        /// <summary>Incremented whenever the in-memory model is replaced (e.g. the file was changed externally).</summary>
        public static int Version { get; private set; }

        public static List<ShortcutPage> Pages
        {
            get
            {
                if (_data == null)
                {
                    _data = Load(out _lastSyncedJson);
                    Version++;
                }
                return _data.pages;
            }
        }

        static ShortcutStorage()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Flush;
            EditorApplication.quitting += Flush;
        }

        private static ShortcutData Load(out string json)
        {
            json = null;
            if (!File.Exists(FullPath))
            {
                var newData = new ShortcutData();
                // Add initial page
                newData.pages.Add(new ShortcutPage { pageName = "Page 1" });
                return newData;
            }

            json = File.ReadAllText(FullPath, Encoding.UTF8);
            return Parse(json);
        }

        private static ShortcutData Parse(string json)
        {
            var data = JsonUtility.FromJson<ShortcutData>(json);
            if (data == null)
                data = new ShortcutData();

            #pragma warning disable CS0618
            if (data.pages == null || data.pages.Count == 0)
            {
                if (data.items != null && data.items.Count > 0)
                {
                    data.pages = new List<ShortcutPage>
                    {
                        new ShortcutPage { pageName = "Page 1", items = data.items }
                    };
                }
                else
                {
                    data.pages = new List<ShortcutPage> { new ShortcutPage { pageName = "Page 1" } };
                }
            }
            data.items = null;
            #pragma warning restore CS0618

            return data;
        }

        /// <summary>Marks the model as changed. The file is written after a short idle period.</summary>
        public static void MarkDirty()
        {
            _dirty = true;
            _saveDueTime = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            if (!_updateHooked)
            {
                EditorApplication.update += OnEditorUpdate;
                _updateHooked = true;
            }

            ShortcutWindow.RepaintAllWindows();
        }

        private static void OnEditorUpdate()
        {
            if (_dirty && EditorApplication.timeSinceStartup < _saveDueTime)
            {
                return;
            }
            Flush();
        }

        public static void Flush()
        {
            if (_updateHooked)
            {
                EditorApplication.update -= OnEditorUpdate;
                _updateHooked = false;
            }

            if (!_dirty || _data == null)
            {
                return;
            }
            _dirty = false;

            var json = JsonUtility.ToJson(_data, true);
            if (json == _lastSyncedJson)
            {
                return;
            }

            var dir = Path.GetDirectoryName(FullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Set before importing so the postprocessor recognizes this as our own write
            _lastSyncedJson = json;
            File.WriteAllText(FullPath, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(RelativePath);
        }

        /// <summary>Called when the settings file was imported. Reloads only if its content differs from what we hold.</summary>
        internal static void OnFileImported()
        {
            if (_data == null || !File.Exists(FullPath))
            {
                return;
            }

            var json = File.ReadAllText(FullPath, Encoding.UTF8);
            if (json == _lastSyncedJson)
            {
                return;
            }

            // Changed externally (VCS update, manual edit, etc.): the file wins over pending edits
            _data = Parse(json);
            _lastSyncedJson = json;
            _dirty = false;
            Version++;
            ShortcutWindow.RepaintAllWindows();
        }
    }

    internal sealed class ShortcutStoragePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var path = ShortcutStorage.RelativePath;
            foreach (var imported in importedAssets)
            {
                if (imported == path)
                {
                    ShortcutStorage.OnFileImported();
                    return;
                }
            }
        }
    }

    public sealed class ShortcutWindow : EditorWindow
    {
        private static readonly List<ShortcutWindow> OpenWindows = new List<ShortcutWindow>();

        private Vector2 _scroll;
        private int _currentPageIndex = 0;
        private ReorderableList _list;
        private ViewMode _viewMode = ViewMode.List;
        private float _iconSize = 64f;
        private int _selectedIconIndex = -1; // For icon view selection
        private int _dataVersion = -1;

        private static List<ShortcutPage> Pages => ShortcutStorage.Pages;
        
        // Performance optimization: cache resolved objects
        private readonly Dictionary<string, UnityEngine.Object> _objectCache = new Dictionary<string, UnityEngine.Object>();
        // Negative cache: GlobalObjectIdentifierToObjectSlow is expensive, so don't retry unresolvable ids on every repaint
        private readonly Dictionary<string, double> _missingRetryTime = new Dictionary<string, double>();
        private const double MissingRetryInterval = 2.0;
        private readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        
        // GUIStyle cache
        private GUIStyle _iconLabelStyle;
        private GUIStyle _listLabelStyle;
        private GUIStyle _iconLabelTextFieldStyle;
        private GUIStyle _listLabelTextFieldStyle;
        private GUIContent _iconViewContent;
        private GUIContent _listViewContent;
        
        // GUIContent cache (with the object it was built for, to detect changes)
        private readonly Dictionary<string, KeyValuePair<UnityEngine.Object, GUIContent>> _contentCache =
            new Dictionary<string, KeyValuePair<UnityEngine.Object, GUIContent>>();
        
        // AssetPreview fetch state
        private readonly Dictionary<string, double> _previewRequestTime = new Dictionary<string, double>();
        private const double PreviewRetryInterval = 0.5;

        [MenuItem("Tools/muShortcut/Shortcuts")]
        private static void Open()
        {
            var window = CreateInstance<ShortcutWindow>();
            window.titleContent = new GUIContent("Shortcuts");
            window.Show();
        }

        internal static void RepaintAllWindows()
        {
            foreach (var window in OpenWindows)
            {
                if (window != null)
                {
                    window.Repaint();
                }
            }
        }

        private void SyncWithStorage()
        {
            var pages = Pages; // may load on first access
            if (_dataVersion == ShortcutStorage.Version)
            {
                return;
            }
            _dataVersion = ShortcutStorage.Version;

            if (pages.Count == 0)
            {
                pages.Add(new ShortcutPage { pageName = "Page 1" });
            }
            
            // Preserve current page index if possible
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, pages.Count - 1);
            
            ClearCaches();
            EnsureList(GetCurrentPage()?.items);
        }

        private void ClearCaches()
        {
            _objectCache.Clear();
            _missingRetryTime.Clear();
            _iconCache.Clear();
            _contentCache.Clear();
            _previewRequestTime.Clear();
        }

        private void InvalidateMissingCache()
        {
            if (_missingRetryTime.Count > 0)
            {
                _missingRetryTime.Clear();
                Repaint();
            }
        }

        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            InvalidateMissingCache();
        }

        private GUIStyle GetIconLabelStyle()
        {
            if (_iconLabelStyle == null)
            {
                _iconLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
            }
            return _iconLabelStyle;
        }

        private GUIStyle GetListLabelStyle()
        {
            if (_listLabelStyle == null)
            {
                _listLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    imagePosition = ImagePosition.ImageLeft
                };
            }
            return _listLabelStyle;
        }

        // One style per view; the text color is updated per item instead of allocating a style per color
        private GUIStyle GetLabelTextFieldStyle(Color color)
        {
            if (_iconLabelTextFieldStyle == null)
            {
                _iconLabelTextFieldStyle = new GUIStyle(EditorStyles.textField)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
            _iconLabelTextFieldStyle.normal.textColor = color;
            _iconLabelTextFieldStyle.focused.textColor = color;
            return _iconLabelTextFieldStyle;
        }

        private GUIStyle GetLabelTextFieldStyleForList(Color color)
        {
            // Icon view uses MiddleCenter, List view uses default (left-aligned)
            if (_listLabelTextFieldStyle == null)
            {
                _listLabelTextFieldStyle = new GUIStyle(EditorStyles.textField);
            }
            _listLabelTextFieldStyle.normal.textColor = color;
            _listLabelTextFieldStyle.focused.textColor = color;
            return _listLabelTextFieldStyle;
        }

        private void OnGUI()
        {
            SyncWithStorage();

            var windowDropArea = new Rect(0, 0, position.width, position.height);
            HandleDragAndDrop(windowDropArea);

            // Handle Delete key
            HandleDeleteKey();

            // Page navigation UI
            DrawPageNavigation();

            // View mode and toolbar
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var items = GetCurrentPage()?.items;
            if (items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox("No shortcuts yet. Drag objects here.", MessageType.Info);
            }
            else
            {
                switch (_viewMode)
                {
                    case ViewMode.Icon:
                        DrawIconView(items);
                        break;
                    case ViewMode.List:
                        DrawListView(items);
                        break;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleDeleteKey()
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Delete && !EditorGUIUtility.editingTextField)
            {
                var currentPage = GetCurrentPage();
                if (currentPage == null || currentPage.items.Count == 0)
                {
                    return;
                }

                if (_viewMode == ViewMode.List && _list != null)
                {
                    // For list view, use ReorderableList's selection
                    if (_list.index >= 0 && _list.index < currentPage.items.Count)
                    {
                        currentPage.items.RemoveAt(_list.index);
                        ShortcutStorage.MarkDirty();
                        if (_list.index >= currentPage.items.Count)
                        {
                            _list.index = currentPage.items.Count - 1;
                        }
                        evt.Use();
                    }
                }
                else if (_viewMode == ViewMode.Icon)
                {
                    // For icon view, delete the selected item
                    if (_selectedIconIndex >= 0 && _selectedIconIndex < currentPage.items.Count)
                    {
                        currentPage.items.RemoveAt(_selectedIconIndex);
                        ShortcutStorage.MarkDirty();
                        if (_selectedIconIndex >= currentPage.items.Count)
                        {
                            _selectedIconIndex = currentPage.items.Count - 1;
                        }
                        evt.Use();
                    }
                }
            }
        }

        private void DrawPageNavigation()
        {
            Rect pageLabelRect = default;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Left arrow button (loop: first page -> last page)
                EditorGUI.BeginDisabledGroup(Pages.Count == 0);
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    if (Pages.Count > 0)
                    {
                        _currentPageIndex = _currentPageIndex > 0 ? _currentPageIndex - 1 : Pages.Count - 1;
                        EnsureList(GetCurrentPage()?.items);
                    }
                }
                EditorGUI.EndDisabledGroup();

                // Page number display (right-click: context menu to add/delete page)
                GUILayout.Label($"{_currentPageIndex + 1}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));
                pageLabelRect = GUILayoutUtility.GetLastRect();

                // Right arrow button (loop: last page -> first page)
                EditorGUI.BeginDisabledGroup(Pages.Count == 0);
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    if (Pages.Count > 0)
                    {
                        _currentPageIndex = _currentPageIndex < Pages.Count - 1 ? _currentPageIndex + 1 : 0;
                        EnsureList(GetCurrentPage()?.items);
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            // Page number area: right-click context menu (add page / delete page)
            if (Event.current.type == EventType.ContextClick && pageLabelRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Page"), false, () =>
                {
                    var newPageName = $"Page {Pages.Count + 1}";
                    Pages.Add(new ShortcutPage { pageName = newPageName });
                    _currentPageIndex = Pages.Count - 1;
                    ShortcutStorage.MarkDirty();
                    EnsureList(GetCurrentPage()?.items);
                });
                menu.AddSeparator("");
                if (Pages.Count > 1)
                {
                    menu.AddItem(new GUIContent("Delete Page"), false, () =>
                    {
                        Pages.RemoveAt(_currentPageIndex);
                        if (_currentPageIndex >= Pages.Count)
                        {
                            _currentPageIndex = Pages.Count - 1;
                        }
                        ShortcutStorage.MarkDirty();
                        EnsureList(GetCurrentPage()?.items);
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Delete Page"));
                }
                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (_iconViewContent == null)
                {
                    _iconViewContent = CreateIconViewContent();
                    // List view icon: built-in names vary by Unity version; use fallback to avoid console errors
                    _listViewContent = new GUIContent("≡", "List view");
                }
                var iconContent = _iconViewContent;
                var listContent = _listViewContent;

                if (GUILayout.Toggle(_viewMode == ViewMode.Icon, iconContent, EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    _viewMode = ViewMode.Icon;
                }
                if (GUILayout.Toggle(_viewMode == ViewMode.List, listContent, EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    _viewMode = ViewMode.List;
                    _selectedIconIndex = -1; // Reset selection when switching to list view
                }

                GUILayout.Space(10);

                // Icon size slider (only for icon view)
                if (_viewMode == ViewMode.Icon)
                {
                    GUILayout.Label("Size:", EditorStyles.miniLabel, GUILayout.Width(40));
                    _iconSize = GUILayout.HorizontalSlider(_iconSize, 32f, 128f, GUILayout.Width(100));
                    var sizeLabel = $"{_iconSize:F0}";
                    GUILayout.Label(sizeLabel, EditorStyles.miniLabel, GUILayout.Width(30));
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Label", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    var currentPage = GetCurrentPage();
                    if (currentPage != null)
                    {
                        currentPage.items.Add(new ShortcutItem
                        {
                            itemType = ShortcutItemType.Label,
                            labelText = "Label",
                            labelColor = Color.yellow
                        });
                        ShortcutStorage.MarkDirty();
                    }
                }
            }
        }

        private static GUIContent CreateIconViewContent()
        {
            var iconContent = EditorGUIUtility.IconContent("d_Grid.Default");
            if (iconContent == null || iconContent.image == null)
            {
                iconContent = EditorGUIUtility.IconContent("Grid.Default");
            }
            if (iconContent == null || iconContent.image == null)
            {
                return new GUIContent("■", "Icon view");
            }
            return new GUIContent(iconContent.image, "Icon view");
        }

        private ShortcutPage GetCurrentPage()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= Pages.Count)
            {
                return null;
            }
            return Pages[_currentPageIndex];
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            var evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AddObjects(DragAndDrop.objectReferences);
                }

                evt.Use();
            }
        }

        private void AddObjects(UnityEngine.Object[] objects)
        {
            var currentPage = GetCurrentPage();
            if (currentPage == null)
            {
                return;
            }

            var items = currentPage.items;
            var hasChanges = false;

            foreach (var obj in objects)
            {
                if (obj == null)
                {
                    continue;
                }

                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
                if (string.IsNullOrEmpty(globalId))
                {
                    continue;
                }

                if (items.Exists(item => item.globalId == globalId))
                {
                    continue;
                }

                items.Add(new ShortcutItem
                {
                    globalId = globalId,
                    displayName = obj.name
                });
                // We already have the object; avoid resolving it again via GlobalObjectIdentifierToObjectSlow
                _objectCache[globalId] = obj;
                _missingRetryTime.Remove(globalId);
                hasChanges = true;
            }

            if (hasChanges)
            {
                ShortcutStorage.MarkDirty();
            }
        }

        private void RemoveMissing(List<ShortcutItem> items)
        {
            items.RemoveAll(item =>
                item.itemType == ShortcutItemType.Object &&
                ResolveObject(item.globalId) == null);
        }

        private UnityEngine.Object ResolveObject(string globalId)
        {
            if (string.IsNullOrEmpty(globalId))
            {
                return null;
            }

            // Use cache to avoid expensive GlobalObjectId resolution every frame
            if (_objectCache.TryGetValue(globalId, out var cachedObj))
            {
                // Verify the cached object is still valid
                if (cachedObj != null)
                {
                    return cachedObj;
                }
                // Object was destroyed, remove from cache
                _objectCache.Remove(globalId);
            }

            // Unresolvable ids (unloaded scene, deleted asset) are retried only occasionally
            var now = EditorApplication.timeSinceStartup;
            if (_missingRetryTime.TryGetValue(globalId, out var retryTime) && now < retryTime)
            {
                return null;
            }

            if (!GlobalObjectId.TryParse(globalId, out var id))
            {
                _missingRetryTime[globalId] = double.MaxValue;
                return null;
            }

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            if (obj != null)
            {
                _objectCache[globalId] = obj;
                _missingRetryTime.Remove(globalId);
            }
            else
            {
                _missingRetryTime[globalId] = now + MissingRetryInterval;
            }
            return obj;
        }

        private GUIContent BuildContent(ShortcutItem item, UnityEngine.Object obj)
        {
            var cacheKey = item.globalId;
            
            // Reuse while it was built for the same object (or both missing)
            if (_contentCache.TryGetValue(cacheKey, out var cached) && ReferenceEquals(cached.Key, obj))
            {
                return cached.Value;
            }
            
            GUIContent content;
            if (obj != null)
            {
                var raw = EditorGUIUtility.ObjectContent(obj, obj.GetType());
                content = new GUIContent(raw.text, raw.image, raw.tooltip);
            }
            else
            {
                var label = string.IsNullOrEmpty(item.displayName) ? "Missing" : item.displayName;
                content = new GUIContent(label + " (Missing)");
            }
            
            _contentCache[cacheKey] = new KeyValuePair<UnityEngine.Object, GUIContent>(obj, content);
            return content;
        }

        private void OnEnable()
        {
            OpenWindows.Add(this);
            _dataVersion = -1;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.projectChanged += InvalidateMissingCache;
        }

        private void OnDisable()
        {
            OpenWindows.Remove(this);
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorApplication.projectChanged -= InvalidateMissingCache;
            ClearCaches();
            ShortcutStorage.Flush();
        }

        private void DrawIconView(List<ShortcutItem> items)
        {
            var padding = 5f;
            var spacing = 10f;
            var availableWidth = position.width - padding * 2;
            var columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (_iconSize + spacing)));
            var itemHeight = _iconSize + 20f;

            // Reset selection if out of bounds
            if (_selectedIconIndex >= items.Count)
            {
                _selectedIconIndex = -1;
            }

            // Calculate visible range based on scroll position
            var totalRows = Mathf.CeilToInt((float)items.Count / columns);
            var startRow = Mathf.Max(0, Mathf.FloorToInt((_scroll.y - padding) / itemHeight) - 1);
            var endRow = Mathf.Min(totalRows, Mathf.CeilToInt((_scroll.y + position.height - padding) / itemHeight) + 1);
            var startIndex = startRow * columns;
            var endIndex = Mathf.Min(items.Count, endRow * columns);

            // Only draw visible items
            for (int i = startIndex; i < endIndex; i++)
            {
                var item = items[i];
                var row = i / columns;
                var col = i % columns;
                var x = padding + col * (_iconSize + spacing);
                var y = row * itemHeight;

                var itemRect = new Rect(x, y, _iconSize, itemHeight);
                
                // Only draw if visible in scroll view
                if (itemRect.yMax >= _scroll.y && itemRect.y <= _scroll.y + position.height)
                {
                    DrawIconItem(itemRect, item, i);
                }
            }
        }

        private void DrawIconItem(Rect rect, ShortcutItem item, int index)
        {
            var evt = Event.current;

            if (item.itemType == ShortcutItemType.Label)
            {
                var colorRect = new Rect(rect.x, rect.y, rect.width, 16f);
                var textRect = new Rect(rect.x, rect.y + 18f, rect.width, 20f);

                EditorGUI.BeginChangeCheck();
                var nextColor = EditorGUI.ColorField(colorRect, GUIContent.none, item.labelColor, false, false, false);
                var style = GetLabelTextFieldStyle(item.labelColor);
                var nextText = EditorGUI.TextField(textRect, item.labelText, style);
                if (EditorGUI.EndChangeCheck())
                {
                    item.labelColor = nextColor;
                    item.labelText = nextText;
                    ShortcutStorage.MarkDirty();
                }
            }
            else
            {
                var obj = ResolveObject(item.globalId);
                var iconRect = new Rect(rect.x, rect.y, rect.width, _iconSize);
                var labelRect = new Rect(rect.x, rect.y + _iconSize + 2f, rect.width, 18f);

                var prevColor = GUI.color;
                if (obj == null)
                {
                    GUI.color = Color.gray;
                }

                // Draw icon (with caching)
                Texture2D icon = null;
                string displayName = item.displayName;
                if (obj != null)
                {
                    // Use cache to avoid expensive AssetPreview calls every frame
                    if (_iconCache.TryGetValue(item.globalId, out var cachedIcon))
                    {
                        icon = cachedIcon;
                    }
                    else
                    {
                        // Check retry interval
                        var now = EditorApplication.timeSinceStartup;
                        var shouldRetry = !_previewRequestTime.TryGetValue(item.globalId, out var lastTime) ||
                                         (now - lastTime) >= PreviewRetryInterval;
                        
                        if (shouldRetry)
                        {
                            _previewRequestTime[item.globalId] = now;
                            
                            // Prefer GetMiniThumbnail (available immediately)
                            icon = AssetPreview.GetMiniThumbnail(obj);
                            if (icon != null)
                            {
                                _iconCache[item.globalId] = icon;
                            }
                            else
                            {
                                // GetAssetPreview is async; use if ready
                                icon = AssetPreview.GetAssetPreview(obj);
                                if (icon != null)
                                {
                                    _iconCache[item.globalId] = icon;
                                }
                            }
                        }
                    }
                    displayName = obj.name;
                }

                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f));
                }

                // Draw label
                GUI.Label(labelRect, displayName, GetIconLabelStyle());

                // Draw selection highlight
                if (_selectedIconIndex == index)
                {
                    var selectionRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                    EditorGUI.DrawRect(selectionRect, new Color(0.3f, 0.5f, 1f, 0.3f));
                    var borderRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                    EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, borderRect.width, 2), new Color(0.3f, 0.5f, 1f));
                    EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.yMax - 2, borderRect.width, 2), new Color(0.3f, 0.5f, 1f));
                    EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, 2, borderRect.height), new Color(0.3f, 0.5f, 1f));
                    EditorGUI.DrawRect(new Rect(borderRect.xMax - 2, borderRect.y, 2, borderRect.height), new Color(0.3f, 0.5f, 1f));
                }

                // Click detection
                if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
                {
                    if (evt.clickCount == 2)
                    {
                        if (obj != null)
                        {
                            var path = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                            {
                                AssetDatabase.OpenAsset(obj);
                                evt.Use();
                            }
                        }
                    }
                    else if (evt.clickCount == 1)
                    {
                        _selectedIconIndex = index;
                        if (obj != null)
                        {
                            SelectObject(obj);
                        }
                        else
                        {
                            Debug.LogWarning($"mushortcut: Target not found (unloaded scene/deleted): {item.displayName}");
                        }
                        evt.Use();
                    }
                }

                GUI.color = prevColor;
            }

            // Right-click menu
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete shortcut"), false, () =>
                {
                    var currentPage = GetCurrentPage();
                    if (currentPage != null)
                    {
                        currentPage.items.RemoveAt(index);
                        ShortcutStorage.MarkDirty();
                    }
                });
                menu.ShowAsContext();
                evt.Use();
            }
        }

        private void DrawListView(List<ShortcutItem> items)
        {
            EnsureList(items);
            if (_list != null)
            {
                _list.DoLayoutList();
            }
        }


        private void EnsureList(List<ShortcutItem> items)
        {
            if (items == null)
            {
                _list = null;
                return;
            }

            if (_list != null && _list.list == items)
            {
                return;
            }

            _list = new ReorderableList(items, typeof(ShortcutItem), true, false, false, false)
            {
                elementHeight = 18f
            };
            _list.drawElementCallback = DrawElement;
            _list.onReorderCallback = _ => ShortcutStorage.MarkDirty();
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var currentPage = GetCurrentPage();
            if (currentPage == null)
            {
                return;
            }

            var items = currentPage.items;
            if (index < 0 || index >= items.Count)
            {
                return;
            }

            var item = items[index];

            var contentRect = new Rect(rect.x, rect.y + 1, rect.width - 4f, rect.height - 2);
            var evt = Event.current;

            if (item.itemType == ShortcutItemType.Label)
            {
                const float iconSize = 16f;
                var colorRect = new Rect(
                    contentRect.x,
                    contentRect.y + (contentRect.height - iconSize) * 0.5f,
                    iconSize,
                    iconSize);
                var textRect = new Rect(colorRect.xMax + 4, contentRect.y, contentRect.width - iconSize - 4f, contentRect.height);

                EditorGUI.BeginChangeCheck();
                var nextColor = EditorGUI.ColorField(colorRect, GUIContent.none, item.labelColor, false, false, false);
                var style = GetLabelTextFieldStyleForList(item.labelColor);
                var nextText = EditorGUI.TextField(textRect, item.labelText, style);
                if (EditorGUI.EndChangeCheck())
                {
                    item.labelColor = nextColor;
                    item.labelText = nextText;
                    ShortcutStorage.MarkDirty();
                }
            }
            else
            {
                var obj = ResolveObject(item.globalId);
                var content = BuildContent(item, obj);
                var prevColor = GUI.color;
                if (obj == null)
                {
                    GUI.color = Color.gray;
                }
                
                // Click detection
                if (evt.type == EventType.MouseDown && evt.button == 0 && contentRect.Contains(evt.mousePosition))
                {
                    if (evt.clickCount == 2)
                    {
                        // Double click: open asset
                        if (obj != null)
                        {
                            var path = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                            {
                                AssetDatabase.OpenAsset(obj);
                                evt.Use();
                                GUI.color = prevColor;
                                return;
                            }
                        }
                    }
                    else if (evt.clickCount == 1)
                    {
                        // Single click: select object
                        if (obj != null)
                        {
                            SelectObject(obj);
                        }
                        else
                        {
                            Debug.LogWarning($"mushortcut: Target not found (unloaded scene/deleted): {item.displayName}");
                        }
                        evt.Use();
                    }
                }
                
                GUI.Label(contentRect, content, GetListLabelStyle());
                GUI.color = prevColor;
            }

            // Right-click menu
            if (evt.type == EventType.ContextClick && contentRect.Contains(evt.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete shortcut"), false, () =>
                {
                    if (currentPage != null)
                    {
                        currentPage.items.RemoveAt(index);
                        ShortcutStorage.MarkDirty();
                    }
                });
                menu.ShowAsContext();
                evt.Use();
            }
        }

        private static void SelectObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = obj;
                
                // Select in tree side first, then show folder contents in next frame
                TrySelectFolderInTree(obj);
                var instanceId = obj.GetInstanceID();
                EditorApplication.delayCall += () =>
                {
                    TryShowFolderContents(instanceId);
                };
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        private static void TrySelectFolderInTree(UnityEngine.Object obj)
        {
            var projectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null)
            {
                return;
            }

            var windows = Resources.FindObjectsOfTypeAll(projectBrowserType);
            foreach (var window in windows)
            {
                // Try method to select in tree side
                var frameObjectMethod = projectBrowserType.GetMethod(
                    "FrameObject",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int), typeof(bool), typeof(bool) },
                    null);

                if (frameObjectMethod != null)
                {
                    // Set second argument to false to select in tree side
                    frameObjectMethod.Invoke(window, new object[] { obj.GetInstanceID(), false, false });
                }
                
                // Additional processing to select in tree side
                var setSelectionMethod = projectBrowserType.GetMethod(
                    "SetSelection",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int[]) },
                    null);

                if (setSelectionMethod != null)
                {
                    setSelectionMethod.Invoke(window, new object[] { new[] { obj.GetInstanceID() } });
                }
            }
        }

        private static void TryShowFolderContents(int instanceId)
        {
            var projectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null)
            {
                return;
            }

            var showMethod = projectBrowserType.GetMethod(
                "ShowFolderContents",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(bool) },
                null);

            if (showMethod == null)
            {
                return;
            }

            var windows = Resources.FindObjectsOfTypeAll(projectBrowserType);
            foreach (var window in windows)
            {
                showMethod.Invoke(window, new object[] { instanceId, true });
            }
        }
    }
}

