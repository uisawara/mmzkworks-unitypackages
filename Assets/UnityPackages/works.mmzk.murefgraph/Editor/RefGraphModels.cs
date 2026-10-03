using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    public enum RefGraphNodeKind
    {
        Root,
        /// <summary>Descendant GameObject of the root (only when children are included).</summary>
        ChildObject,
        Component,
        MissingScript,
        Reference,
    }

    public enum RefGraphLayoutMode
    {
        /// <summary>Components in Inspector order.</summary>
        ComponentOrder,

        /// <summary>Components grouped by the assembly their type belongs to.</summary>
        ByAssembly,
    }

    public sealed class RefGraphNode
    {
        public string Id;
        public RefGraphNodeKind Kind;
        public string Label;
        public string SubLabel;
        /// <summary>Assembly name of the component type. Component / MissingScript only.</summary>
        public string AssemblyName;
        public string Tooltip;
        /// <summary>Id of the GameObject node that owns this component. Component / MissingScript only.</summary>
        public string OwnerId;
        public Object Target;
        /// <summary>Discovery order within the same kind (Hierarchy order for GameObjects, then Inspector order for components).</summary>
        public int Order;
        /// <summary>Hierarchy depth below the root (0 for the root). GameObject nodes only.</summary>
        public int Depth;

        public bool IsGameObject => Kind == RefGraphNodeKind.Root || Kind == RefGraphNodeKind.ChildObject;
        public bool IsComponent => Kind == RefGraphNodeKind.Component || Kind == RefGraphNodeKind.MissingScript;
    }

    public sealed class RefGraphEdge
    {
        public string FromId;
        public string ToId;
        /// <summary>Serialized field paths that hold this reference. Empty for hierarchy (parent -> child) and ownership (GameObject -> component) edges.</summary>
        public List<string> FieldPaths = new List<string>();
    }

    public sealed class RefGraph
    {
        public const string RootId = "root";

        public readonly List<RefGraphNode> Nodes = new List<RefGraphNode>();
        public readonly List<RefGraphEdge> Edges = new List<RefGraphEdge>();
        private readonly Dictionary<string, RefGraphNode> _byId = new Dictionary<string, RefGraphNode>();

        public RefGraphNode Root => TryGetNode(RootId, out var n) ? n : null;

        public void AddNode(RefGraphNode node)
        {
            Nodes.Add(node);
            _byId[node.Id] = node;
        }

        public bool TryGetNode(string id, out RefGraphNode node)
        {
            if (id == null)
            {
                node = null;
                return false;
            }
            return _byId.TryGetValue(id, out node);
        }

        /// <summary>Adds an edge or, if one already exists between the same nodes, appends the field path to it.</summary>
        public RefGraphEdge AddEdge(string fromId, string toId, string fieldPath = null)
        {
            RefGraphEdge edge = null;
            foreach (var e in Edges)
            {
                if (e.FromId == fromId && e.ToId == toId)
                {
                    edge = e;
                    break;
                }
            }
            if (edge == null)
            {
                edge = new RefGraphEdge { FromId = fromId, ToId = toId };
                Edges.Add(edge);
            }
            if (!string.IsNullOrEmpty(fieldPath) && !edge.FieldPaths.Contains(fieldPath))
                edge.FieldPaths.Add(fieldPath);
            return edge;
        }
    }

    public readonly struct RefGraphGroupFrame
    {
        public readonly string AssemblyName;
        public readonly Rect Rect;

        public RefGraphGroupFrame(string assemblyName, Rect rect)
        {
            AssemblyName = assemblyName;
            Rect = rect;
        }
    }

    public sealed class RefGraphLayoutResult
    {
        public readonly Dictionary<string, Rect> Rects = new Dictionary<string, Rect>();
        public readonly List<RefGraphGroupFrame> Groups = new List<RefGraphGroupFrame>();
    }
}
