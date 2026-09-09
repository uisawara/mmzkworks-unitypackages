using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefGraphCommentBlockTests
    {
        [Test]
        public void ToScreenRect_AppliesOriginPanAndZoom()
        {
            var graph = new Rect(10f, 20f, 80f, 40f);
            var origin = new Vector2(10f, 20f);
            var pan = new Vector2(5f, 7f);
            const float zoom = 2f;
            var screen = AsmdefGraphCommentBlock.ToScreenRect(graph, origin, pan, zoom);
            Assert.AreEqual(10f + 5f + 10f * zoom, screen.x);
            Assert.AreEqual(20f + 7f + 20f * zoom, screen.y);
            Assert.AreEqual(80f * zoom, screen.width);
            Assert.AreEqual(40f * zoom, screen.height);
        }

        [Test]
        public void ToGraphRect_InvertsToScreenRect()
        {
            var graph = new Rect(12f, 34f, 90f, 50f);
            var origin = new Vector2(8f, 16f);
            var pan = new Vector2(-3f, 4f);
            const float zoom = 0.5f;
            var screen = AsmdefGraphCommentBlock.ToScreenRect(graph, origin, pan, zoom);
            var back = AsmdefGraphCommentBlock.ToGraphRect(screen, origin, pan, zoom);
            Assert.AreEqual(graph.x, back.x, 0.001f);
            Assert.AreEqual(graph.y, back.y, 0.001f);
            Assert.AreEqual(graph.width, back.width, 0.001f);
            Assert.AreEqual(graph.height, back.height, 0.001f);
        }

        [Test]
        public void NormalizeAndClamp_FlipsInvertedRectAndEnforcesMinSize()
        {
            var inverted = new Rect(100f, 80f, -10f, -5f);
            var clamped = AsmdefGraphCommentBlock.NormalizeAndClamp(inverted);
            Assert.AreEqual(90f, clamped.x, 0.001f);
            Assert.AreEqual(75f, clamped.y, 0.001f);
            Assert.AreEqual(AsmdefGraphCommentBlock.MinWidth, clamped.width);
            Assert.AreEqual(AsmdefGraphCommentBlock.MinHeight, clamped.height);
        }

        [Test]
        public void IsCreateSizeValid_RejectsTooSmallRect()
        {
            var tiny = new Rect(0f, 0f, 10f, 10f);
            Assert.IsFalse(AsmdefGraphCommentBlock.IsCreateSizeValid(tiny));
            var ok = new Rect(0f, 0f, AsmdefGraphCommentBlock.MinWidth * 0.5f, AsmdefGraphCommentBlock.MinHeight * 0.5f);
            Assert.IsTrue(AsmdefGraphCommentBlock.IsCreateSizeValid(ok));
        }

        [Test]
        public void ResizeFromCorner_BottomRightKeepsOriginAndClampsMin()
        {
            var start = new Rect(10f, 20f, 100f, 80f);
            var resized = AsmdefGraphCommentBlock.ResizeFromCorner(start, CommentCorner.BottomRight, new Vector2(200f, 180f));
            Assert.AreEqual(10f, resized.x);
            Assert.AreEqual(20f, resized.y);
            Assert.AreEqual(190f, resized.width, 0.001f);
            Assert.AreEqual(160f, resized.height, 0.001f);

            var tooSmall = AsmdefGraphCommentBlock.ResizeFromCorner(start, CommentCorner.BottomRight, new Vector2(11f, 21f));
            Assert.AreEqual(AsmdefGraphCommentBlock.MinWidth, tooSmall.width);
            Assert.AreEqual(AsmdefGraphCommentBlock.MinHeight, tooSmall.height);
        }

        [Test]
        public void ResizeFromCorner_TopLeftMovesOriginAndKeepsOpposite()
        {
            var start = new Rect(100f, 80f, 120f, 90f);
            var resized = AsmdefGraphCommentBlock.ResizeFromCorner(start, CommentCorner.TopLeft, new Vector2(40f, 30f));
            Assert.AreEqual(40f, resized.xMin, 0.001f);
            Assert.AreEqual(30f, resized.yMin, 0.001f);
            Assert.AreEqual(220f, resized.xMax, 0.001f);
            Assert.AreEqual(170f, resized.yMax, 0.001f);
        }

        [Test]
        public void HitTestCorner_DetectsEachCorner()
        {
            var screen = new Rect(100f, 100f, 200f, 80f);
            float hit = AsmdefGraphCommentBlock.HandleHitSizeScreen;
            Assert.AreEqual(CommentCorner.TopLeft, AsmdefGraphCommentBlock.HitTestCorner(screen, new Vector2(100f, 100f), hit));
            Assert.AreEqual(CommentCorner.TopRight, AsmdefGraphCommentBlock.HitTestCorner(screen, new Vector2(300f, 100f), hit));
            Assert.AreEqual(CommentCorner.BottomRight, AsmdefGraphCommentBlock.HitTestCorner(screen, new Vector2(300f, 180f), hit));
            Assert.AreEqual(CommentCorner.BottomLeft, AsmdefGraphCommentBlock.HitTestCorner(screen, new Vector2(100f, 180f), hit));
            Assert.AreEqual(CommentCorner.None, AsmdefGraphCommentBlock.HitTestCorner(screen, new Vector2(200f, 140f), hit));
        }

        [Test]
        public void GetLabelScreenRect_IsInsideTopCenter()
        {
            var block = new AsmdefGraphCommentBlock
            {
                Id = "id",
                Label = "Hello",
                GraphRect = new Rect(0f, 0f, 200f, 80f)
            };
            var label = block.GetLabelScreenRect(Vector2.zero, Vector2.zero, 1f);
            var screen = block.GetScreenRect(Vector2.zero, Vector2.zero, 1f);
            float expectedLeft = screen.xMin
                + AsmdefGraphCommentBlock.LabelHorizontalInsetScreen
                + AsmdefGraphCommentBlock.ColorButtonSizeScreen
                + AsmdefGraphCommentBlock.ColorButtonGapScreen;
            Assert.AreEqual(expectedLeft, label.xMin, 0.001f);
            Assert.AreEqual(screen.xMax - AsmdefGraphCommentBlock.LabelHorizontalInsetScreen, label.xMax, 0.001f);
            Assert.AreEqual(screen.yMin, label.yMin, 0.001f);
            Assert.LessOrEqual(label.yMax, screen.yMax);
            Assert.AreEqual(AsmdefGraphCommentBlock.LabelHeightGraph, label.height, 0.001f);
        }

        [Test]
        public void GetColorButtonScreenRect_IsInsideTopLeftInset()
        {
            var block = new AsmdefGraphCommentBlock
            {
                Id = "id",
                Label = "Hello",
                GraphRect = new Rect(0f, 0f, 200f, 80f)
            };
            var btn = block.GetColorButtonScreenRect(Vector2.zero, Vector2.zero, 1f);
            var screen = block.GetScreenRect(Vector2.zero, Vector2.zero, 1f);
            var label = block.GetLabelScreenRect(Vector2.zero, Vector2.zero, 1f);
            Assert.AreEqual(screen.xMin + AsmdefGraphCommentBlock.LabelHorizontalInsetScreen, btn.xMin, 0.001f);
            Assert.AreEqual(AsmdefGraphCommentBlock.ColorButtonSizeScreen, btn.width, 0.001f);
            Assert.AreEqual(AsmdefGraphCommentBlock.ColorButtonSizeScreen, btn.height, 0.001f);
            Assert.Less(btn.xMax, label.xMin);
            Assert.IsTrue(block.HitTestColorButton(btn.center, Vector2.zero, Vector2.zero, 1f));
            Assert.IsFalse(block.HitTestColorButton(btn.center + new Vector2(20f, 0f), Vector2.zero, Vector2.zero, 1f));
        }

        [Test]
        public void Create_AssignsIdDefaultLabelAndClampedRect()
        {
            var block = AsmdefGraphCommentBlock.Create(new Rect(0f, 0f, 10f, 10f));
            Assert.IsFalse(string.IsNullOrEmpty(block.Id));
            Assert.AreEqual(AsmdefGraphCommentBlock.DefaultLabel, block.Label);
            Assert.AreEqual(AsmdefGraphCommentBlock.MinWidth, block.GraphRect.width);
            Assert.AreEqual(AsmdefGraphCommentBlock.MinHeight, block.GraphRect.height);
            Assert.AreEqual(AsmdefGraphCommentPalette.DefaultHue, block.Hue, 0.0001f);
        }

        [Test]
        public void ToEntryAndFromEntry_RoundTrip()
        {
            var original = new AsmdefGraphCommentBlock
            {
                Id = "abc",
                Label = "Notes",
                GraphRect = new Rect(5f, 6f, 120f, 70f),
                Hue = 0.42f
            };
            var restored = AsmdefGraphCommentBlock.FromEntry(original.ToEntry());
            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Id, restored.Id);
            Assert.AreEqual(original.Label, restored.Label);
            Assert.AreEqual(original.GraphRect.x, restored.GraphRect.x);
            Assert.AreEqual(original.GraphRect.y, restored.GraphRect.y);
            Assert.AreEqual(original.GraphRect.width, restored.GraphRect.width);
            Assert.AreEqual(original.GraphRect.height, restored.GraphRect.height);
            Assert.AreEqual(0.42f, restored.Hue, 0.0001f);
        }

        [Test]
        public void FromEntry_MissingColor_UsesDefaultHue()
        {
            var restored = AsmdefGraphCommentBlock.FromEntry(new CommentBlockEntry
            {
                id = "old",
                label = "Legacy",
                x = 0f,
                y = 0f,
                width = 80f,
                height = 40f
            });
            Assert.IsNotNull(restored);
            Assert.AreEqual(AsmdefGraphCommentPalette.DefaultHue, restored.Hue, 0.0001f);
        }

        [Test]
        public void GraphRectFromScreenDrag_NormalizesCorners()
        {
            var origin = new Vector2(10f, 20f);
            var pan = Vector2.zero;
            var rect = AsmdefGraphCommentBlock.GraphRectFromScreenDrag(
                new Vector2(110f, 120f), new Vector2(30f, 40f), origin, pan, 1f);
            Assert.AreEqual(20f, rect.xMin, 0.001f);
            Assert.AreEqual(20f, rect.yMin, 0.001f);
            Assert.AreEqual(100f, rect.xMax, 0.001f);
            Assert.AreEqual(100f, rect.yMax, 0.001f);
        }

        [Test]
        public void NextRandomHue_DiffersFromCurrent()
        {
            const float current = 0.13f;
            float next = AsmdefGraphCommentPalette.NextRandomHue(current);
            float dist = Mathf.Abs(next - current);
            if (dist > 0.5f)
                dist = 1f - dist;
            Assert.GreaterOrEqual(dist, 0.12f);
        }
    }
}
