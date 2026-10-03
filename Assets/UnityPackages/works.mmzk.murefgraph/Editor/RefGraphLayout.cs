using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muRefgraph
{
    /// <summary>Lays out the graph in three columns: Root | Components | References.</summary>
    public static class RefGraphLayout
    {
        public const float NodeWidth = 220f;
        public const float NodeHeight = 44f;
        public const float ColumnSpacing = 300f;
        public const float NodeYSpacing = 10f;
        public const float GroupHeaderHeight = 20f;
        public const float GroupPadding = 8f;
        public const float GroupSpacing = 18f;

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

            var components = new List<RefGraphNode>();
            var references = new List<RefGraphNode>();
            foreach (var n in graph.Nodes)
            {
                if (n.Kind == RefGraphNodeKind.Component || n.Kind == RefGraphNodeKind.MissingScript)
                    components.Add(n);
                else if (n.Kind == RefGraphNodeKind.Reference)
                    references.Add(n);
            }
            components.Sort((a, b) => a.Order.CompareTo(b.Order));
            references.Sort((a, b) => a.Order.CompareTo(b.Order));

            // Components column
            float componentX = ColumnSpacing;
            float y = 0f;
            if (mode == RefGraphLayoutMode.ByAssembly)
            {
                var byAssembly = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var c in components)
                {
                    var key = c.AssemblyName ?? string.Empty;
                    if (!byAssembly.TryGetValue(key, out var list))
                        byAssembly[key] = list = new List<string>();
                    list.Add(c.Id);
                }
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
            }
            else
            {
                foreach (var c in components)
                {
                    positions[c.Id] = new Vector2(componentX, y);
                    y += NodeHeight + NodeYSpacing;
                }
                if (components.Count > 0)
                    y -= NodeYSpacing;
            }
            float componentColumnHeight = Mathf.Max(y, NodeHeight);

            // Root: vertically centered against the components column
            if (graph.Root != null)
                positions[RefGraph.RootId] = new Vector2(0f, (componentColumnHeight - NodeHeight) * 0.5f);

            // References column: sort by the mean Y of their sources to reduce edge crossings,
            // then stack downward without overlap, each as close to its barycenter as possible.
            var barycenters = new Dictionary<string, float>();
            foreach (var r in references)
            {
                float sum = 0f;
                int count = 0;
                foreach (var e in graph.Edges)
                {
                    if (e.ToId != r.Id || !positions.TryGetValue(e.FromId, out var p))
                        continue;
                    sum += p.y;
                    count++;
                }
                barycenters[r.Id] = count > 0 ? sum / count : 0f;
            }
            var sortedRefs = new List<RefGraphNode>(references);
            sortedRefs.Sort((a, b) =>
            {
                int cmp = barycenters[a.Id].CompareTo(barycenters[b.Id]);
                return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
            });
            float referenceX = ColumnSpacing * 2f;
            float nextFreeY = float.MinValue;
            foreach (var r in sortedRefs)
            {
                float ry = Mathf.Max(barycenters[r.Id], nextFreeY);
                positions[r.Id] = new Vector2(referenceX, ry);
                nextFreeY = ry + NodeHeight + NodeYSpacing;
            }

            return positions;
        }
    }
}
