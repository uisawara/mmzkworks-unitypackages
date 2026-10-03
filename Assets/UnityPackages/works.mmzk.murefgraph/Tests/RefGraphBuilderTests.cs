using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mmzkworks.muRefgraph.Tests
{
    public class RefGraphBuilderTests
    {
        private GameObject _go;
        private GameObject _other;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Target");
            _other = new GameObject("Other");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_other);
        }

        [Test]
        public void Build_Null_ReturnsEmptyGraph()
        {
            var graph = RefGraphBuilder.Build(null);
            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void Build_CreatesRootAndComponentNodesInInspectorOrder()
        {
            _go.AddComponent<RefGraphTestBehaviour>();

            var graph = RefGraphBuilder.Build(_go);

            Assert.That(graph.Root, Is.Not.Null);
            Assert.That(graph.Root.Label, Is.EqualTo("Target"));
            var components = graph.Nodes.Where(n => n.Kind == RefGraphNodeKind.Component).OrderBy(n => n.Order).ToList();
            Assert.That(components.Select(n => n.Label), Is.EqualTo(new[] { "Transform", "RefGraphTestBehaviour" }));
            Assert.That(components[0].AssemblyName, Is.EqualTo(typeof(Transform).Assembly.GetName().Name));
            Assert.That(components[1].AssemblyName, Is.EqualTo("works.mmzk.murefgraph.Tests.Support"));
            foreach (var c in components)
                Assert.That(graph.Edges.Any(e => e.FromId == RefGraph.RootId && e.ToId == c.Id), Is.True);
        }

        [Test]
        public void Build_ExternalReferences_AreDedupedWithFieldPathsAggregated()
        {
            var b = _go.AddComponent<RefGraphTestBehaviour>();
            b.other = _other;
            b.others = new[] { _other, null };

            var graph = RefGraphBuilder.Build(_go);

            var refs = graph.Nodes.Where(n => n.Kind == RefGraphNodeKind.Reference).ToList();
            Assert.That(refs.Count, Is.EqualTo(1));
            Assert.That(refs[0].Label, Is.EqualTo("Other"));
            Assert.That(refs[0].Target, Is.SameAs(_other));

            var edge = graph.Edges.Single(e => e.ToId == refs[0].Id);
            Assert.That(edge.FromId, Is.EqualTo(RefGraphBuilder.ComponentId(1)));
            Assert.That(edge.FieldPaths, Is.EquivalentTo(new[] { "other", "others[0]" }));
        }

        [Test]
        public void Build_SelfReferences_PointToRootAndComponentNodes()
        {
            var b = _go.AddComponent<RefGraphTestBehaviour>();
            b.self = _go;
            b.selfTransform = _go.transform;

            var graph = RefGraphBuilder.Build(_go);

            var from = RefGraphBuilder.ComponentId(1);
            Assert.That(graph.Nodes.Any(n => n.Kind == RefGraphNodeKind.Reference), Is.False);
            var toRoot = graph.Edges.Single(e => e.FromId == from && e.ToId == RefGraph.RootId);
            Assert.That(toRoot.FieldPaths, Is.EqualTo(new[] { "self" }));
            var toTransform = graph.Edges.Single(e => e.FromId == from && e.ToId == RefGraphBuilder.ComponentId(0));
            Assert.That(toTransform.FieldPaths, Is.EqualTo(new[] { "selfTransform" }));
        }

        [Test]
        public void Build_ExcludesOwnershipAndHierarchyProperties()
        {
            var child = new GameObject("Child");
            child.transform.SetParent(_go.transform);
            _go.AddComponent<RefGraphTestBehaviour>();

            var graph = RefGraphBuilder.Build(_go);

            // m_GameObject / m_Script / m_Children は辿らない
            Assert.That(graph.Nodes.Any(n => n.Kind == RefGraphNodeKind.Reference), Is.False);
            Assert.That(graph.Edges.Any(e => e.FromId != RefGraph.RootId), Is.False);
        }

        [Test]
        public void Build_WithoutChildren_ChildReferenceIsExternal()
        {
            var child = new GameObject("Child");
            child.transform.SetParent(_go.transform);
            var b = _go.AddComponent<RefGraphTestBehaviour>();
            b.other = child;

            var graph = RefGraphBuilder.Build(_go);

            Assert.That(graph.Nodes.Any(n => n.Kind == RefGraphNodeKind.ChildObject), Is.False);
            var reference = graph.Nodes.Single(n => n.Kind == RefGraphNodeKind.Reference);
            Assert.That(reference.Target, Is.SameAs(child));
        }

        [Test]
        public void Build_WithChildren_AddsDescendantsInHierarchyOrderWithHierarchyEdges()
        {
            var a = new GameObject("A");
            a.transform.SetParent(_go.transform);
            var a1 = new GameObject("A1");
            a1.transform.SetParent(a.transform);
            var b = new GameObject("B");
            b.transform.SetParent(_go.transform);

            var graph = RefGraphBuilder.Build(_go, includeChildren: true);

            var children = graph.Nodes.Where(n => n.Kind == RefGraphNodeKind.ChildObject).OrderBy(n => n.Order).ToList();
            Assert.That(children.Select(n => n.Label), Is.EqualTo(new[] { "A", "A1", "B" }));
            Assert.That(children.Select(n => n.SubLabel), Is.EqualTo(new[] { "A", "A/A1", "B" }));
            Assert.That(children.Select(n => n.Depth), Is.EqualTo(new[] { 1, 2, 1 }));
            Assert.That(graph.Root.Depth, Is.EqualTo(0));

            string IdOf(string label) => graph.Nodes.Single(n => n.IsGameObject && n.Label == label).Id;
            Assert.That(graph.Edges.Any(e => e.FromId == RefGraph.RootId && e.ToId == IdOf("A")), Is.True);
            Assert.That(graph.Edges.Any(e => e.FromId == IdOf("A") && e.ToId == IdOf("A1")), Is.True);
            Assert.That(graph.Edges.Any(e => e.FromId == RefGraph.RootId && e.ToId == IdOf("B")), Is.True);
            Assert.That(graph.Edges.Any(e => e.FromId == RefGraph.RootId && e.ToId == IdOf("A1")), Is.False);

            // 子の Component は子の GameObject に属する
            var a1Transform = graph.Nodes.Single(n => n.IsComponent && n.Target == a1.transform);
            Assert.That(a1Transform.OwnerId, Is.EqualTo(IdOf("A1")));
            Assert.That(graph.Edges.Any(e => e.FromId == IdOf("A1") && e.ToId == a1Transform.Id), Is.True);
        }

        [Test]
        public void Build_WithChildren_ReferencesToChildrenPointToTheirNodes()
        {
            var child = new GameObject("Child");
            child.transform.SetParent(_go.transform);
            var b = _go.AddComponent<RefGraphTestBehaviour>();
            b.other = child;
            b.selfTransform = child.transform;

            var graph = RefGraphBuilder.Build(_go, includeChildren: true);

            Assert.That(graph.Nodes.Any(n => n.Kind == RefGraphNodeKind.Reference), Is.False);
            var from = RefGraphBuilder.ComponentId(1);
            var childNode = graph.Nodes.Single(n => n.Kind == RefGraphNodeKind.ChildObject);
            var childTransform = graph.Nodes.Single(n => n.IsComponent && n.Target == child.transform);
            Assert.That(graph.Edges.Single(e => e.FromId == from && e.ToId == childNode.Id).FieldPaths, Is.EqualTo(new[] { "other" }));
            Assert.That(graph.Edges.Single(e => e.FromId == from && e.ToId == childTransform.Id).FieldPaths, Is.EqualTo(new[] { "selfTransform" }));
        }

        [Test]
        public void FormatFieldPath_CollapsesArrayData()
        {
            Assert.That(RefGraphBuilder.FormatFieldPath("items.Array.data[2].target"), Is.EqualTo("items[2].target"));
            Assert.That(RefGraphBuilder.FormatFieldPath("plain"), Is.EqualTo("plain"));
        }
    }
}
