using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    public class MemoryStorage : IStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _storage = new ConcurrentDictionary<string, byte[]>();

        public int Count => _storage.Count;

        public UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(_storage.Keys.ToArray());
        }

        public UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            if (!_storage.TryGetValue(key, out var data))
                throw new KeyNotFoundException($"Key not found: {key}");

            return UniTask.FromResult(data);
        }

        public UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            if (data == null || data.Length == 0)
                _storage.TryRemove(key, out _);
            else
                _storage.AddOrUpdate(key, data, (k, v) => data);

            return UniTask.CompletedTask;
        }

        public UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            if (!_storage.TryGetValue(key, out var data))
                throw new KeyNotFoundException($"Key not found: {key}");

            var stream = new MemoryStream(data);

            return UniTask.FromResult<Stream>(stream);
        }

        public async UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            cancellationToken.ThrowIfCancellationRequested();

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            var data = memoryStream.ToArray();
            _storage.AddOrUpdate(key, data, (k, v) => data);
        }

        public void Clear()
        {
            _storage.Clear();
        }
    }
}