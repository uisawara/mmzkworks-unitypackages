using System;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    public enum CommentCorner
    {
        None = 0,
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    /// <summary>Comment rectangle in graph space, with screen-space conversion and resize helpers.</summary>
    public sealed class AsmdefGraphCommentBlock
    {
        public const string DefaultLabel = "Comment";
        public const float MinWidth = 80f;
        public const float MinHeight = 40f;
        public const float HandleSizeScreen = 8f;
        public const float HandleHitSizeScreen = 14f;
        public const float LabelHeightGraph = 18f;
        public const float LabelHorizontalInsetScreen = 16f;
        public const float ColorButtonSizeScreen = 7f;
        public const float ColorButtonGapScreen = 4f;

        public string Id;
        public string Label;
        public Rect GraphRect;
        public float Hue = AsmdefGraphCommentPalette.DefaultHue;

        public static AsmdefGraphCommentBlock Create(Rect graphRect, string label = null)
        {
            return new AsmdefGraphCommentBlock
            {
                Id = Guid.NewGuid().ToString("N"),
                Label = string.IsNullOrEmpty(label) ? DefaultLabel : label,
                GraphRect = NormalizeAndClamp(graphRect),
                Hue = AsmdefGraphCommentPalette.DefaultHue
            };
        }

        public CommentBlockEntry ToEntry()
        {
            return new CommentBlockEntry
            {
                id = Id,
                label = Label,
                x = GraphRect.x,
                y = GraphRect.y,
                width = GraphRect.width,
                height = GraphRect.height,
                hue = Hue,
                hasColor = true
            };
        }

        public static AsmdefGraphCommentBlock FromEntry(CommentBlockEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                return null;
            return new AsmdefGraphCommentBlock
            {
                Id = entry.id,
                Label = string.IsNullOrEmpty(entry.label) ? DefaultLabel : entry.label,
                GraphRect = NormalizeAndClamp(new Rect(entry.x, entry.y, entry.width, entry.height)),
                Hue = entry.hasColor ? entry.hue : AsmdefGraphCommentPalette.DefaultHue
            };
        }

        public static Rect ToScreenRect(Rect graphRect, Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            float z = Mathf.Max(zoom, 0.0001f);
            return new Rect(
                graphOrigin.x + panOffset.x + graphRect.x * z,
                graphOrigin.y + panOffset.y + graphRect.y * z,
                graphRect.width * z,
                graphRect.height * z);
        }

        public static Rect ToGraphRect(Rect screenRect, Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            float z = Mathf.Max(zoom, 0.0001f);
            return new Rect(
                (screenRect.x - graphOrigin.x - panOffset.x) / z,
                (screenRect.y - graphOrigin.y - panOffset.y) / z,
                screenRect.width / z,
                screenRect.height / z);
        }

        public static Vector2 ScreenToGraph(Vector2 screen, Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            float z = Mathf.Max(zoom, 0.0001f);
            return new Vector2(
                (screen.x - graphOrigin.x - panOffset.x) / z,
                (screen.y - graphOrigin.y - panOffset.y) / z);
        }

        public static Rect GraphRectFromScreenDrag(Vector2 screenA, Vector2 screenB, Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            var a = ScreenToGraph(screenA, graphOrigin, panOffset, zoom);
            var b = ScreenToGraph(screenB, graphOrigin, panOffset, zoom);
            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y));
        }

        public static Rect ScreenRectFromDrag(Vector2 screenA, Vector2 screenB)
        {
            return Rect.MinMaxRect(
                Mathf.Min(screenA.x, screenB.x),
                Mathf.Min(screenA.y, screenB.y),
                Mathf.Max(screenA.x, screenB.x),
                Mathf.Max(screenA.y, screenB.y));
        }

        public Rect GetScreenRect(Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            return ToScreenRect(GraphRect, graphOrigin, panOffset, zoom);
        }

        public Rect GetLabelScreenRect(Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            var screen = GetScreenRect(graphOrigin, panOffset, zoom);
            float z = Mathf.Max(zoom, 0.0001f);
            float labelH = Mathf.Min(LabelHeightGraph * z, screen.height);
            float leftInset = LabelHorizontalInsetScreen + ColorButtonSizeScreen + ColorButtonGapScreen;
            float rightInset = LabelHorizontalInsetScreen;
            float width = screen.width - leftInset - rightInset;
            if (width < 1f)
            {
                leftInset = LabelHorizontalInsetScreen;
                rightInset = LabelHorizontalInsetScreen;
                width = screen.width - leftInset - rightInset;
                if (width < 1f)
                {
                    width = Mathf.Max(1f, screen.width);
                    leftInset = (screen.width - width) * 0.5f;
                    rightInset = leftInset;
                }
            }
            return new Rect(screen.xMin + leftInset, screen.yMin, width, labelH);
        }

        public Rect GetColorButtonScreenRect(Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            var screen = GetScreenRect(graphOrigin, panOffset, zoom);
            float z = Mathf.Max(zoom, 0.0001f);
            float labelH = Mathf.Min(LabelHeightGraph * z, screen.height);
            float size = ColorButtonSizeScreen;
            float x = screen.xMin + LabelHorizontalInsetScreen;
            float y = screen.yMin + (labelH - size) * 0.5f;
            return new Rect(x, y, size, size);
        }

        public bool HitTestColorButton(Vector2 mouse, Vector2 graphOrigin, Vector2 panOffset, float zoom)
        {
            var r = GetColorButtonScreenRect(graphOrigin, panOffset, zoom);
            var delta = mouse - r.center;
            float radius = r.width * 0.5f;
            return delta.sqrMagnitude <= radius * radius;
        }

        public static Rect GetHandleScreenRect(Rect screenRect, CommentCorner corner, float handleSize)
        {
            float h = handleSize;
            switch (corner)
            {
                case CommentCorner.TopLeft:
                    return new Rect(screenRect.xMin - h * 0.5f, screenRect.yMin - h * 0.5f, h, h);
                case CommentCorner.TopRight:
                    return new Rect(screenRect.xMax - h * 0.5f, screenRect.yMin - h * 0.5f, h, h);
                case CommentCorner.BottomRight:
                    return new Rect(screenRect.xMax - h * 0.5f, screenRect.yMax - h * 0.5f, h, h);
                case CommentCorner.BottomLeft:
                    return new Rect(screenRect.xMin - h * 0.5f, screenRect.yMax - h * 0.5f, h, h);
                default:
                    return default;
            }
        }

        public static CommentCorner HitTestCorner(Rect screenRect, Vector2 mouse, float hitSize)
        {
            if (GetHandleScreenRect(screenRect, CommentCorner.TopLeft, hitSize).Contains(mouse))
                return CommentCorner.TopLeft;
            if (GetHandleScreenRect(screenRect, CommentCorner.TopRight, hitSize).Contains(mouse))
                return CommentCorner.TopRight;
            if (GetHandleScreenRect(screenRect, CommentCorner.BottomRight, hitSize).Contains(mouse))
                return CommentCorner.BottomRight;
            if (GetHandleScreenRect(screenRect, CommentCorner.BottomLeft, hitSize).Contains(mouse))
                return CommentCorner.BottomLeft;
            return CommentCorner.None;
        }

        public static Rect ResizeFromCorner(Rect graphRect, CommentCorner corner, Vector2 graphMouse)
        {
            float xMin = graphRect.xMin;
            float xMax = graphRect.xMax;
            float yMin = graphRect.yMin;
            float yMax = graphRect.yMax;
            switch (corner)
            {
                case CommentCorner.TopLeft:
                    xMin = Mathf.Min(graphMouse.x, xMax - MinWidth);
                    yMin = Mathf.Min(graphMouse.y, yMax - MinHeight);
                    break;
                case CommentCorner.TopRight:
                    xMax = Mathf.Max(graphMouse.x, xMin + MinWidth);
                    yMin = Mathf.Min(graphMouse.y, yMax - MinHeight);
                    break;
                case CommentCorner.BottomRight:
                    xMax = Mathf.Max(graphMouse.x, xMin + MinWidth);
                    yMax = Mathf.Max(graphMouse.y, yMin + MinHeight);
                    break;
                case CommentCorner.BottomLeft:
                    xMin = Mathf.Min(graphMouse.x, xMax - MinWidth);
                    yMax = Mathf.Max(graphMouse.y, yMin + MinHeight);
                    break;
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Rect NormalizeAndClamp(Rect rect)
        {
            var n = Rect.MinMaxRect(
                Mathf.Min(rect.xMin, rect.xMax),
                Mathf.Min(rect.yMin, rect.yMax),
                Mathf.Max(rect.xMin, rect.xMax),
                Mathf.Max(rect.yMin, rect.yMax));
            if (n.width < MinWidth)
                n.width = MinWidth;
            if (n.height < MinHeight)
                n.height = MinHeight;
            return n;
        }

        public static bool IsCreateSizeValid(Rect unclampedGraphRect)
        {
            float w = Mathf.Abs(unclampedGraphRect.width);
            float h = Mathf.Abs(unclampedGraphRect.height);
            return w >= MinWidth * 0.5f && h >= MinHeight * 0.5f;
        }
    }

    public sealed class CommentColorState
    {
        public bool HasColor;
        public float Hue = AsmdefGraphCommentPalette.DefaultHue;
    }

    public static class AsmdefGraphCommentPalette
    {
        public const float DefaultHue = 0.13f;

        public static void GetColors(float hue, out Color fill, out Color border, out Color borderSelected)
        {
            hue = Mathf.Repeat(hue, 1f);
            var rgb = Color.HSVToRGB(hue, 0.74f, 0.95f);
            fill = new Color(rgb.r, rgb.g, rgb.b, 0.06f);
            border = new Color(rgb.r * 0.95f, rgb.g * 0.88f, rgb.b * 0.6f, 0.375f);
            var selected = Color.HSVToRGB(hue, 0.7f, 1f);
            borderSelected = new Color(selected.r, selected.g, selected.b, 0.475f);
        }

        public static Color Swatch(float hue)
        {
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.75f, 0.95f);
        }

        public static float NextRandomHue(float current)
        {
            float hue = UnityEngine.Random.value;
            float dist = Mathf.Abs(hue - current);
            if (dist > 0.5f)
                dist = 1f - dist;
            if (dist < 0.12f)
                hue = Mathf.Repeat(current + 0.37f, 1f);
            return hue;
        }
    }
}
