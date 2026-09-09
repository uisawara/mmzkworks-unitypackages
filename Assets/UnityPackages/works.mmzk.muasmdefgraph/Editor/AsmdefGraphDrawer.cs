using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Draws graph edges, node borders, backgrounds and labels.</summary>
    public static class AsmdefGraphDrawer
    {
        private const string DllPrefix = "DLL::";
        private const int RoundedCornerSegments = 6;
        private const float NodeCornerRadiusAtDefaultHeight = 5f;

        public static void DrawGraph(
            Dictionary<string, Rect> rects,
            HashSet<string> roots,
            HashSet<string> lowInterestNodes,
            Vector2 mousePosition,
            HashSet<string> selectedNodes = null,
            bool isMarqueeSelecting = false,
            Vector2 selectionBoxStart = default)
        {
            if (rects == null || rects.Count == 0)
                return;

            string hoveredNode = null;
            foreach (var kv in rects)
            {
                if (kv.Value.Contains(mousePosition))
                {
                    hoveredNode = kv.Key;
                    break;
                }
            }

            var dependentNodes = new HashSet<string>();
            var dependeeNodes = new HashSet<string>();
            if (hoveredNode != null)
            {
                if (!hoveredNode.StartsWith(DllPrefix, System.StringComparison.Ordinal) &&
                    AsmdefDatabase.TryGetAsmdefInfoByName(hoveredNode, out var info))
                {
                    foreach (var refName in info.References)
                    {
                        if (rects.ContainsKey(refName))
                            dependentNodes.Add(refName);
                    }
                    if (info.PrecompiledReferences != null)
                    {
                        foreach (var dll in info.PrecompiledReferences)
                        {
                            if (string.IsNullOrEmpty(dll)) continue;
                            var dllKey = DllPrefix + dll;
                            if (rects.ContainsKey(dllKey))
                                dependentNodes.Add(dllKey);
                        }
                    }
                }

                foreach (var kv in rects)
                {
                    var from = kv.Key;
                    if (from.StartsWith(DllPrefix, System.StringComparison.Ordinal) || from == hoveredNode)
                        continue;
                    if (AsmdefDatabase.TryGetAsmdefInfoByName(from, out var fromInfo))
                    {
                        if (fromInfo.References.Contains(hoveredNode))
                            dependeeNodes.Add(from);
                        if (hoveredNode.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                        {
                            var dllName = hoveredNode.Substring(DllPrefix.Length);
                            if (fromInfo.PrecompiledReferences != null && fromInfo.PrecompiledReferences.Contains(dllName))
                                dependeeNodes.Add(from);
                        }
                    }
                }
            }

            Handles.BeginGUI();
            try
            {
                foreach (var kv in rects)
                {
                    bool isLowInterest = lowInterestNodes != null && lowInterestNodes.Contains(kv.Key);
                    DrawRoundedNodeFill(kv.Value, GetNodeFillColor(kv.Key, roots, isLowInterest));
                }

                foreach (var kv in rects)
                {
                    var name = kv.Key;
                    var r = kv.Value;
                    bool isSelected = selectedNodes != null && selectedNodes.Contains(name);
                    bool isCurrentNode = (name == hoveredNode);
                    bool isDependent = dependentNodes.Contains(name);
                    bool isDependee = dependeeNodes.Contains(name);
                    bool isLowInterest = lowInterestNodes != null && lowInterestNodes.Contains(name);

                    if (name.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                    {
                        if (isSelected) Handles.color = Color.white;
                        else if (isCurrentNode) Handles.color = Color.green;
                        else if (isDependent) Handles.color = new Color(1f, 0.5f, 0f, 1f);
                        else if (isDependee) Handles.color = Color.yellow;
                        else Handles.color = new Color(0.2f, 0.7f, 1f, 1f);
                        if (isLowInterest) Handles.color = Dim(Handles.color);
                        DrawRoundedNodeBorder(r, isSelected);
                    }
                    else
                    {
                        if (isSelected) Handles.color = Color.white;
                        else if (isCurrentNode) Handles.color = Color.green;
                        else if (isDependent) Handles.color = new Color(1f, 0.5f, 0f, 1f);
                        else if (isDependee) Handles.color = Color.yellow;
                        else
                        {
                            var isRoot = roots != null && roots.Contains(name);
                            Handles.color = isRoot
                                ? new Color(1f, 1f, 1f, 1f)
                                : new Color(0.7f, 0.7f, 0.7f, 1f);
                        }
                        if (isLowInterest) Handles.color = Dim(Handles.color);
                        DrawRoundedNodeBorder(r, isSelected);
                    }
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            var centerStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            var guidStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = (int)(EditorStyles.label.fontSize * 0.75f) };

            foreach (var kv in rects)
            {
                var name = kv.Key;
                var r = kv.Value;
                bool isLowInterest = lowInterestNodes != null && lowInterestNodes.Contains(name);

                if (name.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                {
                    var dllName = name.Substring(DllPrefix.Length);
                    GUI.Label(r, new GUIContent(dllName, dllName), centerStyle);
                    continue;
                }

                string displayName = name;
                if (AsmdefDatabase.TryGetPathByDisplayName(name, out var pathForGuid) && !string.IsNullOrEmpty(pathForGuid))
                {
                    var guid = AssetDatabase.AssetPathToGUID(pathForGuid);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        float lineHeight = r.height * 0.4f;
                        float totalHeight = lineHeight * 2f;
                        float textStartY = r.y + (r.height - totalHeight) * 0.5f;
                        var nameRect = new Rect(r.x, textStartY, r.width, lineHeight);
                        var guidRect = new Rect(r.x, textStartY + lineHeight, r.width, lineHeight);
                        var originalTextColor = GUI.color;
                        GUI.color = isLowInterest
                            ? new Color(originalTextColor.r * 0.7f, originalTextColor.g * 0.7f, originalTextColor.b * 0.7f, 1f)
                            : originalTextColor;
                        GUI.Label(nameRect, new GUIContent(displayName, displayName), centerStyle);
                        GUI.Label(guidRect, new GUIContent(guid, guid), guidStyle);
                        GUI.color = originalTextColor;
                    }
                    else
                    {
                        GUI.Label(r, new GUIContent(displayName, displayName), centerStyle);
                    }
                }
                else
                {
                    GUI.Label(r, new GUIContent(displayName, displayName), centerStyle);
                }
            }

            Handles.BeginGUI();
            try
            {
                foreach (var kv in rects)
                {
                    var from = kv.Key;
                    if (from.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                        continue;
                    if (!AsmdefDatabase.TryGetAsmdefInfoByName(from, out var info))
                        continue;
                    var fromRect = kv.Value;

                    foreach (var to in info.References)
                    {
                        if (!rects.TryGetValue(to, out var toRect))
                            continue;
                        bool isLowInterest = (lowInterestNodes != null && (lowInterestNodes.Contains(from) || lowInterestNodes.Contains(to)));
                        Handles.color = isLowInterest
                            ? new Color(0.55f, 0.55f, 0.55f, 1f)
                            : new Color(1f, 1f, 1f, 1f);
                        DrawBezierEdge(new Vector2(fromRect.xMax, fromRect.center.y), new Vector2(toRect.xMin, toRect.center.y), isLowInterest);
                    }

                    if (info.PrecompiledReferences != null)
                    {
                        foreach (var dll in info.PrecompiledReferences)
                        {
                            if (string.IsNullOrEmpty(dll)) continue;
                            var dllKey = DllPrefix + dll;
                            if (!rects.TryGetValue(dllKey, out var toRect))
                                continue;
                            bool isLowInterest = (lowInterestNodes != null && (lowInterestNodes.Contains(from) || lowInterestNodes.Contains(dllKey)));
                            Handles.color = isLowInterest
                                ? new Color(0.12f, 0.42f, 0.6f, 1f)
                                : new Color(0.2f, 0.7f, 1f, 1f);
                            DrawBezierEdge(new Vector2(fromRect.xMax, fromRect.center.y), new Vector2(toRect.xMin, toRect.center.y), isLowInterest);
                        }
                    }
                }

                if (isMarqueeSelecting)
                {
                    var boxMin = new Vector2(Mathf.Min(selectionBoxStart.x, mousePosition.x), Mathf.Min(selectionBoxStart.y, mousePosition.y));
                    var boxMax = new Vector2(Mathf.Max(selectionBoxStart.x, mousePosition.x), Mathf.Max(selectionBoxStart.y, mousePosition.y));
                    var boxRect = Rect.MinMaxRect(boxMin.x, boxMin.y, boxMax.x, boxMax.y);
                    Handles.DrawSolidRectangleWithOutline(new Vector3[] {
                        new Vector3(boxRect.xMin, boxRect.yMin, 0f),
                        new Vector3(boxRect.xMax, boxRect.yMin, 0f),
                        new Vector3(boxRect.xMax, boxRect.yMax, 0f),
                        new Vector3(boxRect.xMin, boxRect.yMax, 0f)
                    }, new Color(0.2f, 0.5f, 1f, 0.15f), new Color(0.3f, 0.6f, 1f, 0.8f));
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static readonly Color CommentHandleFill = new Color(1f, 1f, 1f, 0.95f);

        public static void DrawCommentBodies(
            List<AsmdefGraphCommentBlock> comments,
            Vector2 graphOrigin,
            Vector2 panOffset,
            float zoomLevel,
            HashSet<string> selectedCommentIds,
            bool isCreatingComment,
            Vector2 commentCreateStart,
            Vector2 mousePosition,
            string renamingCommentId = null)
        {
            if ((comments == null || comments.Count == 0) && !isCreatingComment)
                return;

            Handles.BeginGUI();
            try
            {
                if (comments != null)
                {
                    foreach (var block in comments)
                    {
                        if (block == null) continue;
                        var screen = block.GetScreenRect(graphOrigin, panOffset, zoomLevel);
                        bool selected = selectedCommentIds != null && selectedCommentIds.Contains(block.Id);
                        AsmdefGraphCommentPalette.GetColors(block.Hue, out var fill, out var border, out var borderSelected);
                        DrawCommentRect(screen, selected, zoomLevel, fill, border, borderSelected);
                    }
                }
                if (isCreatingComment)
                {
                    var preview = AsmdefGraphCommentBlock.ScreenRectFromDrag(commentCreateStart, mousePosition);
                    AsmdefGraphCommentPalette.GetColors(AsmdefGraphCommentPalette.DefaultHue, out var fill, out var border, out var borderSelected);
                    DrawCommentRect(preview, true, zoomLevel, fill, border, borderSelected);
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            if (comments == null || comments.Count == 0)
                return;

            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            var originalColor = GUI.color;
            GUI.color = Color.white;
            foreach (var block in comments)
            {
                if (block == null || block.Id == renamingCommentId)
                    continue;
                var labelRect = block.GetLabelScreenRect(graphOrigin, panOffset, zoomLevel);
                GUI.Label(labelRect, new GUIContent(block.Label, block.Label), labelStyle);
            }
            GUI.color = originalColor;
        }

        public static void DrawCommentColorButtons(
            List<AsmdefGraphCommentBlock> comments,
            Vector2 graphOrigin,
            Vector2 panOffset,
            float zoomLevel)
        {
            if (comments == null || comments.Count == 0)
                return;

            Handles.BeginGUI();
            try
            {
                foreach (var block in comments)
                {
                    if (block == null) continue;
                    var btn = block.GetColorButtonScreenRect(graphOrigin, panOffset, zoomLevel);
                    var fill = AsmdefGraphCommentPalette.Swatch(block.Hue);
                    fill.a = 0.5f;
                    Handles.color = fill;
                    Handles.DrawSolidDisc(new Vector3(btn.center.x, btn.center.y, 0f), Vector3.forward, btn.width * 0.5f);
                    EditorGUIUtility.AddCursorRect(btn, MouseCursor.Link);
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            foreach (var block in comments)
            {
                if (block == null) continue;
                var btn = block.GetColorButtonScreenRect(graphOrigin, panOffset, zoomLevel);
                GUI.Label(btn, new GUIContent(string.Empty, "Randomize this comment color"));
            }
        }

        public static void DrawCommentChrome(
            List<AsmdefGraphCommentBlock> comments,
            Vector2 graphOrigin,
            Vector2 panOffset,
            float zoomLevel,
            HashSet<string> selectedCommentIds,
            string renamingCommentId)
        {
            if (comments == null || comments.Count == 0)
                return;

            foreach (var block in comments)
            {
                if (block == null) continue;
                bool selected = selectedCommentIds != null && selectedCommentIds.Contains(block.Id);
                if (!selected)
                    continue;

                var screen = block.GetScreenRect(graphOrigin, panOffset, zoomLevel);
                DrawHandleFill(screen, CommentCorner.TopLeft);
                DrawHandleFill(screen, CommentCorner.TopRight);
                DrawHandleFill(screen, CommentCorner.BottomRight);
                DrawHandleFill(screen, CommentCorner.BottomLeft);
                EditorGUIUtility.AddCursorRect(AsmdefGraphCommentBlock.GetHandleScreenRect(screen, CommentCorner.TopLeft, AsmdefGraphCommentBlock.HandleHitSizeScreen), MouseCursor.ResizeUpLeft);
                EditorGUIUtility.AddCursorRect(AsmdefGraphCommentBlock.GetHandleScreenRect(screen, CommentCorner.TopRight, AsmdefGraphCommentBlock.HandleHitSizeScreen), MouseCursor.ResizeUpRight);
                EditorGUIUtility.AddCursorRect(AsmdefGraphCommentBlock.GetHandleScreenRect(screen, CommentCorner.BottomRight, AsmdefGraphCommentBlock.HandleHitSizeScreen), MouseCursor.ResizeUpLeft);
                EditorGUIUtility.AddCursorRect(AsmdefGraphCommentBlock.GetHandleScreenRect(screen, CommentCorner.BottomLeft, AsmdefGraphCommentBlock.HandleHitSizeScreen), MouseCursor.ResizeUpRight);
            }
        }

        private static void DrawCommentRect(Rect screen, bool selected, float zoomLevel, Color fill, Color border, Color borderSelected)
        {
            float radius = Mathf.Min(
                NodeCornerRadiusAtDefaultHeight * Mathf.Max(zoomLevel, 0.0001f),
                screen.width * 0.5f,
                screen.height * 0.5f);
            var prev = Handles.color;
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(BuildRoundedRectPath(screen, radius, close: false));
            Handles.color = selected ? borderSelected : border;
            Handles.DrawAAPolyLine(2f, BuildRoundedRectPath(screen, radius, close: true));
            Handles.color = prev;
        }

        private static void DrawHandleFill(Rect screenRect, CommentCorner corner)
        {
            var r = AsmdefGraphCommentBlock.GetHandleScreenRect(screenRect, corner, AsmdefGraphCommentBlock.HandleSizeScreen);
            EditorGUI.DrawRect(r, CommentHandleFill);
        }

        private const float EdgeEndpointCircleRadius = 2.8f; // 4f * 0.7
        private const float SelectedBorderOffset = 1f;

        private static Color Dim(Color color)
        {
            return new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f);
        }

        private static Color GetNodeFillColor(string name, HashSet<string> roots, bool isLowInterest)
        {
            if (name.StartsWith(DllPrefix, System.StringComparison.Ordinal))
                return isLowInterest ? new Color(0.16f, 0.26f, 0.32f, 1f) : new Color(0.22f, 0.38f, 0.48f, 1f);
            var isRoot = roots != null && roots.Contains(name);
            if (isLowInterest)
                return isRoot ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
            return isRoot ? new Color(0.32f, 0.32f, 0.32f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
        }

        private static float CornerRadius(Rect r)
        {
            float scaled = NodeCornerRadiusAtDefaultHeight * (r.height / Mathf.Max(AsmdefGraphLayout.NodeHeight, 0.0001f));
            return Mathf.Min(scaled, r.width * 0.5f, r.height * 0.5f);
        }

        private static void DrawRoundedNodeFill(Rect r, Color fill)
        {
            var path = BuildRoundedRectPath(r, CornerRadius(r), close: false);
            var prev = Handles.color;
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(path);
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

        public static void DrawBezierEdge(Vector2 p1, Vector2 p2, bool isLowInterest = false)
        {
            float dist = Mathf.Max((p2.x - p1.x) * 0.35f, 40f);
            var c1 = p1 + new Vector2(dist, 0f);
            var c2 = p2 + new Vector2(-dist, 0f);
            Handles.DrawBezier(new Vector3(p1.x, p1.y, 0f), new Vector3(p2.x, p2.y, 0f),
                new Vector3(c1.x, c1.y, 0f), new Vector3(c2.x, c2.y, 0f),
                Handles.color, null, 2f);
            Handles.DrawSolidDisc(new Vector3(p1.x, p1.y, 0f), Vector3.forward, EdgeEndpointCircleRadius);
            Handles.DrawSolidDisc(new Vector3(p2.x, p2.y, 0f), Vector3.forward, EdgeEndpointCircleRadius);
        }

        private static string Truncate(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxChars)
                return s;
            return s.Substring(0, System.Math.Max(0, maxChars - 3)) + "...";
        }
    }
}
