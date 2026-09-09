using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Mutable state passed into the input handler.</summary>
    public sealed class AsmdefGraphInputContext
    {
        public Event Event;
        public Dictionary<string, Rect> Rects;
        public Vector2 PanOffset;
        public float ZoomLevel;
        public Dictionary<string, Vector2> NodeOffsets;
        public string DraggedNode;
        public Vector2 DragStartMouse;
        public Vector2 DragStartOffset;
        public bool IsPanning;
        public Vector2 LastMouse;
        public string LastClickedNode;
        public float LastClickTime;
        public HashSet<string> LowInterestNodes;
        public Action Repaint;
        public Action Save;
        public Action<string> SelectAssetInProject;

        // Range selection (marquee) and multi-node drag
        public HashSet<string> SelectedNodes;
        public Vector2 SelectionBoxStart;
        public bool IsMarqueeSelecting;
        public Dictionary<string, Vector2> DragStartOffsetsForSelected;

        /// <summary>ツールバー領域の高さ。この範囲内のクリックはグラフで消費しない（ツールバーボタンに渡す）。</summary>
        public float ToolbarHeight;

        public Vector2 GraphOrigin;
        public List<AsmdefGraphCommentBlock> CommentBlocks;
        public HashSet<string> SelectedCommentIds;
        public bool IsCreatingComment;
        public Vector2 CommentCreateStart;
        public string DraggingCommentId;
        public string ResizingCommentId;
        public CommentCorner ResizingCorner;
        public Rect DragStartCommentRect;
        public Dictionary<string, Rect> DragStartCommentRects;
        public string RenamingCommentId;
        public string RenameBuffer;
        public string RenameOriginalLabel;
        public bool FocusRenameField;
        public bool SelectAllRenameField;
    }

    /// <summary>Handles node drag, pan, zoom, double-click, right-click interest toggle and comment blocks.</summary>
    public static class AsmdefGraphInputHandler
    {
        private const float DoubleClickTimeThreshold = 0.5f;
        private const float DragThreshold = 5f;

        public static void Handle(AsmdefGraphInputContext ctx)
        {
            var e = ctx.Event;
            if (e == null)
                return;

            var rects = ctx.Rects;
            if (rects == null)
            {
                HandlePanOnly(ctx);
                return;
            }

            if (HandleRenameEvents(ctx))
                return;

            // Left button: node drag / double-click / comment / marquee / create
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                ctx.DraggedNode = null;
                ctx.DragStartOffsetsForSelected = null;
                ctx.DraggingCommentId = null;
                ctx.ResizingCommentId = null;
                ctx.ResizingCorner = CommentCorner.None;
                ctx.DragStartCommentRects = null;
                ctx.IsCreatingComment = false;

                if (TryBeginCommentColorClick(ctx))
                    return;

                foreach (var kv in rects)
                {
                    if (!kv.Value.Contains(e.mousePosition))
                        continue;

                    CommitRenameIfAny(ctx);

                    float currentTime = (float)EditorApplication.timeSinceStartup;
                    bool isDoubleClick = (ctx.LastClickedNode == kv.Key && (currentTime - ctx.LastClickTime) < DoubleClickTimeThreshold);

                    if (isDoubleClick || e.clickCount >= 2)
                    {
                        ctx.SelectAssetInProject?.Invoke(kv.Key);
                        ctx.LastClickedNode = null;
                        ctx.LastClickTime = 0f;
                        ClearCommentSelection(ctx);
                        e.Use();
                        ctx.Repaint?.Invoke();
                        return;
                    }

                    ctx.LastClickedNode = kv.Key;
                    ctx.LastClickTime = currentTime;
                    ctx.DraggedNode = kv.Key;
                    ctx.DragStartMouse = e.mousePosition;
                    ctx.DragStartOffset = ctx.NodeOffsets != null && ctx.NodeOffsets.TryGetValue(ctx.DraggedNode, out var off) ? off : Vector2.zero;
                    ClearCommentSelection(ctx);

                    bool control = e.control;
                    if (control)
                    {
                        if (ctx.SelectedNodes == null)
                            ctx.SelectedNodes = new HashSet<string>();
                        if (ctx.SelectedNodes.Contains(kv.Key))
                            ctx.SelectedNodes.Remove(kv.Key);
                        else
                            ctx.SelectedNodes.Add(kv.Key);
                        if (ctx.SelectedNodes.Count > 1 && ctx.NodeOffsets != null)
                        {
                            ctx.DragStartOffsetsForSelected = new Dictionary<string, Vector2>();
                            foreach (var id in ctx.SelectedNodes)
                                ctx.DragStartOffsetsForSelected[id] = ctx.NodeOffsets.TryGetValue(id, out var o) ? o : Vector2.zero;
                        }
                    }
                    else
                    {
                        bool nodeInSelection = ctx.SelectedNodes != null && ctx.SelectedNodes.Contains(kv.Key);
                        if (!nodeInSelection)
                        {
                            ctx.SelectedNodes = new HashSet<string> { kv.Key };
                        }
                        else if (ctx.SelectedNodes != null && ctx.SelectedNodes.Count > 1 && ctx.NodeOffsets != null)
                        {
                            ctx.DragStartOffsetsForSelected = new Dictionary<string, Vector2>();
                            foreach (var id in ctx.SelectedNodes)
                                ctx.DragStartOffsetsForSelected[id] = ctx.NodeOffsets.TryGetValue(id, out var o) ? o : Vector2.zero;
                        }
                    }

                    e.Use();
                    return;
                }
                // ツールバー上のクリックは消費しない（Show DLLs / Show unresolved 等に渡す）
                if (ctx.ToolbarHeight > 0 && e.mousePosition.y < ctx.ToolbarHeight)
                    return;

                if (TryBeginCommentInteraction(ctx))
                    return;

                CommitRenameIfAny(ctx);
                ctx.LastClickedNode = null;
                ctx.LastClickTime = 0f;
                if (e.control)
                {
                    ctx.IsCreatingComment = true;
                    ctx.CommentCreateStart = e.mousePosition;
                    ctx.IsMarqueeSelecting = false;
                }
                else
                {
                    ctx.IsMarqueeSelecting = true;
                    ctx.SelectionBoxStart = e.mousePosition;
                }
                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (ctx.IsCreatingComment)
                {
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
                if (ctx.IsMarqueeSelecting)
                {
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
                if (!string.IsNullOrEmpty(ctx.ResizingCommentId))
                {
                    var block = FindComment(ctx, ctx.ResizingCommentId);
                    if (block != null)
                    {
                        var graphMouse = AsmdefGraphCommentBlock.ScreenToGraph(e.mousePosition, ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                        block.GraphRect = AsmdefGraphCommentBlock.ResizeFromCorner(ctx.DragStartCommentRect, ctx.ResizingCorner, graphMouse);
                    }
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
                if (!string.IsNullOrEmpty(ctx.DraggingCommentId))
                {
                    if ((e.mousePosition - ctx.DragStartMouse).magnitude > DragThreshold)
                    {
                        ctx.LastClickedNode = null;
                        ctx.LastClickTime = 0f;
                    }
                    var delta = (e.mousePosition - ctx.DragStartMouse) / Mathf.Max(ctx.ZoomLevel, 0.0001f);
                    if (ctx.DragStartCommentRects != null && ctx.DragStartCommentRects.Count > 0)
                    {
                        foreach (var kv in ctx.DragStartCommentRects)
                        {
                            var block = FindComment(ctx, kv.Key);
                            if (block == null) continue;
                            var start = kv.Value;
                            block.GraphRect = new Rect(start.x + delta.x, start.y + delta.y, start.width, start.height);
                        }
                    }
                    else
                    {
                        var block = FindComment(ctx, ctx.DraggingCommentId);
                        if (block != null)
                        {
                            var start = ctx.DragStartCommentRect;
                            block.GraphRect = new Rect(start.x + delta.x, start.y + delta.y, start.width, start.height);
                        }
                    }
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
                if (ctx.DraggedNode != null && ctx.NodeOffsets != null)
                {
                    if ((e.mousePosition - ctx.DragStartMouse).magnitude > DragThreshold)
                    {
                        ctx.LastClickedNode = null;
                        ctx.LastClickTime = 0f;
                    }
                    var delta = e.mousePosition - ctx.DragStartMouse;
                    if (ctx.DragStartOffsetsForSelected != null && ctx.DragStartOffsetsForSelected.Count > 0)
                    {
                        foreach (var kv in ctx.DragStartOffsetsForSelected)
                            ctx.NodeOffsets[kv.Key] = kv.Value + delta / ctx.ZoomLevel;
                    }
                    else
                    {
                        ctx.NodeOffsets[ctx.DraggedNode] = ctx.DragStartOffset + delta / ctx.ZoomLevel;
                    }
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (ctx.IsCreatingComment)
                {
                    FinishCreateComment(ctx);
                    ctx.IsCreatingComment = false;
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
                if (ctx.IsMarqueeSelecting)
                {
                    var boxMin = new Vector2(Mathf.Min(ctx.SelectionBoxStart.x, e.mousePosition.x), Mathf.Min(ctx.SelectionBoxStart.y, e.mousePosition.y));
                    var boxMax = new Vector2(Mathf.Max(ctx.SelectionBoxStart.x, e.mousePosition.x), Mathf.Max(ctx.SelectionBoxStart.y, e.mousePosition.y));
                    var selectionRect = Rect.MinMaxRect(boxMin.x, boxMin.y, boxMax.x, boxMax.y);
                    ctx.SelectedNodes = new HashSet<string>();
                    foreach (var kv in rects)
                    {
                        if (selectionRect.Overlaps(kv.Value))
                            ctx.SelectedNodes.Add(kv.Key);
                    }
                    ctx.SelectedCommentIds = new HashSet<string>();
                    if (!e.shift && ctx.CommentBlocks != null)
                    {
                        foreach (var block in ctx.CommentBlocks)
                        {
                            if (block == null) continue;
                            var screen = block.GetScreenRect(ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                            if (selectionRect.Overlaps(screen))
                                ctx.SelectedCommentIds.Add(block.Id);
                        }
                    }
                    ctx.IsMarqueeSelecting = false;
                    e.Use();
                    ctx.Repaint?.Invoke();
                }
                else if (!string.IsNullOrEmpty(ctx.ResizingCommentId) || !string.IsNullOrEmpty(ctx.DraggingCommentId))
                {
                    var dragDelta = (e.mousePosition - ctx.DragStartMouse).magnitude;
                    if (dragDelta >= DragThreshold)
                        ctx.Save?.Invoke();
                    ctx.ResizingCommentId = null;
                    ctx.ResizingCorner = CommentCorner.None;
                    ctx.DraggingCommentId = null;
                    ctx.DragStartCommentRects = null;
                }
                else if (ctx.DraggedNode != null)
                {
                    var dragDelta = (e.mousePosition - ctx.DragStartMouse).magnitude;
                    if (dragDelta < DragThreshold)
                    {
                        float currentTime = (float)EditorApplication.timeSinceStartup;
                        bool isDoubleClick = (ctx.LastClickedNode == ctx.DraggedNode && (currentTime - ctx.LastClickTime) < DoubleClickTimeThreshold);
                        if (isDoubleClick)
                        {
                            ctx.SelectAssetInProject?.Invoke(ctx.DraggedNode);
                            ctx.LastClickedNode = null;
                            ctx.LastClickTime = 0f;
                            e.Use();
                            ctx.Repaint?.Invoke();
                        }
                    }
                    else
                    {
                        ctx.Save?.Invoke();
                    }
                    ctx.DraggedNode = null;
                    ctx.DragStartOffsetsForSelected = null;
                }
            }

            // Right button: toggle low interest / high interest
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                foreach (var kv in rects)
                {
                    if (!kv.Value.Contains(e.mousePosition))
                        continue;
                    var clickedNode = kv.Key;
                    if (ctx.LowInterestNodes == null)
                        ctx.LowInterestNodes = new HashSet<string>();

                    bool isMultiSelectWithClicked = ctx.SelectedNodes != null && ctx.SelectedNodes.Count > 1 && ctx.SelectedNodes.Contains(clickedNode);
                    if (isMultiSelectWithClicked)
                    {
                        int lowCount = 0;
                        foreach (var id in ctx.SelectedNodes)
                        {
                            if (ctx.LowInterestNodes.Contains(id))
                                lowCount++;
                        }
                        int selectedCount = ctx.SelectedNodes.Count;
                        bool isMixed = lowCount > 0 && lowCount < selectedCount;
                        if (isMixed)
                        {
                            foreach (var id in ctx.SelectedNodes)
                                ctx.LowInterestNodes.Add(id);
                        }
                        else
                        {
                            foreach (var id in ctx.SelectedNodes)
                            {
                                if (ctx.LowInterestNodes.Contains(id))
                                    ctx.LowInterestNodes.Remove(id);
                                else
                                    ctx.LowInterestNodes.Add(id);
                            }
                        }
                    }
                    else
                    {
                        bool isLow = ctx.LowInterestNodes.Contains(clickedNode);
                        if (isLow)
                            ctx.LowInterestNodes.Remove(clickedNode);
                        else
                            ctx.LowInterestNodes.Add(clickedNode);
                    }

                    ctx.Save?.Invoke();
                    ctx.Repaint?.Invoke();
                    e.Use();
                    return;
                }
            }

            // Middle button: pan
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                ctx.IsPanning = true;
                ctx.LastMouse = e.mousePosition;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                if (ctx.IsPanning)
                {
                    var delta = e.mousePosition - ctx.LastMouse;
                    ctx.PanOffset += delta;
                    ctx.LastMouse = e.mousePosition;
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return;
                }
            }
            if (e.type == EventType.MouseUp && e.button == 2)
            {
                ctx.IsPanning = false;
            }

            // Scroll: zoom
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 0.01f;
                float oldZoom = ctx.ZoomLevel;
                ctx.ZoomLevel = Mathf.Clamp(ctx.ZoomLevel + zoomDelta, 0.3f, 3.0f);
                if (Mathf.Abs(ctx.ZoomLevel - oldZoom) > 0.001f)
                {
                    Vector2 mouseWorldPos = (e.mousePosition - ctx.PanOffset) / oldZoom;
                    ctx.PanOffset = e.mousePosition - mouseWorldPos * ctx.ZoomLevel;
                }
                e.Use();
                ctx.Repaint?.Invoke();
                return;
            }
        }

        /// <summary>When there are no nodes (no roots), only pan is available.</summary>
        public static void HandlePanOnly(AsmdefGraphInputContext ctx)
        {
            var e = ctx.Event;
            if (e == null) return;
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                ctx.IsPanning = true;
                ctx.LastMouse = e.mousePosition;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseDrag && e.button == 2 && ctx.IsPanning)
            {
                var delta = e.mousePosition - ctx.LastMouse;
                ctx.PanOffset += delta;
                ctx.LastMouse = e.mousePosition;
                e.Use();
                ctx.Repaint?.Invoke();
                return;
            }
            if (e.type == EventType.MouseUp && e.button == 2)
            {
                ctx.IsPanning = false;
            }
        }

        private static bool HandleRenameEvents(AsmdefGraphInputContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.RenamingCommentId))
                return false;
            var e = ctx.Event;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRenameIfAny(ctx);
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return true;
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    CancelRename(ctx);
                    e.Use();
                    ctx.Repaint?.Invoke();
                    return true;
                }
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var block = FindComment(ctx, ctx.RenamingCommentId);
                if (block != null)
                {
                    var labelRect = block.GetLabelScreenRect(ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                    if (labelRect.Contains(e.mousePosition))
                        return true;
                }
                CommitRenameIfAny(ctx);
            }
            return false;
        }

        private static bool TryBeginCommentColorClick(AsmdefGraphInputContext ctx)
        {
            var e = ctx.Event;
            if (ctx.CommentBlocks == null || ctx.CommentBlocks.Count == 0)
                return false;

            for (int i = ctx.CommentBlocks.Count - 1; i >= 0; i--)
            {
                var block = ctx.CommentBlocks[i];
                if (block == null) continue;
                if (!block.HitTestColorButton(e.mousePosition, ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel))
                    continue;
                CommitRenameIfAny(ctx);
                block.Hue = AsmdefGraphCommentPalette.NextRandomHue(block.Hue);
                SelectOnlyComment(ctx, block.Id);
                ctx.Save?.Invoke();
                e.Use();
                ctx.Repaint?.Invoke();
                return true;
            }

            return false;
        }

        private static bool TryBeginCommentInteraction(AsmdefGraphInputContext ctx)
        {
            var e = ctx.Event;
            if (ctx.CommentBlocks == null || ctx.CommentBlocks.Count == 0)
                return false;

            for (int i = ctx.CommentBlocks.Count - 1; i >= 0; i--)
            {
                var block = ctx.CommentBlocks[i];
                if (block == null) continue;
                var screen = block.GetScreenRect(ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                bool selected = ctx.SelectedCommentIds != null && ctx.SelectedCommentIds.Contains(block.Id);
                if (selected)
                {
                    var corner = AsmdefGraphCommentBlock.HitTestCorner(screen, e.mousePosition, AsmdefGraphCommentBlock.HandleHitSizeScreen);
                    if (corner != CommentCorner.None)
                    {
                        CommitRenameIfAny(ctx);
                        BeginCommentResize(ctx, block, corner);
                        e.Use();
                        ctx.Repaint?.Invoke();
                        return true;
                    }
                }
            }

            for (int i = ctx.CommentBlocks.Count - 1; i >= 0; i--)
            {
                var block = ctx.CommentBlocks[i];
                if (block == null) continue;
                var labelRect = block.GetLabelScreenRect(ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                if (!labelRect.Contains(e.mousePosition))
                    continue;
                SelectOnlyComment(ctx, block.Id);
                StartRename(ctx, block, selectAll: true);
                e.Use();
                ctx.Repaint?.Invoke();
                return true;
            }

            for (int i = ctx.CommentBlocks.Count - 1; i >= 0; i--)
            {
                var block = ctx.CommentBlocks[i];
                if (block == null) continue;
                var screen = block.GetScreenRect(ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
                if (!screen.Contains(e.mousePosition))
                    continue;
                CommitRenameIfAny(ctx);
                BeginCommentMove(ctx, block);
                e.Use();
                ctx.Repaint?.Invoke();
                return true;
            }

            return false;
        }

        private static void BeginCommentResize(AsmdefGraphInputContext ctx, AsmdefGraphCommentBlock block, CommentCorner corner)
        {
            ctx.ResizingCommentId = block.Id;
            ctx.ResizingCorner = corner;
            ctx.DragStartMouse = ctx.Event.mousePosition;
            ctx.DragStartCommentRect = block.GraphRect;
            SelectOnlyComment(ctx, block.Id);
        }

        private static void BeginCommentMove(AsmdefGraphInputContext ctx, AsmdefGraphCommentBlock block)
        {
            ctx.DraggingCommentId = block.Id;
            ctx.DragStartMouse = ctx.Event.mousePosition;
            ctx.DragStartCommentRect = block.GraphRect;
            bool alreadySelected = ctx.SelectedCommentIds != null && ctx.SelectedCommentIds.Contains(block.Id);
            if (!alreadySelected)
                SelectOnlyComment(ctx, block.Id);
            else
                ctx.SelectedNodes = new HashSet<string>();

            if (ctx.SelectedCommentIds != null && ctx.SelectedCommentIds.Count > 1)
            {
                ctx.DragStartCommentRects = new Dictionary<string, Rect>();
                foreach (var id in ctx.SelectedCommentIds)
                {
                    var selected = FindComment(ctx, id);
                    if (selected != null)
                        ctx.DragStartCommentRects[id] = selected.GraphRect;
                }
            }
        }

        private static void FinishCreateComment(AsmdefGraphInputContext ctx)
        {
            var e = ctx.Event;
            var unclamped = AsmdefGraphCommentBlock.GraphRectFromScreenDrag(
                ctx.CommentCreateStart, e.mousePosition, ctx.GraphOrigin, ctx.PanOffset, ctx.ZoomLevel);
            if (!AsmdefGraphCommentBlock.IsCreateSizeValid(unclamped))
                return;
            if (ctx.CommentBlocks == null)
                return;
            var block = AsmdefGraphCommentBlock.Create(unclamped);
            ctx.CommentBlocks.Add(block);
            SelectOnlyComment(ctx, block.Id);
            StartRename(ctx, block, selectAll: true);
            ctx.Save?.Invoke();
        }

        private static void StartRename(AsmdefGraphInputContext ctx, AsmdefGraphCommentBlock block, bool selectAll)
        {
            ctx.RenamingCommentId = block.Id;
            ctx.RenameOriginalLabel = block.Label;
            ctx.RenameBuffer = block.Label;
            ctx.FocusRenameField = true;
            ctx.SelectAllRenameField = selectAll;
        }

        private static void CommitRenameIfAny(AsmdefGraphInputContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.RenamingCommentId))
                return;
            var block = FindComment(ctx, ctx.RenamingCommentId);
            if (block != null)
            {
                var name = ctx.RenameBuffer;
                if (string.IsNullOrWhiteSpace(name))
                    name = AsmdefGraphCommentBlock.DefaultLabel;
                else
                    name = name.Trim();
                block.Label = name;
            }
            ctx.RenamingCommentId = null;
            ctx.RenameBuffer = null;
            ctx.RenameOriginalLabel = null;
            ctx.FocusRenameField = false;
            ctx.SelectAllRenameField = false;
            ctx.Save?.Invoke();
        }

        private static void CancelRename(AsmdefGraphInputContext ctx)
        {
            var block = FindComment(ctx, ctx.RenamingCommentId);
            if (block != null && ctx.RenameOriginalLabel != null)
                block.Label = ctx.RenameOriginalLabel;
            ctx.RenamingCommentId = null;
            ctx.RenameBuffer = null;
            ctx.RenameOriginalLabel = null;
            ctx.FocusRenameField = false;
            ctx.SelectAllRenameField = false;
        }

        private static void SelectOnlyComment(AsmdefGraphInputContext ctx, string commentId)
        {
            ctx.SelectedCommentIds = new HashSet<string> { commentId };
            ctx.SelectedNodes = new HashSet<string>();
        }

        private static void ClearCommentSelection(AsmdefGraphInputContext ctx)
        {
            ctx.SelectedCommentIds = new HashSet<string>();
        }

        private static AsmdefGraphCommentBlock FindComment(AsmdefGraphInputContext ctx, string id)
        {
            if (ctx.CommentBlocks == null || string.IsNullOrEmpty(id))
                return null;
            foreach (var block in ctx.CommentBlocks)
            {
                if (block != null && block.Id == id)
                    return block;
            }
            return null;
        }
    }
}
