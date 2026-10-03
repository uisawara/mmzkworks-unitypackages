using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>Shows a GameObject's components and the objects their fields reference as a graph.</summary>
    public sealed class RefGraphWindow : EditorWindow
    {
        private const float ToolbarHeight = 18f;
        private static readonly Vector2 GraphOrigin = new Vector2(10f, ToolbarHeight + 2f);

        [SerializeField] private GameObject _target;
        [SerializeField] private bool _groupByAssembly;
        [SerializeField] private bool _includeChildren;
        [SerializeField] private bool _followSelection;

        private RefGraph _graph;
        private bool _graphDirty = true;
        private bool _needsCentering = true;
        private readonly RefGraphViewState _state = new RefGraphViewState();
        // 手動配置はモードごとに保持し、切り替えで混ざらないようにする
        private readonly Dictionary<string, Vector2> _offsetsComponentOrder = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _offsetsByAssembly = new Dictionary<string, Vector2>();

        private RefGraphLayoutMode LayoutMode =>
            _groupByAssembly ? RefGraphLayoutMode.ByAssembly : RefGraphLayoutMode.ComponentOrder;

        public static void OpenFor(GameObject go)
        {
            var w = GetWindow<RefGraphWindow>(false, "Ref Graph", true);
            w.minSize = new Vector2(720, 420);
            w.SetTarget(go);
        }

        private void SetTarget(GameObject go)
        {
            if (_target != go)
            {
                _target = go;
                _offsetsComponentOrder.Clear();
                _offsetsByAssembly.Clear();
                _state.SelectedNodes.Clear();
                _needsCentering = true;
            }
            _graphDirty = true;
            Repaint();
        }

        private void OnEnable()
        {
            // ホバーハイライトのため MouseMove を受け取る
            wantsMouseMove = true;
            EditorApplication.hierarchyChanged += MarkDirty;
            Undo.undoRedoPerformed += MarkDirty;
            Selection.selectionChanged += OnSelectionChanged;
            _graphDirty = true;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkDirty;
            Undo.undoRedoPerformed -= MarkDirty;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnFocus() => MarkDirty();

        private void MarkDirty()
        {
            _graphDirty = true;
            Repaint();
        }

        private void OnSelectionChanged()
        {
            if (!_followSelection)
                return;
            var go = Selection.activeGameObject;
            if (go != null && go != _target)
                SetTarget(go);
        }

        private void OnGUI()
        {
            if (_graphDirty)
            {
                _graph = _target != null ? RefGraphBuilder.Build(_target, _includeChildren) : null;
                _graphDirty = false;
            }

            var e = Event.current;
            if (e.type == EventType.MouseMove)
                Repaint();
            RefGraphLayoutResult layout = null;
            if (_target != null && _graph != null)
            {
                var offsets = CurrentOffsets();
                _state.NodeOffsets = offsets;

                if (_needsCentering && position.width > 0f && CenterView())
                    _needsCentering = false;
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Home && CenterView())
                {
                    e.Use();
                    Repaint();
                }

                layout = RefGraphLayout.Compute(_graph, LayoutMode, offsets, _state.PanOffset, _state.ZoomLevel, GraphOrigin);
            }

            RefGraphInputHandler.Handle(e, _state, layout?.Rects, GraphOrigin, OpenNode, Repaint);

            if (layout != null)
            {
                // 入力でパン/ズーム/オフセットが変わっている可能性があるため描画用に再計算
                layout = RefGraphLayout.Compute(_graph, LayoutMode, _state.NodeOffsets, _state.PanOffset, _state.ZoomLevel, GraphOrigin);
                if (_groupByAssembly)
                    RefGraphDrawer.DrawGroups(layout.Groups, _state.ZoomLevel);
                RefGraphDrawer.DrawGraph(_graph, layout.Rects, e.mousePosition,
                    _state.SelectedNodes, _state.IsMarqueeSelecting, _state.SelectionBoxStart, _state.ZoomLevel);
            }
            else
            {
                GUILayout.Space(ToolbarHeight + 4f);
                EditorGUILayout.HelpBox(
                    "No target. Open from GameObject/muRefgraph in the Hierarchy, Assets/Open Ref Graph on a prefab, or enable Follow Selection.",
                    MessageType.Info);
            }

            // ツールバーを最後に描画して前面に表示
            DrawToolbar();
        }

        private Dictionary<string, Vector2> CurrentOffsets() =>
            _groupByAssembly ? _offsetsByAssembly : _offsetsComponentOrder;

        private void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(0, 0, position.width, ToolbarHeight));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var targetName = _target != null ? _target.name : "(no target)";
                if (_graph != null && _target != null)
                {
                    int children = 0, components = 0, references = 0;
                    foreach (var n in _graph.Nodes)
                    {
                        if (n.Kind == RefGraphNodeKind.Reference) references++;
                        else if (n.Kind == RefGraphNodeKind.ChildObject) children++;
                        else if (n.IsComponent) components++;
                    }
                    if (_includeChildren)
                        targetName += $"   Children: {children}";
                    targetName += $"   Components: {components}  References: {references}";
                }
                GUILayout.Label(targetName, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                var includeChildren = GUILayout.Toggle(_includeChildren, "Include Children", EditorStyles.toolbarButton, GUILayout.Width(110));
                if (includeChildren != _includeChildren)
                {
                    _includeChildren = includeChildren;
                    _state.SelectedNodes.Clear();
                    _needsCentering = true;
                    MarkDirty();
                }

                var groupByAssembly = GUILayout.Toggle(_groupByAssembly, "Group by Assembly", EditorStyles.toolbarButton, GUILayout.Width(120));
                if (groupByAssembly != _groupByAssembly)
                {
                    _groupByAssembly = groupByAssembly;
                    _state.SelectedNodes.Clear();
                    _needsCentering = true;
                    Repaint();
                }

                var follow = GUILayout.Toggle(_followSelection, "Follow Selection", EditorStyles.toolbarButton, GUILayout.Width(110));
                if (follow != _followSelection)
                {
                    _followSelection = follow;
                    if (_followSelection)
                        OnSelectionChanged();
                }

                if (GUILayout.Button("Reset Layout", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    CurrentOffsets().Clear();
                    _state.ZoomLevel = 1f;
                    _needsCentering = true;
                    Repaint();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    MarkDirty();
            }
            GUILayout.EndArea();
        }

        /// <summary>Pans so that the bounding box of all nodes is centered in the window.</summary>
        private bool CenterView()
        {
            var layout = RefGraphLayout.Compute(_graph, LayoutMode, CurrentOffsets(), Vector2.zero, _state.ZoomLevel, GraphOrigin);
            bool any = false;
            Rect bounds = default;
            foreach (var r in layout.Rects.Values)
            {
                bounds = any ? Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                    Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax)) : r;
                any = true;
            }
            foreach (var g in layout.Groups)
            {
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, g.Rect.xMin), Mathf.Min(bounds.yMin, g.Rect.yMin),
                    Mathf.Max(bounds.xMax, g.Rect.xMax), Mathf.Max(bounds.yMax, g.Rect.yMax));
            }
            if (!any)
                return false;
            var viewCenter = new Vector2(position.width * 0.5f, (position.height + ToolbarHeight) * 0.5f);
            _state.PanOffset = viewCenter - bounds.center;
            return true;
        }

        private void OpenNode(string nodeId)
        {
            if (_graph == null || !_graph.TryGetNode(nodeId, out var node) || node.Target == null)
                return;
            Selection.activeObject = node.Target;
            EditorGUIUtility.PingObject(node.Target);
        }
    }
}
