using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    public class FileStorage : IStorage
    {
        private readonly string _baseDirectory;

        public FileStorage(string baseDirectory = "Storage")
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));

            if (!Directory.Exists(_baseDirectory)) Directory.CreateDirectory(_baseDirectory);
        }

        public UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = Directory.GetFiles(_baseDirectory, "*", SearchOption.AllDirectories);
            var keys = files.Select(file => GetKeyFromPath(file)).ToArray();

            return UniTask.FromResult(keys);
        }

        public async UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            var filePath = GetPathFromKey(key);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {key}");

            return await File.ReadAllBytesAsync(filePath, cancellationToken);
        }

        public async UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            var filePath = GetPathFromKey(key);
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            if (data == null || data.Length == 0)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            else
            {
                await File.WriteAllBytesAsync(filePath, data, cancellationToken);
            }
        }

        public UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            var filePath = GetPathFromKey(key);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {key}");

            var stream = File.OpenRead(filePath);

            return UniTask.FromResult<Stream>(stream);
        }

        public async UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            cancellationToken.ThrowIfCancellationRequested();

            var filePath = GetPathFromKey(key);
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        private string GetPathFromKey(string key)
        {
            var safeKey = key.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_baseDirectory, safeKey);
        }

        private string GetKeyFromPath(string filePath)
        {
            var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}