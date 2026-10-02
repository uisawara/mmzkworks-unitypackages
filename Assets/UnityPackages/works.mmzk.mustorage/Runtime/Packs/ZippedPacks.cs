using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mmzkworks.muStorage
{
    public sealed class ZippedPacks : IPacks
    {
        private readonly Dictionary<string, string> _loadedPackages = new Dictionary<string, string>();
        private readonly string _tempDirectory;
        private readonly IStorage _zippedStorage;

        public ZippedPacks(Settings settings, IStorage zippedStorage)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _tempDirectory = settings.TempDirectory;
            _zippedStorage = zippedStorage ?? throw new ArgumentNullException(nameof(zippedStorage));
            if (!Directory.Exists(_tempDirectory)) Directory.CreateDirectory(_tempDirectory);
        }

        public async UniTask Clear()
        {
            foreach (var path in _loadedPackages.Values)
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            _loadedPackages.Clear();

            var keys = await _zippedStorage.ListAll();
            foreach (var key in keys) await _zippedStorage.WriteAsync(key, new byte[0]);
        }

        public async UniTask<IPacks.ArchiveScheme[]> ListAll()
        {
            var schemes = new List<IPacks.ArchiveScheme>();
            foreach (var (_, schemeData) in await ReadAllSchemeData())
                schemes.Add(new IPacks.ArchiveScheme(schemeData.DirectoryPath));

            return schemes.ToArray();
        }

        public async UniTask<IPacks.ArchiveScheme> Add(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            var scheme = new IPacks.ArchiveScheme(directoryPath);
            var packageId = Guid.NewGuid().ToString();
            var zipKey = $"{packageId}.zip";
            var jsonKey = $"{packageId}.json";

            byte[] zipData;
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    AddDirectoryToZip(archive, directoryPath, "");
                }

                zipData = memoryStream.ToArray();
            }

            await _zippedStorage.WriteAsync(zipKey, zipData);

            var schemeData = new SchemeData
            {
                DirectoryPath = directoryPath,
                PackageId = packageId,
                ZipKey = zipKey
            };
            var jsonData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(schemeData));
            await _zippedStorage.WriteAsync(jsonKey, jsonData);
            return scheme;
        }

        public async UniTask Delete(IPacks.ArchiveScheme scheme)
        {
            if (scheme == null)
                throw new ArgumentNullException(nameof(scheme));
            if (_loadedPackages.ContainsKey(scheme.DirectoryPath)) await Unload(scheme);

            var (jsonKey, schemeData) = await FindSchemeData(scheme.DirectoryPath);
            if (schemeData == null) return;

            await _zippedStorage.WriteAsync(jsonKey, new byte[0]);
            await _zippedStorage.WriteAsync(schemeData.ZipKey, new byte[0]);
        }

        public async UniTask<string> Load(IPacks.ArchiveScheme archiveScheme)
        {
            if (archiveScheme == null)
                throw new ArgumentNullException(nameof(archiveScheme));
            if (_loadedPackages.TryGetValue(archiveScheme.DirectoryPath, out var existingPath)) return existingPath;

            var (_, schemeData) = await FindSchemeData(archiveScheme.DirectoryPath);
            if (schemeData == null)
                throw new InvalidOperationException($"Archive scheme not found: {archiveScheme.DirectoryPath}");

            var extractPath = Path.Combine(_tempDirectory, schemeData.PackageId);
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            var extractRoot = Path.GetFullPath(extractPath) + Path.DirectorySeparatorChar;

            var zipData = await _zippedStorage.ReadAsync(schemeData.ZipKey);
            using (var memoryStream = new MemoryStream(zipData))
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    var outPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                    if (!outPath.StartsWith(extractRoot, StringComparison.Ordinal))
                        throw new IOException($"Invalid entry path in archive: {entry.FullName}");

                    // ディレクトリエントリは名前が '/' で終わる
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(outPath);
                        continue;
                    }

                    var outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);

                    using (var entryStream = entry.Open())
                    using (var outFile = File.Create(outPath))
                    {
                        entryStream.CopyTo(outFile);
                    }
                }
            }

            _loadedPackages[archiveScheme.DirectoryPath] = extractPath;
            return extractPath;
        }

        public UniTask Unload(IPacks.ArchiveScheme archiveScheme)
        {
            if (archiveScheme == null)
                throw new ArgumentNullException(nameof(archiveScheme));
            if (_loadedPackages.TryGetValue(archiveScheme.DirectoryPath, out var path))
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                _loadedPackages.Remove(archiveScheme.DirectoryPath);
            }

            return UniTask.CompletedTask;
        }

        private async UniTask<List<(string jsonKey, SchemeData schemeData)>> ReadAllSchemeData()
        {
            var result = new List<(string, SchemeData)>();
            var keys = await _zippedStorage.ListAll();
            foreach (var key in keys)
            {
                if (!key.EndsWith(".json")) continue;
                try
                {
                    var jsonData = await _zippedStorage.ReadAsync(key);
                    var schemeData = JsonUtility.FromJson<SchemeData>(Encoding.UTF8.GetString(jsonData));
                    if (schemeData != null && !string.IsNullOrEmpty(schemeData.DirectoryPath))
                        result.Add((key, schemeData));
                }
                catch (Exception)
                {
                }
            }

            return result;
        }

        private async UniTask<(string jsonKey, SchemeData schemeData)> FindSchemeData(string directoryPath)
        {
            foreach (var item in await ReadAllSchemeData())
                if (item.schemeData.DirectoryPath == directoryPath)
                    return item;

            return (null, null);
        }

        private static void AddDirectoryToZip(ZipArchive archive, string sourceDir, string relativePath)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var entryName = CombineEntryName(relativePath, Path.GetFileName(filePath));
                var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                using (var entryStream = entry.Open())
                using (var fileStream = File.OpenRead(filePath))
                {
                    fileStream.CopyTo(entryStream);
                }
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = CombineEntryName(relativePath, Path.GetFileName(dir));
                AddDirectoryToZip(archive, dir, dirName);
            }
        }

        // ZIP のエントリ名は OS によらず '/' 区切りにする
        private static string CombineEntryName(string relativePath, string name)
        {
            return string.IsNullOrEmpty(relativePath) ? name : relativePath + "/" + name;
        }

        public class Settings
        {
            public Settings(string tempDirectory)
            {
                TempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
            }

            public string TempDirectory { get; }
        }

        [Serializable]
        private class SchemeData
        {
            public string DirectoryPath;
            public string PackageId;
            public string ZipKey;
        }
    }
}
