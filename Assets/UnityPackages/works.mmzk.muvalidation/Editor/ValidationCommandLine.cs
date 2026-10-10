using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Batch-mode entry points. Both call the same code as the editor: export uses <see cref="RulesCatalog"/>,
    /// and the run uses <see cref="ValidationBuildCheck"/> with Project Settings &gt; muValidation.
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod Mmzkworks.muValidation.Editor.ValidationCommandLine.ExportRules -muValidationExport rules.json
    /// Unity -batchmode -quit -projectPath . -executeMethod Mmzkworks.muValidation.Editor.ValidationCommandLine.Run -muValidationReport report.json
    /// </code>
    /// A relative path is resolved from the project folder. Run exits with code 1 when the check fails.
    /// </summary>
    public static class ValidationCommandLine
    {
        public const string ExportArgument = "-muValidationExport";
        public const string ReportArgument = "-muValidationReport";

        /// <summary>
        /// Writes every configured rule as JSON. Without <see cref="ExportArgument"/>, writes
        /// muvalidation-rules.json in the project folder.
        /// </summary>
        public static void ExportRules()
        {
            try
            {
                var path = ProjectFile(GetOption(ExportArgument) ?? "muvalidation-rules.json");
                var catalog = RulesCatalog.Collect();
                catalog.Write(path);
                Debug.Log($"[muValidation] Exported rules to {path} " +
                          $"({catalog.FileNames.Length} file name, {catalog.Scenes.Length} scene, " +
                          $"{catalog.Assignments.Length} assignment, {catalog.Attributes.Length} attribute type(s)).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[muValidation] Failed to export rules: {e}");
                Exit(1);
            }
        }

        /// <summary>
        /// Runs the build check (assets and enabled build scenes, per Project Settings &gt; muValidation).
        /// Writes a JSON report when <see cref="ReportArgument"/> is set. Exits with code 1 on failure.
        /// </summary>
        public static void Run()
        {
            try
            {
                var settings = ValidationSettings.instance;
                if (!ValidationBuildCheck.Run(settings, Application.isBatchMode, out var issues))
                {
                    Debug.LogError("[muValidation] Validation was canceled.");
                    Exit(2);
                    return;
                }

                var errors = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
                var warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
                var failed = (settings.failOnErrors && errors > 0) || (settings.failOnWarnings && warnings > 0);

                var report = GetOption(ReportArgument);
                if (!string.IsNullOrEmpty(report))
                {
                    var reportPath = ProjectFile(report);
                    WriteReport(reportPath, settings, issues, errors, warnings, failed);
                    Debug.Log($"[muValidation] Wrote validation report to {reportPath}");
                }

                if (failed)
                {
                    Debug.LogError($"[muValidation] Validation failed: {errors} error(s), {warnings} warning(s).");
                    Exit(1);
                    return;
                }

                Debug.Log($"[muValidation] Validation passed: {errors} error(s), {warnings} warning(s).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[muValidation] Validation failed: {e}");
                Exit(1);
            }
        }

        private static void WriteReport(string path, ValidationSettings settings, List<ValidationIssue> issues,
            int errors, int warnings, bool failed)
        {
            var root = new JsonObject();
            root.Set("errors", errors);
            root.Set("warnings", warnings);
            root.Set("failed", failed);
            root.Set("includeAssets", settings.includeAssets);
            root.Set("includeBuildScenes", settings.includeBuildScenes);
            root.Set("failOnErrors", settings.failOnErrors);
            root.Set("failOnWarnings", settings.failOnWarnings);
            root.Set("issues", ValidationReport.Issues(issues));
            ValidationReport.Write(path, root);
        }

        private static string GetOption(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return null;
        }

        private static string ProjectFile(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(root, path));
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
