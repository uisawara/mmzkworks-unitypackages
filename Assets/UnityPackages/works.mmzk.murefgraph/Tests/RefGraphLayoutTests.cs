using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muRefgraph.Tests
{
    public class RefGraphLayoutTests
    {
        private static RefGraph CreateGraph()
        {
            var g = new RefGraph();
            g.AddNode(new RefGraphNode { Id = RefGraph.RootId, Kind = RefGraphNodeKind.Root, Label = "Root" });
            // Inspector 順: B, A, B, A
            var assemblies = new[] { "B.Asm", "A.Asm", "B.Asm", "A.Asm" };
            for (int i = 0; i < assemblies.Length; i++)
            {
                var id = RefGraphBuilder.ComponentId(i);
                g.AddNode(new RefGraphNode { Id = id, Kind = RefGraphNodeKind.Component, AssemblyName = assemblies[i], Order = i });
                g.AddEdge(RefGraph.RootId, id);
            }
            g.AddNode(new RefGraphNode { Id = "r:1", Kind = RefGraphNodeKind.Reference, Order = 0 });
            g.AddNode(new RefGraphNode { Id = "r:2", Kind = RefGraphNodeKind.Reference, Order = 1 });
            g.AddEdge("c:3", "r:1", "a");
            g.AddEdge("c:0", "r:2", "b");
            return g;
        }

        private static RefGraphLayoutResult Compute(RefGraph g, RefGraphLayoutMode mode, Dictionary<string, Vector2> offsets = null)
        {
            return RefGraphLayout.Compute(g, mode, offsets, Vector2.zero, 1f, Vector2.zero);
        }

        [Test]
        public void Compute_EmptyGraph_ReturnsNoRects()
        {
            var result = Compute(new RefGraph(), RefGraphLayoutMode.ComponentOrder);
            Assert.That(result.Rects, Is.Empty);
            Assert.That(result.Groups, Is.Empty);
        }

        [Test]
        public void ComponentOrder_PlacesComponentsInInspectorOrderInOneColumn()
        {
            var r = Compute(CreateGraph(), RefGraphLayoutMode.ComponentOrder).Rects;

            var ys = Enumerable.Range(0, 4).Select(i => r[RefGraphBuilder.ComponentId(i)].y).ToList();
            Assert.That(ys, Is.Ordered.Ascending);
            Assert.That(Enumerable.Range(0, 4).Select(i => r[RefGraphBuilder.ComponentId(i)].x).Distinct().Count(), Is.EqualTo(1));
            Assert.That(r[RefGraph.RootId].x, Is.LessThan(r["c:0"].x));
            Assert.That(r["r:1"].x, Is.GreaterThan(r["c:0"].x));
        }

        [Test]
        public void ByAssembly_GroupsComponentsContiguouslyInNameOrder()
        {
            var result = Compute(CreateGraph(), RefGraphLayoutMode.ByAssembly);
            var r = result.Rects;

            // A.Asm (c:1, c:3) が B.Asm (c:0, c:2) より上、グループ内は Inspector 順
            Assert.That(r["c:1"].y, Is.LessThan(r["c:3"].y));
            Assert.That(r["c:3"].y, Is.LessThan(r["c:0"].y));
            Assert.That(r["c:0"].y, Is.LessThan(r["c:2"].y));

            Assert.That(result.Groups.Select(g => g.AssemblyName), Is.EqualTo(new[] { "A.Asm", "B.Asm" }));
            var a = result.Groups[0].Rect;
            var b = result.Groups[1].Rect;
            Assert.That(a.Overlaps(b), Is.False);
            foreach (var id in new[] { "c:1", "c:3" })
                Assert.That(Contains(a, r[id]), Is.True, id);
            foreach (var id in new[] { "c:0", "c:2" })
                Assert.That(Contains(b, r[id]), Is.True, id);
        }

        [Test]
        public void ComponentOrder_HasNoGroups()
        {
            Assert.That(Compute(CreateGraph(), RefGraphLayoutMode.ComponentOrder).Groups, Is.Empty);
        }

        [Test]
        public void Nodes_DoNotOverlap([Values] RefGraphLayoutMode mode)
        {
            var rects = Compute(CreateGraph(), mode).Rects.Values.ToList();
            for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                Assert.That(rects[i].Overlaps(rects[j]), Is.False, $"{rects[i]} vs {rects[j]}");
        }

        [Test]
        public void References_AreOrderedBySourcePosition()
        {
            // ComponentOrder: r:2 は c:0 (上) から、r:1 は c:3 (下) から参照される
            var r = Compute(CreateGraph(), RefGraphLayoutMode.ComponentOrder).Rects;
            Assert.That(r["r:2"].y, Is.LessThan(r["r:1"].y));
        }

        [Test]
        public void Offsets_AreAppliedAndGroupFrameFollows()
        {
            var g = CreateGraph();
            var before = Compute(g, RefGraphLayoutMode.ByAssembly);
            var offsets = new Dictionary<string, Vector2> { ["c:1"] = new Vector2(500f, 0f) };
            var after = Compute(g, RefGraphLayoutMode.ByAssembly, offsets);

            Assert.That(after.Rects["c:1"].x, Is.EqualTo(before.Rects["c:1"].x + 500f).Within(0.01f));
            Assert.That(Contains(after.Groups[0].Rect, after.Rects["c:1"]), Is.True);
        }

        [Test]
        public void ZoomAndPan_TransformRects()
        {
            var g = CreateGraph();
            var baseRect = Compute(g, RefGraphLayoutMode.ComponentOrder).Rects["c:1"];
            var r = RefGraphLayout.Compute(g, RefGraphLayoutMode.ComponentOrder, null, new Vector2(5f, 7f), 2f, new Vector2(10f, 20f)).Rects["c:1"];

            Assert.That(r.x, Is.EqualTo(10f + 5f + baseRect.x * 2f).Within(0.01f));
            Assert.That(r.y, Is.EqualTo(20f + 7f + baseRect.y * 2f).Within(0.01f));
            Assert.That(r.width, Is.EqualTo(RefGraphLayout.NodeWidth * 2f).Within(0.01f));
        }

        /// <summary>Root (c:0, c:1) with a child "g:1" owning g:1/c:0, g:1/c:1.</summary>
        private static RefGraph CreateGraphWithChild()
        {
            var g = new RefGraph();
            g.AddNode(new RefGraphNode { Id = RefGraph.RootId, Kind = RefGraphNodeKind.Root, Order = 0 });
            g.AddNode(new RefGraphNode { Id = "g:1", Kind = RefGraphNodeKind.ChildObject, Order = 1, Depth = 1 });
            g.AddEdge(RefGraph.RootId, "g:1");
            var comps = new[] { ("c:0", RefGraph.RootId, "B.Asm"), ("c:1", RefGraph.RootId, "A.Asm"), ("g:1/c:0", "g:1", "B.Asm"), ("g:1/c:1", "g:1", "A.Asm") };
            for (int i = 0; i < comps.Length; i++)
            {
                var (id, owner, asm) = comps[i];
                g.AddNode(new RefGraphNode { Id = id, Kind = RefGraphNodeKind.Component, AssemblyName = asm, OwnerId = owner, Order = i });
                g.AddEdge(owner, id);
            }
            return g;
        }

        [Test]
        public void ComponentOrder_WithChild_PlacesEachGameObjectBesideItsComponents()
        {
            var r = Compute(CreateGraphWithChild(), RefGraphLayoutMode.ComponentOrder).Rects;

            // 子は深さに応じて字下げされる
            Assert.That(r["g:1"].x, Is.EqualTo(r[RefGraph.RootId].x + RefGraphLayout.TreeIndent).Within(0.01f));
            Assert.That(r["g:1"].xMax, Is.LessThan(r["g:1/c:0"].xMin));
            Assert.That(r["c:1"].yMax, Is.LessThan(r["g:1/c:0"].yMin));
            // 各 GameObject は自分の Component ブロックの縦範囲内にある
            Assert.That(r[RefGraph.RootId].center.y, Is.InRange(r["c:0"].yMin, r["c:1"].yMax));
            Assert.That(r["g:1"].center.y, Is.InRange(r["g:1/c:0"].yMin, r["g:1/c:1"].yMax));
        }

        [Test]
        public void WithChild_NodesDoNotOverlap([Values] RefGraphLayoutMode mode)
        {
            var rects = Compute(CreateGraphWithChild(), mode).Rects.Values.ToList();
            for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                Assert.That(rects[i].Overlaps(rects[j]), Is.False, $"{rects[i]} vs {rects[j]}");
        }

        [Test]
        public void ByAssembly_WithChild_GroupsComponentsAcrossGameObjects()
        {
            var result = Compute(CreateGraphWithChild(), RefGraphLayoutMode.ByAssembly);

            Assert.That(result.Groups.Select(g => g.AssemblyName), Is.EqualTo(new[] { "A.Asm", "B.Asm" }));
            foreach (var id in new[] { "c:1", "g:1/c:1" })
                Assert.That(Contains(result.Groups[0].Rect, result.Rects[id]), Is.True, id);
            foreach (var id in new[] { "c:0", "g:1/c:0" })
                Assert.That(Contains(result.Groups[1].Rect, result.Rects[id]), Is.True, id);
        }

        [Test]
        public void ByAssembly_WithChild_KeepsGameObjectsInHierarchyOrder()
        {
            var g = CreateGraphWithChild();
            // 子の Component を先頭に並ぶアセンブリに寄せても、親より上には来ない
            g.TryGetNode("g:1/c:1", out var n);
            n.AssemblyName = "0.First";
            g.TryGetNode("g:1/c:0", out n);
            n.AssemblyName = "0.First";
            var r = Compute(g, RefGraphLayoutMode.ByAssembly).Rects;

            Assert.That(r[RefGraph.RootId].yMax, Is.LessThanOrEqualTo(r["g:1"].yMin));
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return outer.xMin <= inner.xMin && outer.yMin <= inner.yMin && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
        }
    }
}
