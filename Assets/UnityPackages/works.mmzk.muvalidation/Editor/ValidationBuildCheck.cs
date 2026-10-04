using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Runs validation before a player build (see ValidationSettings) and stops the build on failure.
    /// Issues are logged to the Console (click to ping) and, outside batch mode, shown in the Validation window.
    /// </summary>
    public class ValidationBuildCheck : IPreprocessBuildWithReport
    {
        private const int MaxLoggedIssues = 100;

        // Early, so a failing check stops the build before other preprocessors do work.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = ValidationSettings.instance;
            if (!settings.validateBeforeBuild) return;

            if (!Run(settings, Application.isBatchMode, out var issues))
            {
                throw new BuildFailedException("[muValidation] Validation was canceled.");
            }

            var errors = issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warnings = issues.Count(i => i.Severity == ValidationSeverity.Warning);
            var fail = (settings.failOnErrors && errors > 0) || (settings.failOnWarnings && warnings > 0);
            if (!fail) return;

            if (!Application.isBatchMode) ValidationWindow.ShowIssues(issues, "Build check");
            throw new BuildFailedException(
                $"[muValidation] Build stopped: {errors} error(s), {warnings} warning(s). See the Console or Tools > muValidation > Validation. " +
                "Change this in Project Settings > muValidation.");
        }

        /// <summary>
        /// Runs the same check as before a build, without building, and shows the results.
        /// </summary>
        [MenuItem("Tools/muValidation/Run Build Check", false, 2004)]
        public static void RunFromMenu()
        {
            if (!Run(ValidationSettings.instance, false, out var issues)) return;
            ValidationWindow.ShowIssues(issues, "Build check");
        }

        /// <summary>
        /// Collects and logs issues using the settings. Returns false if canceled.
        /// </summary>
        public static bool Run(ValidationSettings settings, bool batchMode, out List<ValidationIssue> issues)
        {
            issues = new List<ValidationIssue>();
            if (settings.includeAssets && !ValidationRunner.CollectAssets(issues, !batchMode)) return false;
            if (settings.includeBuildScenes) ValidationRunner.CollectBuildScenes(issues, !batchMode);

            Log(issues);
            return true;
        }

        private static void Log(List<ValidationIssue> issues)
        {
            var errors = issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warnings = issues.Count(i => i.Severity == ValidationSeverity.Warning);
            if (issues.Count == 0)
            {
                Debug.Log("[muValidation] Build check passed.");
                return;
            }

            var ordered = issues.OrderByDescending(i => i.Severity).ThenBy(i => i.Location, System.StringComparer.Ordinal).ToList();
            foreach (var issue in ordered.Take(MaxLoggedIssues))
            {
                // Scene objects of scenes opened only for the check are gone by now; Resolve falls back to the scene asset.
                var context = issue.Resolve();
                var text = $"[muValidation] {issue.Location}\n{issue.Message}";
                if (issue.Severity == ValidationSeverity.Error) Debug.LogError(text, context);
                else Debug.LogWarning(text, context);
            }

            var summary = new StringBuilder($"[muValidation] Build check: {errors} error(s), {warnings} warning(s).");
            if (ordered.Count > MaxLoggedIssues) summary.Append($" Only the first {MaxLoggedIssues} are logged.");
            Debug.Log(summary.ToString());
        }
    }
}
