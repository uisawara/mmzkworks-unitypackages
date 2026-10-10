using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Writes a validation result JSON file. The Validation window and
    /// <see cref="ValidationCommandLine"/> both use it, so the <c>issues</c> array matches.
    /// </summary>
    internal static class ValidationReport
    {
        public static void Write(string path, JsonObject root)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is required.", nameof(path));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, root.ToString(), new UTF8Encoding(false));
        }

        public static JsonArray Issues(IEnumerable<ValidationIssue> issues)
        {
            var rows = new JsonArray();
            if (issues == null) return rows;

            foreach (var issue in issues.OrderByDescending(item => item.Severity).ThenBy(item => item.Location ?? "", StringComparer.Ordinal))
            {
                var row = new JsonObject();
                row.Set("severity", issue.Severity.ToString());
                row.Set("location", issue.Location);
                row.Set("message", issue.Message);
                row.Set("assetPath", string.IsNullOrEmpty(issue.AssetPath) ? null : issue.AssetPath);
                row.Set("scenePath", string.IsNullOrEmpty(issue.ScenePath) ? null : issue.ScenePath);
                rows.Add(row);
            }

            return rows;
        }
    }
}
