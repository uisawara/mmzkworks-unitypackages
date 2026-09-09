using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Computes node rects from levels, offsets, pan and zoom.</summary>
    public static class AsmdefGraphLayout
    {
        private const string DllPrefix = "DLL::";

        public const float NodeWidth = 220f;
        public const float NodeHeight = 54f;
        public const float LevelXSpacing = 260f;
        public const float LevelYSpacing = 14f;
        public const float NodeYSpacing = 10f;

        /// <summary>Compute node name -> Rect. startX/startY are the top-left of the graph area (e.g. 10, 40).</summary>
        /// <remarks>Within each level, asmdef nodes are placed first, then DLL nodes, so toggling "Show DLLs" does not shift asmdef positions.</remarks>
        public static Dictionary<string, Rect> ComputeRects(
            List<List<string>> levels,
            Dictionary<string, Vector2> nodeOffsets,
            Vector2 panOffset,
            float zoomLevel,
            float startX = 10f,
            float startY = 40f)
        {
            var rects = new Dictionary<string, Rect>(256);
            float xBase = startX + panOffset.x;
            float yBase = startY + panOffset.y;

            for (int level = 0; level < levels.Count; level++)
            {
                float x = xBase + level * LevelXSpacing * zoomLevel;
                float y = yBase + level * LevelYSpacing * zoomLevel;

                var levelNodes = levels[level];
                var ordered = new List<string>(levelNodes);
                ordered.Sort((a, b) =>
                {
                    var aDll = a.StartsWith(DllPrefix, StringComparison.Ordinal);
                    var bDll = b.StartsWith(DllPrefix, StringComparison.Ordinal);
                    if (aDll != bDll)
                        return aDll.CompareTo(bDll);
                    return string.Compare(a, b, StringComparison.Ordinal);
                });

                foreach (var name in ordered)
                {
                    var baseRect = new Rect(x, y, NodeWidth * zoomLevel, NodeHeight * zoomLevel);
                    if (nodeOffsets != null && nodeOffsets.TryGetValue(name, out var offset))
                    {
                        baseRect.x += offset.x * zoomLevel;
                        baseRect.y += offset.y * zoomLevel;
                    }
                    rects[name] = baseRect;
                    y += (NodeHeight + NodeYSpacing) * zoomLevel;
                }
            }

            return rects;
        }
    }
}
