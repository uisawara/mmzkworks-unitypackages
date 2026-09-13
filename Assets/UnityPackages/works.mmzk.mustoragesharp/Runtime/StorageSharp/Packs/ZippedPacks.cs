/*using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using StorageSharp.Storages;

namespace StorageSharp.Packs
{
    public sealed class ZippedPacks : IPacks
    {
        private readonly Dictionary<string, string> _loadedPackages = new Dictionary<string, string>();
        private readonly string _tempDirectory;
        private readonly IStorage _zippedStorage;

        public ZippedPacks(Settings settings, IStorage zippedStorage)
        {
            _tempDirectory = settings.TempDirectory ?? throw new ArgumentNullException(nameof(settings));
            _zippedStorage = zippedStorage ?? throw new ArgumentNullException(nameof(zippedStorage));
            if (!Directory.Exists(_tempDirectory)) Directory.CreateDirectory(_tempDirectory);
        }

        public async UniTask Clear()
        {
            var keys = await _zippedStorage.ListAll();
            foreach (var key in keys) await _zippedStorage.WriteAsync(key, new byte[0]);
            _loadedPackages.Clear();
        }

        public async UniTask<IPacks.ArchiveScheme[]> ListAll()
        {
            var keys = await _zippedStorage.ListAll();
            var schemes = new List<IPacks.ArchiveScheme>();
            foreach (var key in keys)
                if (key.EndsWith(".json"))
                    try
                    {
                        var jsonData = await _zippedStorage.ReadAsync(key);
                        var jsonString = Encoding.UTF8.GetString(jsonData);
                        var schemeData = JsonConvert.DeserializeObject<SchemeData>(jsonString);
                        schemes.Add(new IPacks.ArchiveScheme(schemeData.DirectoryPath));
                    }
                    catch (Exception)
                    {
                    }

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
                using (var zipStream = new ZipOutputStream(memoryStream))
                {
                    zipStream.SetLevel(9);
                    AddDirectoryToZip(zipStream, directoryPath, "");
                    zipStream.IsStreamOwner = false;
                    zipStream.Close();
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
            var jsonData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(schemeData));
            await _zippedStorage.WriteAsync(jsonKey, jsonData);
            return scheme;
        }

        public async UniTask Delete(IPacks.ArchiveScheme scheme)
        {
            if (scheme == null)
                throw new ArgumentNullException(nameof(scheme));
            if (_loadedPackages.ContainsKey(scheme.DirectoryPath)) await Unload(scheme);
            var keys = await _zippedStorage.ListAll();
            foreach (var key in keys)
                if (key.EndsWith(".json"))
                    try
                    {
                        var jsonData = await _zippedStorage.ReadAsync(key);
                        var jsonString = Encoding.UTF8.GetString(jsonData);
                        var schemeData = JsonConvert.DeserializeObject<SchemeData>(jsonString);
                        if (schemeData.DirectoryPath == scheme.DirectoryPath)
                        {
                            await _zippedStorage.WriteAsync(key, new byte[0]);
                            await _zippedStorage.WriteAsync(schemeData.ZipKey, new byte[0]);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }
        }

        public async UniTask<string> Load(IPacks.ArchiveScheme archiveScheme)
        {
            if (archiveScheme == null)
                throw new ArgumentNullException(nameof(archiveScheme));
            if (_loadedPackages.TryGetValue(archiveScheme.DirectoryPath, out var existingPath)) return existingPath;
            var keys = await _zippedStorage.ListAll();
            SchemeData schemeData = null;
            string jsonKey = null;
            foreach (var key in keys)
                if (key.EndsWith(".json"))
                    try
                    {
                        var jsonBytes = await _zippedStorage.ReadAsync(key);
                        var jsonString = Encoding.UTF8.GetString(jsonBytes);
                        var data = JsonConvert.DeserializeObject<SchemeData>(jsonString);
                        if (data.DirectoryPath == archiveScheme.DirectoryPath)
                        {
                            schemeData = data;
                            jsonKey = key;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }

            if (schemeData == null)
                throw new InvalidOperationException($"Archive scheme not found: {archiveScheme.DirectoryPath}");
            var extractPath = Path.Combine(_tempDirectory, schemeData.PackageId);
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            var zipData = await _zippedStorage.ReadAsync(schemeData.ZipKey);
            using (var memoryStream = new MemoryStream(zipData))
            using (var zipInput = new ZipInputStream(memoryStream))
            {
                ZipEntry entry;
                while ((entry = zipInput.GetNextEntry()) != null)
                {
                    var outPath = Path.Combine(extractPath, entry.Name);
                    var outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);
                    if (!entry.IsDirectory)
                        using (var outFile = File.Create(outPath))
                        {
                            zipInput.CopyTo(outFile);
                        }
                }
            }

            _loadedPackages[archiveScheme.DirectoryPath] = extractPath;
            return extractPath;
        }

        public async UniTask Unload(IPacks.ArchiveScheme archiveScheme)
        {
            if (archiveScheme == null)
                throw new ArgumentNullException(nameof(archiveScheme));
            if (_loadedPackages.TryGetValue(archiveScheme.DirectoryPath, out var path))
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                _loadedPackages.Remove(archiveScheme.DirectoryPath);
            }
        }

        private void AddDirectoryToZip(ZipOutputStream zipStream, string sourceDir, string relativePath)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var entryName = Path.Combine(relativePath, Path.GetFileName(filePath));
                var entry = new ZipEntry(entryName);
                zipStream.PutNextEntry(entry);
                using (var fileStream = File.OpenRead(filePath))
                {
                    fileStream.CopyTo(zipStream);
                }

                zipStream.CloseEntry();
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.Combine(relativePath, Path.GetFileName(dir));
                AddDirectoryToZip(zipStream, dir, dirName);
            }
        }

        public class Settings
        {
            public Settings(string tempDirectory)
            {
                TempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
            }

            public string TempDirectory { get; }
        }

        private class SchemeData
        {
            public string DirectoryPath { get; set; }
            public string PackageId { get; set; }
            public string ZipKey { get; set; }
        }
    }
}*/