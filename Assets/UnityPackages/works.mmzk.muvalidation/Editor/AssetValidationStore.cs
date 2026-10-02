using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Saves asset validation results to Library/ so they survive domain reloads and Editor restarts.
    ///
    /// Each result is stored with the asset's dependency hash and is rechecked against it on use,
    /// so changed assets are re-validated. The file also records a code version (build IDs of
    /// muValidation and every assembly referencing it); if validation code was recompiled with changes,
    /// the file is ignored.
    /// </summary>
    internal static class AssetValidationStore
    {
        private const string FilePath = "Library/muValidation/AssetValidationCache.json";

        [Serializable]
        private class FileData
        {
            public string codeVersion;
            public List<Item> items = new List<Item>();
        }

        [Serializable]
        private class Item
        {
            public string guid;
            public string hash;
            public int severity;
            public string message;
        }

        public readonly struct Entry
        {
            public readonly string Guid;
            public readonly Hash128 Hash;
            public readonly ValidationSummary Summary;

            public Entry(string guid, Hash128 hash, ValidationSummary summary)
            {
                Guid = guid;
                Hash = hash;
                Summary = summary;
            }
        }

        private static string _codeVersion;

        /// <summary>
        /// Returns the saved entries, or none if there is no file or the code version differs.
        /// </summary>
        public static IEnumerable<Entry> Load()
        {
            FileData data;
            try
            {
                if (!File.Exists(FilePath)) return Enumerable.Empty<Entry>();
                data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[muValidation] Ignoring unreadable cache {FilePath}: {e.Message}");
                return Enumerable.Empty<Entry>();
            }

            if (data?.items == null || data.codeVersion != CodeVersion) return Enumerable.Empty<Entry>();

            return data.items
                .Where(item => !string.IsNullOrEmpty(item.guid))
                .Select(item => new Entry(item.guid, Hash128.Parse(item.hash),
                    new ValidationSummary((ValidationSeverity)item.severity, item.message)))
                .ToList();
        }

        public static void Save(IEnumerable<Entry> entries)
        {
            var data = new FileData { codeVersion = CodeVersion };
            foreach (var entry in entries)
            {
                data.items.Add(new Item
                {
                    guid = entry.Guid,
                    hash = entry.Hash.ToString(),
                    severity = (int)entry.Summary.Severity,
                    message = entry.Summary.Message,
                });
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[muValidation] Could not save cache {FilePath}: {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[muValidation] Could not delete cache {FilePath}: {e.Message}");
            }
        }

        // Build IDs (MVIDs) of the assemblies that can affect validation results. Unity compiles
        // deterministically, so an ID changes only when the assembly's code changes.
        private static string CodeVersion
        {
            get
            {
                if (_codeVersion != null) return _codeVersion;

                var runtime = typeof(ValidationAttribute).Assembly;
                var runtimeName = runtime.GetName().Name;
                var editor = typeof(AssetValidationStore).Assembly;

                var ids = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .Where(a => a == runtime || a == editor || a.GetReferencedAssemblies().Any(r => r.Name == runtimeName))
                    .Select(a => a.GetName().Name + ":" + a.ManifestModule.ModuleVersionId)
                    .OrderBy(id => id, StringComparer.Ordinal);

                _codeVersion = string.Join(";", ids);
                return _codeVersion;
            }
        }
    }
}
