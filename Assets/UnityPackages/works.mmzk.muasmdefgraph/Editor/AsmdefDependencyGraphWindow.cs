using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    public sealed class ComponentAsmdefGraphWindow : EditorWindow
    {
        private GameObject _target;
        private HashSet<string> _rootAsmdefNames;
        private Vector2 _panOffset = Vector2.zero;
        private bool _isPanning;
        private Vector2 _lastMouse;
        private Dictionary<string, Vector2> _nodeOffsets = new Dictionary<string, Vector2>(256);
        private string _draggedNode;
        private Vector2 _dragStartMouse;
        private Vector2 _dragStartOffset;
        private float _zoomLevel = 1.0f;
        private HashSet<string> _lowInterestNodes = new HashSet<string>();
        private bool _showDllNodes = true;
        private bool _showUnresolvedNodes;
        private string _lastClickedNode;
        private float _lastClickTime;
        private string _lastLoadedRootKey;
        private bool _hasCenteredForCurrentRoot;
        private HashSet<string> _selectedNodes;
        private Vector2 _selectionBoxStart;
        private bool _isMarqueeSelecting;
        private Dictionary<string, Vector2> _dragStartOffsetsForSelected;
        private readonly List<AsmdefGraphCommentBlock> _commentBlocks = new List<AsmdefGraphCommentBlock>();
        private HashSet<string> _selectedCommentIds;
        private bool _isCreatingComment;
        private Vector2 _commentCreateStart;
        private string _draggingCommentId;
        private string _resizingCommentId;
        private CommentCorner _resizingCorner;
        private Rect _dragStartCommentRect;
        private Dictionary<string, Rect> _dragStartCommentRects;
        private string _renamingCommentId;
        private string _renameBuffer;
        private string _renameOriginalLabel;
        private bool _focusRenameField;
        private bool _selectAllRenameField;

        private AsmdefGraphInputContext _inputContext;

        private const float ToolbarHeight = 18f;
        private const float GraphStartX = 10f;
        private static readonly Vector2 GraphOrigin = new Vector2(GraphStartX, ToolbarHeight + 2f);
        private const string CommentRenameControlName = "muAsmdefgraph.CommentRename";

        public static void OpenFor(GameObject go)
        {
            var w = GetWindow<ComponentAsmdefGraphWindow>(false, "Component Asmdef Graph", true);
            w._target = go;
            w._rootAsmdefNames = null;
            w.minSize = new Vector2(720, 420);
            AsmdefDatabase.Refresh();
            w.Repaint();
        }

        public static void OpenForAsmdef(string asmdefPath)
        {
            var asmdefName = AsmdefPathHelper.ResolveAsmdefNameFromPath(asmdefPath);
            if (string.IsNullOrEmpty(asmdefName))
            {
                EditorUtility.DisplayDialog("Error", $"Failed to resolve asmdef name from path: {asmdefPath}", "OK");
                return;
            }

            OpenForAsmdefName(asmdefName);
        }

        public static void OpenForAsmdefName(string asmdefName)
        {
            var w = GetWindow<ComponentAsmdefGraphWindow>(false, "Component Asmdef Graph", true);
            w._target = null;
            w._rootAsmdefNames = new HashSet<string> { asmdefName };
            w.minSize = new Vector2(720, 420);
            AsmdefDatabase.Refresh();
            w.Repaint();
        }

        public static HashSet<string> GetAsmdefNamesForGameObject(GameObject go) => CollectRootAsmdefNames(go);

        private void OnEnable()
        {
            _showUnresolvedNodes = AsmdefDatabase.ShowUnresolvedNodes;
        }

        private void OnGUI()
        {
            var roots = GetRootAsmdefNames();

            if (_inputContext == null)
                _inputContext = new AsmdefGraphInputContext();

            _inputContext.Event = Event.current;
            _inputContext.PanOffset = _panOffset;
            _inputContext.ZoomLevel = _zoomLevel;
            _inputContext.NodeOffsets = _nodeOffsets;
            _inputContext.DraggedNode = _draggedNode;
            _inputContext.DragStartMouse = _dragStartMouse;
            _inputContext.DragStartOffset = _dragStartOffset;
            _inputContext.IsPanning = _isPanning;
            _inputContext.LastMouse = _lastMouse;
            _inputContext.LastClickedNode = _lastClickedNode;
            _inputContext.LastClickTime = _lastClickTime;
            _inputContext.LowInterestNodes = _lowInterestNodes;
            _inputContext.SelectedNodes = _selectedNodes;
            _inputContext.SelectionBoxStart = _selectionBoxStart;
            _inputContext.IsMarqueeSelecting = _isMarqueeSelecting;
            _inputContext.DragStartOffsetsForSelected = _dragStartOffsetsForSelected;
            _inputContext.ToolbarHeight = ToolbarHeight;
            _inputContext.GraphOrigin = GraphOrigin;
            _inputContext.CommentBlocks = _commentBlocks;
            _inputContext.SelectedCommentIds = _selectedCommentIds;
            _inputContext.IsCreatingComment = _isCreatingComment;
            _inputContext.CommentCreateStart = _commentCreateStart;
            _inputContext.DraggingCommentId = _draggingCommentId;
            _inputContext.ResizingCommentId = _resizingCommentId;
            _inputContext.ResizingCorner = _resizingCorner;
            _inputContext.DragStartCommentRect = _dragStartCommentRect;
            _inputContext.DragStartCommentRects = _dragStartCommentRects;
            _inputContext.RenamingCommentId = _renamingCommentId;
            _inputContext.RenameBuffer = _renameBuffer;
            _inputContext.RenameOriginalLabel = _renameOriginalLabel;
            _inputContext.FocusRenameField = _focusRenameField;
            _inputContext.SelectAllRenameField = _selectAllRenameField;
            _inputContext.Repaint = Repaint;
            _inputContext.Save = SaveNodeOffsets;
            _inputContext.SelectAssetInProject = ProjectViewSelectionHelper.SelectAsmdefInProject;

            if (roots == null || roots.Count == 0)
            {
                _inputContext.Rects = null;
                AsmdefGraphInputHandler.Handle(_inputContext);
                SyncInputContextBack();
                GUILayout.Space(ToolbarHeight); // ツールバー分のスペースを確保
                DrawNoRootsHelp();
                DrawToolbarOverlay(roots, (0, 0));
                return;
            }

            var rootKey = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots);
            if (rootKey != _lastLoadedRootKey)
            {
                AsmdefGraphNodeOffsetPersistence.LoadForRoot(rootKey, _nodeOffsets, _lowInterestNodes, _commentBlocks);
                _lastLoadedRootKey = rootKey;
                _hasCenteredForCurrentRoot = false;
                _selectedCommentIds = new HashSet<string>();
                _renamingCommentId = null;
                _isCreatingComment = false;
                _inputContext.SelectedCommentIds = _selectedCommentIds;
                _inputContext.RenamingCommentId = _renamingCommentId;
                _inputContext.IsCreatingComment = _isCreatingComment;
                _inputContext.CommentBlocks = _commentBlocks;
            }

            var stats = AsmdefGraphBuilder.ComputeDependencyStats(roots);
            var levels = AsmdefGraphBuilder.BuildLevels(roots, maxDepth: 6, _showDllNodes, _showUnresolvedNodes);

            // 表示開始時（ルート読み込み直後）は high interest なノード群の中央がウィンドウ中央に来るようにパンオフセットを設定
            if (!_hasCenteredForCurrentRoot && levels.Count > 0)
            {
                var rectsForBounds = AsmdefGraphLayout.ComputeRects(levels, _nodeOffsets, Vector2.zero, _zoomLevel,
                    startX: GraphOrigin.x, startY: GraphOrigin.y);
                if (CenterViewOnNodes(rectsForBounds, _lowInterestNodes))
                    _hasCenteredForCurrentRoot = true;
            }

            var rects = AsmdefGraphLayout.ComputeRects(levels, _nodeOffsets, _panOffset, _zoomLevel,
                startX: GraphOrigin.x, startY: GraphOrigin.y);

            // Home: high interest ノード群をウィンドウ中央に合わせる（手動センタリング）。常にパン=0 の rects で計算して一律センタリングする。
            var ev = Event.current;
            bool isRenaming = !string.IsNullOrEmpty(_renamingCommentId);
            if (!isRenaming && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Home)
            {
                var rectsForHome = AsmdefGraphLayout.ComputeRects(levels, _nodeOffsets, Vector2.zero, _zoomLevel,
                    startX: GraphOrigin.x, startY: GraphOrigin.y);
                if (CenterViewOnNodes(rectsForHome, _lowInterestNodes))
                {
                    ev.Use();
                    Repaint();
                }
            }
            if (!isRenaming && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Delete || ev.keyCode == KeyCode.Backspace))
            {
                if (_selectedCommentIds != null && _selectedCommentIds.Count > 0)
                {
                    _commentBlocks.RemoveAll(c => c != null && _selectedCommentIds.Contains(c.Id));
                    _selectedCommentIds.Clear();
                    SaveNodeOffsets();
                    ev.Use();
                    Repaint();
                }
            }

            _inputContext.Rects = rects;
            AsmdefGraphInputHandler.Handle(_inputContext);
            SyncInputContextBack();
            DrawNoRootsHelp();

            var mouse = Event.current?.mousePosition ?? Vector2.zero;
            AsmdefGraphDrawer.DrawCommentBodies(
                _commentBlocks, GraphOrigin, _panOffset, _zoomLevel, _selectedCommentIds,
                _isCreatingComment, _commentCreateStart, mouse, _renamingCommentId);
            AsmdefGraphDrawer.DrawCommentChrome(
                _commentBlocks, GraphOrigin, _panOffset, _zoomLevel, _selectedCommentIds, _renamingCommentId);
            AsmdefGraphDrawer.DrawGraph(rects, roots, _lowInterestNodes, mouse,
                _selectedNodes, _isMarqueeSelecting, _selectionBoxStart);
            AsmdefGraphDrawer.DrawCommentColorButtons(_commentBlocks, GraphOrigin, _panOffset, _zoomLevel);
            DrawCommentRenameField();

            // ツールバーを最後に描画して前面に表示
            DrawToolbarOverlay(roots, stats);

            if (!string.IsNullOrEmpty(_renamingCommentId))
                Repaint();
        }

        private void SyncInputContextBack()
        {
            if (_inputContext == null) return;
            _panOffset = _inputContext.PanOffset;
            _zoomLevel = _inputContext.ZoomLevel;
            _draggedNode = _inputContext.DraggedNode;
            _dragStartMouse = _inputContext.DragStartMouse;
            _dragStartOffset = _inputContext.DragStartOffset;
            _isPanning = _inputContext.IsPanning;
            _lastMouse = _inputContext.LastMouse;
            _lastClickedNode = _inputContext.LastClickedNode;
            _lastClickTime = _inputContext.LastClickTime;
            _selectedNodes = _inputContext.SelectedNodes;
            _selectionBoxStart = _inputContext.SelectionBoxStart;
            _isMarqueeSelecting = _inputContext.IsMarqueeSelecting;
            _dragStartOffsetsForSelected = _inputContext.DragStartOffsetsForSelected;
            _selectedCommentIds = _inputContext.SelectedCommentIds;
            _isCreatingComment = _inputContext.IsCreatingComment;
            _commentCreateStart = _inputContext.CommentCreateStart;
            _draggingCommentId = _inputContext.DraggingCommentId;
            _resizingCommentId = _inputContext.ResizingCommentId;
            _resizingCorner = _inputContext.ResizingCorner;
            _dragStartCommentRect = _inputContext.DragStartCommentRect;
            _dragStartCommentRects = _inputContext.DragStartCommentRects;
            _renamingCommentId = _inputContext.RenamingCommentId;
            _renameBuffer = _inputContext.RenameBuffer;
            _renameOriginalLabel = _inputContext.RenameOriginalLabel;
            _focusRenameField = _inputContext.FocusRenameField;
            _selectAllRenameField = _inputContext.SelectAllRenameField;
        }

        /// <summary>ツールバーをウィンドウ上部の固定Rectに描画し、グラフより前面に表示する。</summary>
        private void DrawToolbarOverlay(HashSet<string> roots, (int directCount, int totalCount) stats)
        {
            var toolbarRect = new Rect(0, 0, position.width, ToolbarHeight);
            GUILayout.BeginArea(toolbarRect);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string targetName = _target != null ? _target.name : (roots != null && roots.Count > 0 ? string.Join(", ", roots) : "(no target)");
                EditorGUILayout.LabelField(targetName, GUILayout.ExpandWidth(true));

                if (roots != null && roots.Count > 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Direct: {stats.directCount}  Total: {stats.totalCount}", EditorStyles.miniLabel);
                }
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    AsmdefDatabase.Refresh();
                    Repaint();
                }
                var prevDll = _showDllNodes;
                var prevUnresolved = _showUnresolvedNodes;
                _showDllNodes = GUILayout.Toggle(_showDllNodes, "Show DLLs", EditorStyles.toolbarButton, GUILayout.Width(90));
                _showUnresolvedNodes = GUILayout.Toggle(_showUnresolvedNodes, "Show unresolved", EditorStyles.toolbarButton, GUILayout.Width(120));
                if (_showUnresolvedNodes != AsmdefDatabase.ShowUnresolvedNodes)
                    AsmdefDatabase.ShowUnresolvedNodes = _showUnresolvedNodes;
                if (GUI.changed || _showDllNodes != prevDll || _showUnresolvedNodes != prevUnresolved)
                    Repaint();
            }
            GUILayout.EndArea();
        }

        private void DrawNoRootsHelp()
        {
            var roots = GetRootAsmdefNames();
            if (roots != null && roots.Count > 0)
                return;

            if (_target == null && _rootAsmdefNames == null)
                EditorGUILayout.HelpBox("No target GameObject or asmdef.", MessageType.Info);
            else if (_target != null)
            {
                EditorGUILayout.HelpBox("No asmdef found for MonoBehaviours on this GameObject.", MessageType.Info);
                EditorGUILayout.LabelField("(Scripts may be in Assembly-CSharp or unresolved.)");
            }
            else
                EditorGUILayout.HelpBox("Failed to resolve asmdef names.", MessageType.Warning);
        }

        private void SaveNodeOffsets()
        {
            var roots = GetRootAsmdefNames();
            if (roots == null || roots.Count == 0)
                return;
            var rootKey = AsmdefGraphNodeOffsetPersistence.GetRootKey(roots);
            if (string.IsNullOrEmpty(rootKey))
                return;
            AsmdefGraphNodeOffsetPersistence.SaveNodeOffsets(rootKey, _nodeOffsets, _lowInterestNodes, _commentBlocks);
        }

        private void DrawCommentRenameField()
        {
            if (string.IsNullOrEmpty(_renamingCommentId))
                return;
            AsmdefGraphCommentBlock block = null;
            foreach (var c in _commentBlocks)
            {
                if (c != null && c.Id == _renamingCommentId)
                {
                    block = c;
                    break;
                }
            }
            if (block == null)
                return;

            var labelRect = block.GetLabelScreenRect(GraphOrigin, _panOffset, _zoomLevel);
            GUI.SetNextControlName(CommentRenameControlName);
            var text = _renameBuffer ?? "";
            var newText = EditorGUI.TextField(labelRect, text);
            if (newText != text)
            {
                _renameBuffer = newText;
                if (_inputContext != null)
                    _inputContext.RenameBuffer = newText;
            }

            if (_focusRenameField)
            {
                EditorGUI.FocusTextInControl(CommentRenameControlName);
                if (GUI.GetNameOfFocusedControl() == CommentRenameControlName)
                {
                    _focusRenameField = false;
                    if (_inputContext != null)
                        _inputContext.FocusRenameField = false;
                }
            }
            else if (_selectAllRenameField && Event.current.type == EventType.Repaint)
            {
                var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
                if (editor != null)
                {
                    editor.SelectAll();
                    _selectAllRenameField = false;
                    if (_inputContext != null)
                        _inputContext.SelectAllRenameField = false;
                }
            }
        }

        private HashSet<string> GetRootAsmdefNames()
        {
            if (_rootAsmdefNames != null)
                return _rootAsmdefNames;
            if (_target != null)
                return CollectRootAsmdefNames(_target);
            return null;
        }

        /// <summary>表示位置を、指定ノード群（excludeFromBounds で除外しないノード）の中央がウィンドウ中央に来るように設定する。_panOffset と _inputContext.PanOffset の両方を更新する。</summary>
        /// <param name="rects">バウンディング計算に使うノードの Rect。</param>
        /// <param name="excludeFromBounds">バウンディングから除外するノード名（low interest）。null の場合は全ノードで中央寄せ。</param>
        /// <returns>表示位置を更新した場合 true。</returns>
        private bool CenterViewOnNodes(Dictionary<string, Rect> rects, HashSet<string> excludeFromBounds)
        {
            var bounds = GetBoundingRect(rects, excludeFromBounds);
            if (bounds.width <= 0 || bounds.height <= 0)
                bounds = GetBoundingRect(rects);
            if (bounds.width <= 0 || bounds.height <= 0)
                return false;
            var viewCenter = new Vector2(position.width * 0.5f, position.height * 0.5f);
            _panOffset = viewCenter - new Vector2(bounds.xMin + bounds.width * 0.5f, bounds.yMin + bounds.height * 0.5f);
            if (_inputContext != null)
                _inputContext.PanOffset = _panOffset;
            return true;
        }

        /// <summary>全ノードのバウンディング矩形を返す。</summary>
        private static Rect GetBoundingRect(Dictionary<string, Rect> rects)
        {
            return GetBoundingRect(rects, null);
        }

        /// <summary>excludeNodeNames に含まれないノード（high interest）のみでバウンディング矩形を計算する。null の場合は全ノード対象。</summary>
        private static Rect GetBoundingRect(Dictionary<string, Rect> rects, HashSet<string> excludeNodeNames)
        {
            if (rects == null || rects.Count == 0)
                return new Rect(0, 0, 0, 0);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;
            foreach (var kv in rects)
            {
                if (excludeNodeNames != null && excludeNodeNames.Contains(kv.Key))
                    continue;
                var r = kv.Value;
                any = true;
                if (r.xMin < minX) minX = r.xMin;
                if (r.yMin < minY) minY = r.yMin;
                if (r.xMax > maxX) maxX = r.xMax;
                if (r.yMax > maxY) maxY = r.yMax;
            }
            if (!any)
                return new Rect(0, 0, 0, 0);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static HashSet<string> CollectRootAsmdefNames(GameObject go)
        {
            var roots = new HashSet<string>();
            var mbs = go.GetComponents<MonoBehaviour>();
            foreach (var mb in mbs)
            {
                if (mb == null) continue;
                var ms = MonoScript.FromMonoBehaviour(mb);
                if (ms == null) continue;
                var scriptPath = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(scriptPath)) continue;

                var asmdefName = AsmdefPathHelper.ResolveNearestAsmdefNameForScript(scriptPath);
                if (!string.IsNullOrEmpty(asmdefName))
                    roots.Add(asmdefName);
            }
            return roots;
        }
    }
}
