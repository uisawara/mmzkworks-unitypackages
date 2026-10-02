using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Combines FileNameRules and validation attribute results per asset, and counts errors and warnings under each folder.
    /// Folder counts come from a scan of the whole Assets folder, spread over several editor frames
    /// so a large project does not freeze the Editor.
    /// </summary>
    [InitializeOnLoad]
    public static class ValidationStatus
    {
        private const string MenuPath = "Tools/muValidation/Show Errors On Folders";
        private const string ShowOnFoldersKey = "muValidation.ShowErrorsOnFolders";
        private const double FrameBudgetSeconds = 0.008;

        public readonly struct FolderCounts
        {
            public readonly int Errors;
            public readonly int Warnings;

            public FolderCounts(int errors, int warnings)
            {
                Errors = errors;
                Warnings = warnings;
            }
        }

        // Result of the last completed scan: path -> severity (only invalid paths),
        // and folder path -> error / warning counts below it.
        private static Dictionary<string, ValidationSeverity> _severities;
        private static readonly Dictionary<string, int> FolderErrorCounts = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> FolderWarningCounts = new Dictionary<string, int>();

        // In-progress scan
        private static Queue<string> _pendingGuids;
        private static Dictionary<string, ValidationSeverity> _scanSeverities;
        private static bool _rescanRequested;

        static ValidationStatus()
        {
            EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, ShowOnFolders);
        }

        public static bool ShowOnFolders
        {
            get => EditorPrefs.GetBool(ShowOnFoldersKey, true);
            set
            {
                EditorPrefs.SetBool(ShowOnFoldersKey, value);
                Menu.SetChecked(MenuPath, value);
                if (!value) StopScan();
                EditorApplication.RepaintProjectWindow();
            }
        }

        [MenuItem(MenuPath)]
        private static void ToggleShowOnFolders()
        {
            ShowOnFolders = !ShowOnFolders;
        }

        /// <summary>
        /// Returns the validation result for the asset itself.
        /// </summary>
        public static ValidationSummary GetSummary(string guid)
        {
            var fileName = FileNameRulesRegistry.GetError(guid);
            var fileNameSummary = fileName == null ? ValidationSummary.Valid : ValidationSummary.Error(fileName);
            return ValidationSummary.Combine(fileNameSummary, AssetValidation.GetSummary(guid));
        }

        /// <summary>
        /// Returns how many assets under the folder (any depth) have errors / only warnings.
        /// Zero for non-folders, or while the first scan is still running.
        /// </summary>
        public static FolderCounts GetFolderCounts(string folderPath)
        {
            if (!ShowOnFolders || string.IsNullOrEmpty(folderPath)) return default;
            if (_severities == null || _rescanRequested) StartScan();

            FolderErrorCounts.TryGetValue(folderPath, out var errors);
            FolderWarningCounts.TryGetValue(folderPath, out var warnings);
            return new FolderCounts(errors, warnings);
        }

        /// <summary>
        /// Called when results may have changed anywhere. Folder counts are rescanned on next use;
        /// the previous counts stay visible until the scan finishes.
        /// </summary>
        public static void RequestRescan()
        {
            _rescanRequested = true;
        }

        /// <summary>
        /// Called when a single asset's result may have changed. Updates folder counts right away.
        /// </summary>
        public static void Refresh(string guid)
        {
            if (_severities == null) return;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            var severity = GetSummary(guid).Severity;
            if (_scanSeverities != null) SetSeverity(_scanSeverities, path, severity);

            _severities.TryGetValue(path, out var previous);
            if (previous == severity) return;

            SetSeverity(_severities, path, severity);
            AddToAncestors(path, previous, -1);
            AddToAncestors(path, severity, 1);
        }

        private static void StartScan()
        {
            _rescanRequested = false;
            _pendingGuids = new Queue<string>(AssetDatabase.FindAssets("", new[] { "Assets" }));
            _scanSeverities = new Dictionary<string, ValidationSeverity>();
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        private static void StopScan()
        {
            EditorApplication.update -= Step;
            _pendingGuids = null;
            _scanSeverities = null;
        }

        private static void Step()
        {
            if (_pendingGuids == null)
            {
                EditorApplication.update -= Step;
                return;
            }

            var deadline = EditorApplication.timeSinceStartup + FrameBudgetSeconds;
            while (_pendingGuids.Count > 0 && EditorApplication.timeSinceStartup < deadline)
            {
                var guid = _pendingGuids.Dequeue();
                var severity = GetSummary(guid).Severity;
                if (severity != ValidationSeverity.None) _scanSeverities[AssetDatabase.GUIDToAssetPath(guid)] = severity;
            }

            if (_pendingGuids.Count > 0) return;

            _severities = _scanSeverities;
            StopScan();

            FolderErrorCounts.Clear();
            FolderWarningCounts.Clear();
            foreach (var pair in _severities) AddToAncestors(pair.Key, pair.Value, 1);
            EditorApplication.RepaintProjectWindow();
        }

        private static void SetSeverity(Dictionary<string, ValidationSeverity> map, string path, ValidationSeverity severity)
        {
            if (severity == ValidationSeverity.None) map.Remove(path);
            else map[path] = severity;
        }

        private static void AddToAncestors(string path, ValidationSeverity severity, int delta)
        {
            var counts = severity == ValidationSeverity.Error ? FolderErrorCounts
                : severity == ValidationSeverity.Warning ? FolderWarningCounts
                : null;
            if (counts == null) return;

            for (var folder = FileNameRules.GetParentFolder(path); !string.IsNullOrEmpty(folder); folder = FileNameRules.GetParentFolder(folder))
            {
                counts.TryGetValue(folder, out var count);
                count += delta;
                if (count > 0) counts[folder] = count;
                else counts.Remove(folder);
            }
        }

        /// <summary>
        /// Single entry point for asset changes, so the caches are updated in a fixed order:
        /// - Moves / deletes change paths and folder contents: clear everything and rescan folders.
        /// - A changed FileNameRules affects many files: clear file name results and rescan folders.
        /// - A changed SceneRules / SceneRulesAssignments affects scene objects: rebuild the SceneRules list and scene results.
        /// - Otherwise only the imported assets are re-checked. Cached results of other assets are kept
        ///   and recheck their dependency hash on next use.
        /// </summary>
        private class Postprocessor : AssetPostprocessor
        {
            // Above this, refreshing one by one would block the Editor; rescan over several frames instead.
            private const int MaxIncrementalRefresh = 200;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                var structural = deleted.Length > 0 || moved.Length > 0;
                var ruleChanged = structural || imported.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(FileNameRules));

                if (ruleChanged) FileNameRulesRegistry.Invalidate();
                if (structural || imported.Any(path =>
                {
                    var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    return type == typeof(SceneRules) || type == typeof(SceneRulesAssignments);
                }))
                {
                    SceneRulesRegistry.Invalidate();
                }

                if (structural)
                {
                    AssetValidation.Invalidate();
                    return;
                }

                AssetValidation.MarkStale(imported.Select(AssetDatabase.AssetPathToGUID));

                if (ruleChanged || imported.Length > MaxIncrementalRefresh)
                {
                    RequestRescan();
                }
                else
                {
                    foreach (var path in imported) Refresh(AssetDatabase.AssetPathToGUID(path));
                }

                EditorApplication.RepaintProjectWindow();
            }
        }
    }
}
