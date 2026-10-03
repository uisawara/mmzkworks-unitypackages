using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>View and interaction state of the graph, owned by the window.</summary>
    public sealed class RefGraphViewState
    {
        public Vector2 PanOffset;
        public float ZoomLevel = 1f;
        public Dictionary<string, Vector2> NodeOffsets = new Dictionary<string, Vector2>();
        public HashSet<string> SelectedNodes = new HashSet<string>();

        public string DraggedNode;
        public Vector2 DragStartMouse;
        public Dictionary<string, Vector2> DragStartOffsets;
        public bool IsPanning;
        public Vector2 LastMouse;
        /// <summary>While Space is held, left drag pans.</summary>
        public bool IsSpaceHeld;
        public bool IsMarqueeSelecting;
        public Vector2 SelectionBoxStart;
    }

    /// <summary>
    /// Pan (middle / right / Alt + left / Space + left drag), zoom, node drag, selection and double-click handling
    /// (ported from muAsmdefgraph without comment blocks).
    /// </summary>
    public static class RefGraphInputHandler
    {
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 3.0f;

        /// <param name="rects">Current node screen rects. May be null when there is nothing to show (pan only).</param>
        /// <param name="graphOrigin">Top-left of the graph area; clicks above its Y (toolbar) are ignored.</param>
        /// <param name="openNode">Called with the node id on double-click.</param>
        public static void Handle(
            Event e,
            RefGraphViewState state,
            Dictionary<string, Rect> rects,
            Vector2 graphOrigin,
            Action<string> openNode,
            Action repaint)
        {
            if (e == null || state == null)
                return;

            float graphTop = graphOrigin.y;
            bool panButton = e.button == 2 || e.button == 1 || (e.button == 0 && (e.alt || state.IsSpaceHeld));

            switch (e.type)
            {
                case EventType.KeyDown when e.keyCode == KeyCode.Space && GUIUtility.keyboardControl == 0:
                case EventType.KeyUp when e.keyCode == KeyCode.Space && GUIUtility.keyboardControl == 0:
                    state.IsSpaceHeld = e.type == EventType.KeyDown;
                    e.Use();
                    repaint?.Invoke();
                    return;

                case EventType.MouseDown when panButton:
                    if (e.mousePosition.y < graphTop)
                        return;
                    state.IsPanning = true;
                    state.LastMouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseDrag when state.IsPanning:
                    state.PanOffset += e.mousePosition - state.LastMouse;
                    state.LastMouse = e.mousePosition;
                    e.Use();
                    repaint?.Invoke();
                    return;

                case EventType.MouseUp when state.IsPanning:
                    state.IsPanning = false;
                    e.Use();
                    return;

                case EventType.ScrollWheel:
                {
                    float oldZoom = state.ZoomLevel;
                    state.ZoomLevel = Mathf.Clamp(state.ZoomLevel - e.delta.y * 0.01f, MinZoom, MaxZoom);
                    if (Mathf.Abs(state.ZoomLevel - oldZoom) > 0.001f)
                    {
                        // Keep the graph point under the cursor fixed while zooming
                        var local = e.mousePosition - graphOrigin;
                        Vector2 mouseGraphPos = (local - state.PanOffset) / oldZoom;
                        state.PanOffset = local - mouseGraphPos * state.ZoomLevel;
                    }
                    e.Use();
                    repaint?.Invoke();
                    return;
                }
            }

            if (rects == null)
                return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (e.mousePosition.y < graphTop)
                    return;

                var hit = HitTest(rects, e.mousePosition);
                if (hit == null)
                {
                    if (!EditorGUI.actionKey)
                        state.SelectedNodes.Clear();
                    state.IsMarqueeSelecting = true;
                    state.SelectionBoxStart = e.mousePosition;
                    e.Use();
                    repaint?.Invoke();
                    return;
                }

                if (e.clickCount >= 2)
                {
                    openNode?.Invoke(hit);
                    state.DraggedNode = null;
                    e.Use();
                    return;
                }

                if (EditorGUI.actionKey)
                {
                    if (!state.SelectedNodes.Remove(hit))
                        state.SelectedNodes.Add(hit);
                }
                else if (!state.SelectedNodes.Contains(hit))
                {
                    state.SelectedNodes.Clear();
                    state.SelectedNodes.Add(hit);
                }

                state.DraggedNode = hit;
                state.DragStartMouse = e.mousePosition;
                state.DragStartOffsets = new Dictionary<string, Vector2>();
                var dragTargets = state.SelectedNodes.Contains(hit) ? (IEnumerable<string>)state.SelectedNodes : new[] { hit };
                foreach (var id in dragTargets)
                    state.DragStartOffsets[id] = state.NodeOffsets.TryGetValue(id, out var o) ? o : Vector2.zero;
                e.Use();
                repaint?.Invoke();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (state.IsMarqueeSelecting)
                {
                    e.Use();
                    repaint?.Invoke();
                    return;
                }
                if (state.DraggedNode != null && state.DragStartOffsets != null)
                {
                    var delta = (e.mousePosition - state.DragStartMouse) / Mathf.Max(state.ZoomLevel, 0.0001f);
                    foreach (var kv in state.DragStartOffsets)
                        state.NodeOffsets[kv.Key] = kv.Value + delta;
                    e.Use();
                    repaint?.Invoke();
                }
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (state.IsMarqueeSelecting)
                {
                    var box = MarqueeRect(state.SelectionBoxStart, e.mousePosition);
                    foreach (var kv in rects)
                    {
                        if (box.Overlaps(kv.Value))
                            state.SelectedNodes.Add(kv.Key);
                    }
                    state.IsMarqueeSelecting = false;
                    e.Use();
                    repaint?.Invoke();
                }
                state.DraggedNode = null;
                state.DragStartOffsets = null;
            }
        }

        public static string HitTest(Dictionary<string, Rect> rects, Vector2 position)
        {
            if (rects == null)
                return null;
            foreach (var kv in rects)
            {
                if (kv.Value.Contains(position))
                    return kv.Key;
            }
            return null;
        }

        public static Rect MarqueeRect(Vector2 start, Vector2 current)
        {
            return Rect.MinMaxRect(
                Mathf.Min(start.x, current.x), Mathf.Min(start.y, current.y),
                Mathf.Max(start.x, current.x), Mathf.Max(start.y, current.y));
        }
    }
}
