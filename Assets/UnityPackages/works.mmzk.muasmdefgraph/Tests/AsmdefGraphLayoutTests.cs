using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph.Tests
{
    public class AsmdefGraphLayoutTests
    {
        [Test]
        public void ComputeRects_OneLevelOneNode_ReturnsSingleRectWithExpectedSize()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "NodeA" }
            };
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, Vector2.zero, 1f, 10f, 40f);
            Assert.AreEqual(1, rects.Count);
            Assert.IsTrue(rects.ContainsKey("NodeA"));
            var r = rects["NodeA"];
            Assert.AreEqual(10f, r.x);
            Assert.AreEqual(40f, r.y);
            Assert.AreEqual(AsmdefGraphLayout.NodeWidth, r.width);
            Assert.AreEqual(AsmdefGraphLayout.NodeHeight, r.height);
        }

        [Test]
        public void ComputeRects_TwoLevels_SecondLevelHasCorrectX()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "Root" },
                new List<string> { "Child" }
            };
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, Vector2.zero, 1f, 10f, 40f);
            Assert.AreEqual(2, rects.Count);
            var rootRect = rects["Root"];
            var childRect = rects["Child"];
            Assert.AreEqual(10f, rootRect.x);
            Assert.AreEqual(10f + AsmdefGraphLayout.LevelXSpacing, childRect.x);
        }

        [Test]
        public void ComputeRects_TwoNodesInSameLevel_SecondNodeHasCorrectY()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "A", "B" }
            };
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, Vector2.zero, 1f, 10f, 40f);
            Assert.AreEqual(2, rects.Count);
            var aRect = rects["A"];
            var bRect = rects["B"];
            float expectedBY = 40f + (AsmdefGraphLayout.NodeHeight + AsmdefGraphLayout.NodeYSpacing);
            Assert.AreEqual(expectedBY, bRect.y);
        }

        [Test]
        public void ComputeRects_WithNodeOffsets_AppliesOffset()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "NodeA" }
            };
            var offsets = new Dictionary<string, Vector2> { { "NodeA", new Vector2(50f, 20f) } };
            var rects = AsmdefGraphLayout.ComputeRects(levels, offsets, Vector2.zero, 1f, 10f, 40f);
            var r = rects["NodeA"];
            Assert.AreEqual(10f + 50f, r.x);
            Assert.AreEqual(40f + 20f, r.y);
        }

        [Test]
        public void ComputeRects_WithPanOffset_ShiftsAllRects()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "NodeA" }
            };
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, new Vector2(100f, 50f), 1f, 10f, 40f);
            var r = rects["NodeA"];
            Assert.AreEqual(10f + 100f, r.x);
            Assert.AreEqual(40f + 50f, r.y);
        }

        [Test]
        public void ComputeRects_WithZoomLevel_ScalesSizeAndPosition()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "NodeA" }
            };
            const float zoom = 0.5f;
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, Vector2.zero, zoom, 10f, 40f);
            var r = rects["NodeA"];
            Assert.AreEqual(AsmdefGraphLayout.NodeWidth * zoom, r.width);
            Assert.AreEqual(AsmdefGraphLayout.NodeHeight * zoom, r.height);
            Assert.AreEqual(10f, r.x);
            Assert.AreEqual(40f, r.y);
        }

        [Test]
        public void ComputeRects_EmptyLevels_ReturnsEmptyDictionary()
        {
            var levels = new List<List<string>>();
            var rects = AsmdefGraphLayout.ComputeRects(levels, null, Vector2.zero, 1f);
            Assert.AreEqual(0, rects.Count);
        }

        [Test]
        public void ComputeRects_OffsetWithZoom_ScalesOffset()
        {
            var levels = new List<List<string>>
            {
                new List<string> { "NodeA" }
            };
            var offsets = new Dictionary<string, Vector2> { { "NodeA", new Vector2(100f, 0f) } };
            const float zoom = 0.5f;
            var rects = AsmdefGraphLayout.ComputeRects(levels, offsets, Vector2.zero, zoom, 10f, 40f);
            var r = rects["NodeA"];
            Assert.AreEqual(10f + 100f * zoom, r.x);
        }
    }
}
