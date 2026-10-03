using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>Draws assembly group frames, edges and nodes. Rounded-rect / bezier helpers are ported from muAsmdefgraph.</summary>
    public static class RefGraphDrawer
    {
        private const int RoundedCornerSegments = 6;
        private const float NodeCornerRadiusAtDefaultHeight = 5f;
        private const float EdgeEndpointCircleRadius = 2.8f;
        private const float SelectedBorderOffset = 1f;
        private const float AssemblyBarWidth = 5f;

        private static readonly Color RootFill = new Color(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color ComponentFill = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color MissingFill = new Color(0.42f, 0.18f, 0.18f, 1f);
        private static readonly Color ReferenceFill = new Color(0.22f, 0.3f, 0.38f, 1f);
        private static readonly Color DefaultBorder = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color HoverBorder = Color.green;
        private static readonly Color ConnectedBorder = Color.yellow;
        private static readonly Color OwnershipEdge = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color ReferenceEdge = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color InternalEdge = new Color(1f, 0.6f, 0.2f, 1f);
        private static readonly Color HighlightEdge = Color.yellow;

        /// <summary>Stable hue in [0,1) derived from the assembly name, used for group frames and component bars.</summary>
        public static float AssemblyHue(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return 0f;
            // FNV-1a: string.GetHashCode is not stable across sessions
            uint hash = 2166136261;
            foreach (var ch in assemblyName)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return (hash % 360) / 360f;
        }

        public static Color AssemblyColor(string assemblyName, float saturation = 0.55f, float value = 0.85f)
        {
            return Color.HSVToRGB(AssemblyHue(assemblyName), saturation, value);
        }

        public static void DrawGroups(List<RefGraphGroupFrame> groups, float zoomLevel)
        {
            if (groups == null || groups.Count == 0)
                return;

            Handles.BeginGUI();
            try
            {
                foreach (var g in groups)
                {
                    var c = AssemblyColor(g.AssemblyName);
                    float radius = Mathf.Min(NodeCornerRadiusAtDefaultHeight * zoomLevel, g.Rect.width * 0.5f, g.Rect.height * 0.5f);
                    Handles.color = new Color(c.r, c.g, c.b, 0.12f);
                    Handles.DrawAAConvexPolygon(BuildRoundedRectPath(g.Rect, radius, close: false));
                    Handles.color = new Color(c.r, c.g, c.b, 0.7f);
                    Handles.DrawAAPolyLine(2f, BuildRoundedRectPath(g.Rect, radius, close: true));
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            foreach (var g in groups)
            {
                var labelRect = new Rect(
                    g.Rect.x + RefGraphLayout.GroupPadding * zoomLevel,
                    g.Rect.y,
                    g.Rect.width - RefGraphLayout.GroupPadding * 2f * zoomLevel,
                    RefGraphLayout.GroupHeaderHeight * zoomLevel);
                GUI.Label(labelRect, new GUIContent(g.AssemblyName, g.AssemblyName), style);
            }
        }

        public static void DrawGraph(
            RefGraph graph,
            Dictionary<string, Rect> rects,
            Vector2 mousePosition,
            HashSet<string> selectedNodes,
            bool isMarqueeSelecting,
            Vector2 selectionBoxStart,
            float zoomLevel)
        {
            if (graph == null || rects == null || rects.Count == 0)
                return;

            var hovered = RefGraphInputHandler.HitTest(rects, mousePosition);
            var connected = new HashSet<string>();
            if (hovered != null)
            {
                foreach (var e in graph.Edges)
                {
                    if (e.FromId == hovered) connected.Add(e.ToId);
                    else if (e.ToId == hovered) connected.Add(e.FromId);
                }
            }

            // Edges first so that nodes are drawn on top of them
            Handles.BeginGUI();
            try
            {
                foreach (var e in graph.Edges)
                {
                    if (!rects.TryGetValue(e.FromId, out var from) || !rects.TryGetValue(e.ToId, out var to))
                        continue;
                    bool highlight = hovered != null && (e.FromId == hovered || e.ToId == hovered);
                    Handles.color = highlight ? HighlightEdge : EdgeColor(graph, e);
                    DrawEdge(from, to);
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            Handles.BeginGUI();
            try
            {
                foreach (var n in graph.Nodes)
                {
                    if (!rects.TryGetValue(n.Id, out var r))
                        continue;
                    DrawRoundedNodeFill(r, NodeFill(n.Kind));
                    if (n.Kind == RefGraphNodeKind.Component || n.Kind == RefGraphNodeKind.MissingScript)
                    {
                        var bar = new Rect(r.x + 2f, r.y + CornerRadius(r), AssemblyBarWidth * zoomLevel, r.height - CornerRadius(r) * 2f);
                        EditorGUI.DrawRect(bar, AssemblyColor(n.AssemblyName));
                    }

                    bool isSelected = selectedNodes != null && selectedNodes.Contains(n.Id);
                    if (isSelected) Handles.color = Color.white;
                    else if (n.Id == hovered) Handles.color = HoverBorder;
                    else if (connected.Contains(n.Id)) Handles.color = ConnectedBorder;
                    else Handles.color = n.Kind == RefGraphNodeKind.Root ? Color.white : DefaultBorder;
                    DrawRoundedNodeBorder(r, isSelected);
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            DrawNodeLabels(graph, rects, zoomLevel);

            if (hovered != null)
                DrawEdgeFieldLabels(graph, rects, hovered, zoomLevel);

            if (isMarqueeSelecting)
            {
                var box = RefGraphInputHandler.MarqueeRect(selectionBoxStart, mousePosition);
                Handles.BeginGUI();
                try
                {
                    Handles.DrawSolidRectangleWithOutline(box, new Color(0.2f, 0.5f, 1f, 0.15f), new Color(0.3f, 0.6f, 1f, 0.8f));
                }
                finally
                {
                    Handles.EndGUI();
                }
            }
        }

        private static void DrawNodeLabels(RefGraph graph, Dictionary<string, Rect> rects, float zoomLevel)
        {
            int baseSize = EditorStyles.label.fontSize > 0 ? EditorStyles.label.fontSize : 12;
            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = Mathf.Max(6, Mathf.RoundToInt(baseSize * Mathf.Min(zoomLevel, 1.5f))),
            };
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = Mathf.Max(5, Mathf.RoundToInt(baseSize * 0.8f * Mathf.Min(zoomLevel, 1.5f))),
            };

            foreach (var n in graph.Nodes)
            {
                if (!rects.TryGetValue(n.Id, out var r))
                    continue;
                float inset = (n.Kind == RefGraphNodeKind.Component || n.Kind == RefGraphNodeKind.MissingScript)
                    ? (AssemblyBarWidth + 4f) * zoomLevel
                    : 4f * zoomLevel;
                float lineHeight = r.height * 0.4f;
                float top = r.y + (r.height - lineHeight * 2f) * 0.5f;
                var tooltip = BuildTooltip(graph, n);
                GUI.Label(new Rect(r.x + inset, top, r.width - inset * 2f, lineHeight), new GUIContent(n.Label, tooltip), nameStyle);
                GUI.Label(new Rect(r.x + inset, top + lineHeight, r.width - inset * 2f, lineHeight), new GUIContent(n.SubLabel, tooltip), subStyle);
            }
        }

        /// <summary>Shows the field names on the edges touching the hovered node.</summary>
        private static void DrawEdgeFieldLabels(RefGraph graph, Dictionary<string, Rect> rects, string hovered, float zoomLevel)
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            var bg = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            foreach (var e in graph.Edges)
            {
                if (e.FieldPaths.Count == 0 || (e.FromId != hovered && e.ToId != hovered))
                    continue;
                if (!rects.TryGetValue(e.FromId, out var from) || !rects.TryGetValue(e.ToId, out var to))
                    continue;
                GetEdgeEndpoints(from, to, out var p1, out var p2, out _, out _);
                var mid = (p1 + p2) * 0.5f;
                var text = string.Join(", ", e.FieldPaths);
                var size = style.CalcSize(new GUIContent(text));
                var rect = new Rect(mid.x - size.x * 0.5f - 3f, mid.y - size.y * 0.5f, size.x + 6f, size.y);
                EditorGUI.DrawRect(rect, bg);
                GUI.Label(rect, text, style);
            }
        }

        private static string BuildTooltip(RefGraph graph, RefGraphNode node)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(node.Tooltip))
                lines.Add(node.Tooltip);
            if (node.Kind == RefGraphNodeKind.Reference)
            {
                foreach (var e in graph.Edges)
                {
                    if (e.ToId != node.Id || !graph.TryGetNode(e.FromId, out var from))
                        continue;
                    foreach (var f in e.FieldPaths)
                        lines.Add($"← {from.Label}.{f}");
                }
            }
            return string.Join("\n", lines);
        }

        private static Color EdgeColor(RefGraph graph, RefGraphEdge e)
        {
            if (e.FromId == RefGraph.RootId)
                return OwnershipEdge;
            if (graph.TryGetNode(e.ToId, out var to) && to.Kind != RefGraphNodeKind.Reference)
                return InternalEdge;
            return ReferenceEdge;
        }

        private static Color NodeFill(RefGraphNodeKind kind)
        {
            switch (kind)
            {
                case RefGraphNodeKind.Root: return RootFill;
                case RefGraphNodeKind.MissingScript: return MissingFill;
                case RefGraphNodeKind.Reference: return ReferenceFill;
                default: return ComponentFill;
            }
        }

        /// <summary>
        /// Left-to-right edges go from the right side to the left side. Backward edges (component -> root) are mirrored,
        /// and edges within the same column (component -> component) bulge out to the right.
        /// </summary>
        private static void GetEdgeEndpoints(Rect from, Rect to, out Vector2 p1, out Vector2 p2, out Vector2 c1, out Vector2 c2)
        {
            float columnTolerance = from.width * 0.5f;
            if (to.center.x > from.center.x + columnTolerance)
            {
                p1 = new Vector2(from.xMax, from.center.y);
                p2 = new Vector2(to.xMin, to.center.y);
                float dist = Mathf.Max((p2.x - p1.x) * 0.35f, 40f);
                c1 = p1 + new Vector2(dist, 0f);
                c2 = p2 - new Vector2(dist, 0f);
            }
            else if (to.center.x < from.center.x - columnTolerance)
            {
                p1 = new Vector2(from.xMin, from.center.y);
                p2 = new Vector2(to.xMax, to.center.y);
                float dist = Mathf.Max((p1.x - p2.x) * 0.35f, 40f);
                c1 = p1 - new Vector2(dist, 0f);
                c2 = p2 + new Vector2(dist, 0f);
            }
            else
            {
                p1 = new Vector2(from.xMax, from.center.y);
                p2 = new Vector2(to.xMax, to.center.y);
                float dist = Mathf.Max(Mathf.Abs(p2.y - p1.y) * 0.4f, 30f);
                c1 = p1 + new Vector2(dist, 0f);
                c2 = p2 + new Vector2(dist, 0f);
            }
        }

        private static void DrawEdge(Rect from, Rect to)
        {
            GetEdgeEndpoints(from, to, out var p1, out var p2, out var c1, out var c2);
            Handles.DrawBezier(p1, p2, c1, c2, Handles.color, null, 2f);
            Handles.DrawSolidDisc(p1, Vector3.forward, EdgeEndpointCircleRadius);
            Handles.DrawSolidDisc(p2, Vector3.forward, EdgeEndpointCircleRadius);
        }

        private static float CornerRadius(Rect r)
        {
            float scaled = NodeCornerRadiusAtDefaultHeight * (r.height / Mathf.Max(RefGraphLayout.NodeHeight, 0.0001f));
            return Mathf.Min(scaled, r.width * 0.5f, r.height * 0.5f);
        }

        private static void DrawRoundedNodeFill(Rect r, Color fill)
        {
            var prev = Handles.color;
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(BuildRoundedRectPath(r, CornerRadius(r), close: false));
            Handles.color = prev;
        }

        private static void DrawRoundedNodeBorder(Rect r, bool isSelected)
        {
            Handles.DrawAAPolyLine(2f, BuildRoundedRectPath(r, CornerRadius(r), close: true));
            if (isSelected)
            {
                var outer = new Rect(
                    r.xMin - SelectedBorderOffset,
                    r.yMin - SelectedBorderOffset,
                    r.width + SelectedBorderOffset * 2f,
                    r.height + SelectedBorderOffset * 2f);
                Handles.DrawAAPolyLine(2f, BuildRoundedRectPath(outer, CornerRadius(r) + SelectedBorderOffset, close: true));
            }
        }

        private static Vector3[] BuildRoundedRectPath(Rect r, float radius, bool close)
        {
            radius = Mathf.Max(0f, Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f));
            int count = RoundedCornerSegments * 4 + (close ? 1 : 0);
            var pts = new Vector3[count];
            int i = 0;
            AppendArc(pts, ref i, r.xMax - radius, r.yMin + radius, radius, 270f, 360f);
            AppendArc(pts, ref i, r.xMax - radius, r.yMax - radius, radius, 0f, 90f);
            AppendArc(pts, ref i, r.xMin + radius, r.yMax - radius, radius, 90f, 180f);
            AppendArc(pts, ref i, r.xMin + radius, r.yMin + radius, radius, 180f, 270f);
            if (close)
                pts[i] = pts[0];
            return pts;
        }

        private static void AppendArc(Vector3[] pts, ref int i, float cx, float cy, float radius, float startDeg, float endDeg)
        {
            for (int s = 0; s < RoundedCornerSegments; s++)
            {
                float t = s / (float)RoundedCornerSegments;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                pts[i++] = new Vector3(cx + Mathf.Cos(rad) * radius, cy + Mathf.Sin(rad) * radius, 0f);
            }
        }
    }
}
