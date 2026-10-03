using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>Lays out the graph in three columns: GameObjects (root and, optionally, children) | Components | References.</summary>
    public static class RefGraphLayout
    {
        public const float NodeWidth = 220f;
        public const float NodeHeight = 44f;
        public const float ColumnSpacing = 300f;
        public const float NodeYSpacing = 10f;
        public const float GroupHeaderHeight = 20f;
        public const float GroupPadding = 8f;
        public const float GroupSpacing = 18f;
        /// <summary>Horizontal indent per hierarchy depth for child GameObjects.</summary>
        public const float TreeIndent = 24f;

        /// <summary>
        /// Compute screen rects for every node and, in <see cref="RefGraphLayoutMode.ByAssembly"/>, a frame per assembly group.
        /// nodeOffsets are in graph (unzoomed) units; origin is the top-left of the graph area.
        /// </summary>
        public static RefGraphLayoutResult Compute(
            RefGraph graph,
            RefGraphLayoutMode mode,
            Dictionary<string, Vector2> nodeOffsets,
            Vector2 panOffset,
            float zoomLevel,
            Vector2 origin)
        {
            var result = new RefGraphLayoutResult();
            if (graph == null || graph.Nodes.Count == 0)
                return result;

            var basePositions = ComputeBasePositions(graph, mode, out var groups);

            foreach (var kv in basePositions)
            {
                var pos = kv.Value;
                if (nodeOffsets != null && nodeOffsets.TryGetValue(kv.Key, out var off))
                    pos += off;
                result.Rects[kv.Key] = new Rect(
                    origin + panOffset + pos * zoomLevel,
                    new Vector2(NodeWidth, NodeHeight) * zoomLevel);
            }

            foreach (var group in groups)
            {
                bool any = false;
                float xMin = 0, yMin = 0, xMax = 0, yMax = 0;
                foreach (var id in group.Value)
                {
                    if (!result.Rects.TryGetValue(id, out var r))
                        continue;
                    if (!any)
                    {
                        xMin = r.xMin; yMin = r.yMin; xMax = r.xMax; yMax = r.yMax;
                        any = true;
                        continue;
                    }
                    xMin = Mathf.Min(xMin, r.xMin);
                    yMin = Mathf.Min(yMin, r.yMin);
                    xMax = Mathf.Max(xMax, r.xMax);
                    yMax = Mathf.Max(yMax, r.yMax);
                }
                if (!any)
                    continue;
                float pad = GroupPadding * zoomLevel;
                float header = GroupHeaderHeight * zoomLevel;
                result.Groups.Add(new RefGraphGroupFrame(
                    group.Key,
                    Rect.MinMaxRect(xMin - pad, yMin - pad - header, xMax + pad, yMax + pad)));
            }

            return result;
        }

        /// <summary>Base (pre-offset, unzoomed) top-left positions per node, plus assembly groups (ordered) for ByAssembly.</summary>
        private static Dictionary<string, Vector2> ComputeBasePositions(
            RefGraph graph,
            RefGraphLayoutMode mode,
            out List<KeyValuePair<string, List<string>>> groups)
        {
            var positions = new Dictionary<string, Vector2>();
            groups = new List<KeyValuePair<string, List<string>>>();

            var gameObjects = new List<RefGraphNode>();
            var components = new List<RefGraphNode>();
            var references = new List<RefGraphNode>();
            foreach (var n in graph.Nodes)
            {
                if (n.IsGameObject) gameObjects.Add(n);
                else if (n.IsComponent) components.Add(n);
                else if (n.Kind == RefGraphNodeKind.Reference) references.Add(n);
            }
            gameObjects.Sort((a, b) => a.Order.CompareTo(b.Order));
            components.Sort((a, b) => a.Order.CompareTo(b.Order));
            references.Sort((a, b) => a.Order.CompareTo(b.Order));

            // Child GameObjects are indented by depth (tree style), so the columns to the right shift accordingly
            int maxDepth = 0;
            foreach (var g in gameObjects)
                maxDepth = Mathf.Max(maxDepth, g.Depth);
            float componentX = ColumnSpacing + maxDepth * TreeIndent;
            if (mode == RefGraphLayoutMode.ByAssembly)
            {
                // Components column: grouped by assembly (name order), Inspector/Hierarchy order inside each group
                var byAssembly = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var c in components)
                {
                    var key = c.AssemblyName ?? string.Empty;
                    if (!byAssembly.TryGetValue(key, out var list))
                        byAssembly[key] = list = new List<string>();
                    list.Add(c.Id);
                }
                float y = 0f;
                bool first = true;
                foreach (var kv in byAssembly)
                {
                    if (!first)
                        y += GroupSpacing;
                    first = false;
                    y += GroupHeaderHeight + GroupPadding;
                    foreach (var id in kv.Value)
                    {
                        positions[id] = new Vector2(componentX, y);
                        y += NodeHeight + NodeYSpacing;
                    }
                    y += GroupPadding - NodeYSpacing;
                    groups.Add(new KeyValuePair<string, List<string>>(kv.Key, kv.Value));
                }

                // GameObjects column: near the components they own, but kept in Hierarchy order so the tree reads top-down
                StackByBarycenter(graph, gameObjects, n => n.Depth * TreeIndent, positions,
                    (e, n) => e.FromId == n.Id ? e.ToId : null, keepOrder: true);
            }
            else
            {
                // Each GameObject sits beside its own block of components, in Hierarchy order
                var componentsByOwner = new Dictionary<string, List<RefGraphNode>>();
                foreach (var c in components)
                {
                    var owner = c.OwnerId ?? RefGraph.RootId;
                    if (!componentsByOwner.TryGetValue(owner, out var list))
                        componentsByOwner[owner] = list = new List<RefGraphNode>();
                    list.Add(c);
                }
                float y = 0f;
                for (int i = 0; i < gameObjects.Count; i++)
                {
                    if (i > 0)
                        y += GroupSpacing;
                    float blockTop = y;
                    if (componentsByOwner.TryGetValue(gameObjects[i].Id, out var owned))
                    {
                        foreach (var c in owned)
                        {
                            positions[c.Id] = new Vector2(componentX, y);
                            y += NodeHeight + NodeYSpacing;
                        }
                        y -= NodeYSpacing;
                    }
                    float blockHeight = Mathf.Max(y - blockTop, NodeHeight);
                    positions[gameObjects[i].Id] = new Vector2(gameObjects[i].Depth * TreeIndent, blockTop + (blockHeight - NodeHeight) * 0.5f);
                    y = blockTop + blockHeight;
                }
            }

            // References column: near the components that reference them
            float referenceX = componentX + ColumnSpacing;
            StackByBarycenter(graph, references, _ => referenceX, positions, (e, n) => e.ToId == n.Id ? e.FromId : null);

            return positions;
        }

        /// <summary>
        /// Places nodes in one column near the mean Y of their already-placed neighbours, stacking downward without overlap.
        /// Nodes are sorted by that mean (to reduce edge crossings) unless keepOrder is set, in which case their given order is kept.
        /// </summary>
        /// <param name="xOf">X position of each node.</param>
        /// <param name="neighbourOf">Returns the neighbour id of node n through edge e, or null if e does not connect to n.</param>
        private static void StackByBarycenter(
            RefGraph graph,
            List<RefGraphNode> nodes,
            Func<RefGraphNode, float> xOf,
            Dictionary<string, Vector2> positions,
            Func<RefGraphEdge, RefGraphNode, string> neighbourOf,
            bool keepOrder = false)
        {
            var barycenters = new Dictionary<string, float>();
            foreach (var n in nodes)
            {
                float sum = 0f;
                int count = 0;
                foreach (var e in graph.Edges)
                {
                    var other = neighbourOf(e, n);
                    if (other == null || !positions.TryGetValue(other, out var p))
                        continue;
                    sum += p.y;
                    count++;
                }
                barycenters[n.Id] = count > 0 ? sum / count : 0f;
            }
            var sorted = new List<RefGraphNode>(nodes);
            if (!keepOrder)
            {
                sorted.Sort((a, b) =>
                {
                    int cmp = barycenters[a.Id].CompareTo(barycenters[b.Id]);
                    return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
                });
            }
            float nextFreeY = float.MinValue;
            foreach (var n in sorted)
            {
                float y = Mathf.Max(barycenters[n.Id], nextFreeY);
                positions[n.Id] = new Vector2(xOf(n), y);
                nextFreeY = y + NodeHeight + NodeYSpacing;
            }
        }
    }
}
