using System.Collections.Generic;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Builds level structure and dependency stats from root asmdef names.</summary>
    public static class AsmdefGraphBuilder
    {
        public static List<List<string>> BuildLevels(HashSet<string> rootNames, int maxDepth, bool showDllNodes, bool showUnresolvedNodes)
        {
            var levels = new List<List<string>>();
            var visited = new HashSet<string>();
            var visitedDll = new HashSet<string>();
            var q = new Queue<(string name, int depth)>();

            foreach (var r in rootNames)
            {
                q.Enqueue((r, 0));
                visited.Add(r);
            }

            while (q.Count > 0)
            {
                var (name, depth) = q.Dequeue();
                if (depth > maxDepth) continue;

                while (levels.Count <= depth)
                    levels.Add(new List<string>());
                levels[depth].Add(name);

                if (!AsmdefDatabase.TryGetAsmdefInfoByName(name, out var info))
                    continue;

                foreach (var next in info.References)
                {
                    if (!showUnresolvedNodes && AsmdefDatabase.IsUnresolvedNode(next))
                        continue;
                    if (visited.Add(next))
                        q.Enqueue((next, depth + 1));
                }

                if (showDllNodes)
                {
                    var dllDepth = depth + 1;
                    if (dllDepth <= maxDepth && info.PrecompiledReferences != null && info.PrecompiledReferences.Count > 0)
                    {
                        while (levels.Count <= dllDepth)
                            levels.Add(new List<string>());

                        foreach (var dll in info.PrecompiledReferences)
                        {
                            if (string.IsNullOrEmpty(dll)) continue;
                            var dllKey = "DLL::" + dll;
                            if (visitedDll.Add(dllKey))
                                levels[dllDepth].Add(dllKey);
                        }
                    }
                }
            }

            return levels;
        }

        public static (int directCount, int totalCount) ComputeDependencyStats(HashSet<string> rootAsmdefNames)
        {
            if (rootAsmdefNames == null || rootAsmdefNames.Count == 0)
                return (0, 0);

            var direct = new HashSet<string>();
            foreach (var root in rootAsmdefNames)
            {
                if (!AsmdefDatabase.TryGetAsmdefInfoByName(root, out var info) || info == null)
                    continue;
                foreach (var r in info.References)
                {
                    if (!string.IsNullOrEmpty(r))
                        direct.Add(r);
                }
            }

            var visited = new HashSet<string>();
            var q = new Queue<string>();
            foreach (var d in direct)
            {
                if (visited.Add(d))
                    q.Enqueue(d);
            }

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (!AsmdefDatabase.TryGetAsmdefInfoByName(cur, out var info) || info == null)
                    continue;
                foreach (var next in info.References)
                {
                    if (string.IsNullOrEmpty(next)) continue;
                    if (rootAsmdefNames.Contains(next)) continue;
                    if (visited.Add(next))
                        q.Enqueue(next);
                }
            }

            foreach (var root in rootAsmdefNames)
                direct.Remove(root);

            return (direct.Count, visited.Count);
        }
    }
}
