using System.Collections.Generic;

namespace Mmzkworks.muAsmdefgraph
{
    /// <summary>Lightweight JSON parsing for asmdef and package.json.</summary>
    public static class AsmdefJsonHelper
    {
        public static string ExtractStringProperty(string json, string propName)
        {
            var key = "\"" + propName + "\"";
            int idx = json.IndexOf(key, System.StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            idx = json.IndexOf(':', idx);
            if (idx < 0) return string.Empty;
            idx = json.IndexOf('"', idx);
            if (idx < 0) return string.Empty;
            int start = idx + 1;
            int end = json.IndexOf('"', start);
            if (end < 0 || end <= start) return string.Empty;
            return json.Substring(start, end - start);
        }

        public static List<string> ExtractStringArrayProperty(string json, string propName)
        {
            var key = "\"" + propName + "\"";
            int idx = json.IndexOf(key, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf('[', idx);
            if (idx < 0) return null;
            int endArr = json.IndexOf(']', idx);
            if (endArr < 0) return null;
            var slice = json.Substring(idx + 1, endArr - (idx + 1));
            var results = new List<string>();

            int p = 0;
            while (p < slice.Length)
            {
                int q1 = slice.IndexOf('"', p);
                if (q1 < 0) break;
                int q2 = slice.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                var s = slice.Substring(q1 + 1, q2 - (q1 + 1));
                if (!string.IsNullOrEmpty(s))
                    results.Add(s);
                p = q2 + 1;
            }
            return results;
        }
    }
}
