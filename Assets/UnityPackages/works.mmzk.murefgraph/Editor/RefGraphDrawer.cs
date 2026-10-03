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
        private const float ArrowSize = 9f;
        private const float ArrowSizeMin = 6f;
        private const float SelectedBorderOffset = 1f;
        private const float AssemblyBarWidth = 5f;

        private static readonly Color RootFill = new Color(0.32f, 0.32f, 0.32f, 1f);
        private static readonly Color ComponentFill = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color ChildFill = new Color(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color MissingFill = new Color(0.42f, 0.18f, 0.18f, 1f);
        private static readonly Color ReferenceFill = new Color(0.22f, 0.3f, 0.38f, 1f);
        private static readonly Color DefaultBorder = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color HoverBorder = Color.green;
        private static readonly Color ConnectedBorder = Color.yellow;
        private static readonly Color OwnershipEdge = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color ReferenceEdge = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color HierarchyEdge = new Color(0.45f, 0.75f, 0.45f, 1f);
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
                    DrawEdge(from, to, IsHierarchyEdge(graph, e), zoomLevel);
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
                    if (n.IsComponent)
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
                float inset = n.IsComponent
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
                var path = BuildEdgePath(from, to, IsHierarchyEdge(graph, e), zoomLevel);
                var mid = path[path.Length / 2];
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

        private static bool IsHierarchyEdge(RefGraph graph, RefGraphEdge e)
        {
            return graph.TryGetNode(e.FromId, out var from) && from.IsGameObject
                && graph.TryGetNode(e.ToId, out var to) && to.IsGameObject;
        }

        private static Color EdgeColor(RefGraph graph, RefGraphEdge e)
        {
            if (!graph.TryGetNode(e.FromId, out var from) || !graph.TryGetNode(e.ToId, out var to))
                return ReferenceEdge;
            if (from.IsGameObject)
                return to.IsGameObject ? HierarchyEdge : OwnershipEdge;
            if (to.Kind != RefGraphNodeKind.Reference)
                return InternalEdge;
            return ReferenceEdge;
        }

        private static Color NodeFill(RefGraphNodeKind kind)
        {
            switch (kind)
            {
                case RefGraphNodeKind.Root: return RootFill;
                case RefGraphNodeKind.ChildObject: return ChildFill;
                case RefGraphNodeKind.MissingScript: return MissingFill;
                case RefGraphNodeKind.Reference: return ReferenceFill;
                default: return ComponentFill;
            }
        }

        private const int BezierSegments = 16;
        private const float TreeConnectorInset = 12f;
        private const float BackwardLoop = 20f;

        /// <summary>
        /// Polyline for an edge. Hierarchy edges (parent -> child GameObject) are tree connectors that drop from the parent's
        /// left edge and turn right into the child. Every other edge leaves from the right side of its source:
        /// forward edges enter the target's left side, edges within the same column loop back into the target's right side,
        /// and backward edges (e.g. component -> GameObject) loop into the gap next to the source, cross its column, and enter the target's right side.
        /// </summary>
        private static Vector3[] BuildEdgePath(Rect from, Rect to, bool hierarchy, float zoomLevel)
        {
            var pts = new List<Vector3>(BezierSegments * 2 + 3);
            if (hierarchy)
            {
                float x = from.xMin + TreeConnectorInset * zoomLevel;
                bool below = to.center.y >= from.center.y;
                pts.Add(new Vector3(x, below ? from.yMax : from.yMin, 0f));
                pts.Add(new Vector3(x, to.center.y, 0f));
                pts.Add(new Vector3(to.xMin, to.center.y, 0f));
                return pts.ToArray();
            }

            var right = Vector2.right;
            float columnTolerance = from.width * 0.5f;
            var p1 = new Vector2(from.xMax, from.center.y);
            if (to.center.x > from.center.x + columnTolerance)
            {
                var p2 = new Vector2(to.xMin, to.center.y);
                float dist = Mathf.Max((p2.x - p1.x) * 0.35f, 40f);
                AppendBezier(pts, p1, p1 + right * dist, p2 - right * dist, p2);
            }
            else if (to.center.x < from.center.x - columnTolerance)
            {
                bool down = to.center.y >= from.center.y;
                float gap = RefGraphLayout.NodeYSpacing * zoomLevel * 0.5f;
                float laneY = down ? from.yMax + gap : from.yMin - gap;
                float loop = BackwardLoop * zoomLevel;
                var laneRight = new Vector2(from.xMax, laneY);
                var laneLeft = new Vector2(from.xMin, laneY);
                var p2 = new Vector2(to.xMax, to.center.y);
                AppendBezier(pts, p1, p1 + right * loop, laneRight + right * loop, laneRight);
                // laneRight -> laneLeft is the straight run through the gap
                float dist = Mathf.Max((laneLeft.x - p2.x) * 0.5f, loop);
                AppendBezier(pts, laneLeft, laneLeft - right * dist, p2 + right * dist, p2);
            }
            else
            {
                var p2 = new Vector2(to.xMax, to.center.y);
                float dist = Mathf.Max(Mathf.Abs(p2.y - p1.y) * 0.4f, 30f);
                AppendBezier(pts, p1, p1 + right * dist, p2 + right * dist, p2);
            }
            return pts.ToArray();
        }

        private static void AppendBezier(List<Vector3> pts, Vector2 p0, Vector2 c0, Vector2 c1, Vector2 p1)
        {
            for (int i = 0; i <= BezierSegments; i++)
            {
                float t = i / (float)BezierSegments;
                float u = 1f - t;
                var p = u * u * u * p0 + 3f * u * u * t * c0 + 3f * u * t * t * c1 + t * t * t * p1;
                pts.Add(new Vector3(p.x, p.y, 0f));
            }
        }

        /// <summary>Draws the edge with a small dot at the source and an arrowhead at the target, so the reference direction is visible.</summary>
        private static void DrawEdge(Rect from, Rect to, bool hierarchy, float zoomLevel)
        {
            var path = BuildEdgePath(from, to, hierarchy, zoomLevel);
            var tip = path[path.Length - 1];
            var dir = ArrowDirection(path);
            float size = Mathf.Clamp(ArrowSize * zoomLevel, ArrowSizeMin, ArrowSize * 1.5f);

            // Stop the line at the arrow's base so its end does not poke through the tip
            path[path.Length - 1] = tip - dir * (size * 0.8f);
            Handles.DrawAAPolyLine(2f, path);
            Handles.DrawSolidDisc(path[0], Vector3.forward, EdgeEndpointCircleRadius);

            var side = new Vector3(-dir.y, dir.x, 0f) * (size * 0.5f);
            var baseCenter = tip - dir * size;
            Handles.DrawAAConvexPolygon(tip, baseCenter + side, baseCenter - side);
        }

        /// <summary>Direction of the path's last segment, looking back far enough to skip zero-length steps.</summary>
        private static Vector3 ArrowDirection(Vector3[] path)
        {
            var tip = path[path.Length - 1];
            for (int i = path.Length - 2; i >= 0; i--)
            {
                var d = tip - path[i];
                if (d.sqrMagnitude > 1f)
                    return d.normalized;
            }
            return Vector3.right;
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
