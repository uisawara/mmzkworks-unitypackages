using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    public class CachedStorage : IStorage
    {
        private readonly IStorage _cache;
        private readonly ConcurrentDictionary<string, bool> _cacheHits = new ConcurrentDictionary<string, bool>();
        private readonly IStorage _origin;

        public CachedStorage(IStorage cache, IStorage origin)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        public int CacheHitCount => _cacheHits.Count;

        public async UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _origin.ListAll(cancellationToken);
        }

        public async UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var data = await _cache.ReadAsync(key, cancellationToken);
                _cacheHits.AddOrUpdate(key, true, (k, v) => true);
                return data;
            }
            catch (Exception)
            {
                var data = await _origin.ReadAsync(key, cancellationToken);

                try
                {
                    await _cache.WriteAsync(key, data, cancellationToken);
                }
                catch (Exception)
                {
                }

                return data;
            }
        }

        public async UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            await _origin.WriteAsync(key, data, cancellationToken);

            try
            {
                await _cache.WriteAsync(key, data, cancellationToken);
                _cacheHits.AddOrUpdate(key, true, (k, v) => true);
            }
            catch (Exception)
            {
            }
        }

        public async UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var reader = await _cache.ReadToStreamAsync(key, cancellationToken);
                _cacheHits.AddOrUpdate(key, true, (k, v) => true);
                return reader;
            }
            catch (Exception)
            {
                var reader = await _origin.ReadToStreamAsync(key, cancellationToken);

                try
                {
                    await _cache.WriteAsync(key, reader, cancellationToken);
                }
                catch (Exception)
                {
                }

                return reader;
            }
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
            memoryStream.Position = 0;

            var data = memoryStream.ToArray();

            await _origin.WriteAsync(key, data, cancellationToken);

            try
            {
                await _cache.WriteAsync(key, data, cancellationToken);
                _cacheHits.AddOrUpdate(key, true, (k, v) => true);
            }
            catch (Exception)
            {
            }
        }

        public async UniTask ClearCache()
        {
            try
            {
                if (_cache is MemoryStorage memoryStorage)
                {
                    memoryStorage.Clear();
                }
                else
                {
                    var keys = await _cache.ListAll();
                    foreach (var key in keys) await _cache.WriteAsync(key, new byte[0]);
                }
            }
            catch (Exception)
            {
            }

            _cacheHits.Clear();
        }
    }
}