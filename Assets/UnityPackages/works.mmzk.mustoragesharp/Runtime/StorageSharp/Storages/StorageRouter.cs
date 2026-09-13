using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StorageSharp.Storages
{
    public class StorageRouter : IStorage
    {
        private readonly Branch[] _storageMapping;
        private readonly IStorage _defaultStorage;

        public StorageRouter(Branch[] storageMapping, IStorage defaultStorage = null)
        {
            _storageMapping = storageMapping;
            _defaultStorage = defaultStorage;
        }

        public async UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            var allKeys = new List<string>();
            
            // Get keys from each branch storage
            foreach (var branch in _storageMapping)
            {
                try
                {
                    var keys = await branch.Storage.ListAll(cancellationToken);
                    allKeys.AddRange(keys);
                }
                catch
                {
                    // Skip this branch if an error occurs
                    continue;
                }
            }
            
            // Also get keys from default storage
            if (_defaultStorage != null)
            {
                try
                {
                    var defaultKeys = await _defaultStorage.ListAll(cancellationToken);
                    allKeys.AddRange(defaultKeys);
                }
                catch
                {
                    // Skip if an error occurs
                }
            }
            
            return allKeys.Distinct().ToArray();
        }

        public async UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            var (storage, formattedKey) = FindStorage(key);
            if (storage == null)
            {
                throw new KeyNotFoundException($"No storage found for key: {key}");
            }
            
            return await storage.ReadAsync(formattedKey, cancellationToken);
        }

        public async UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            var (storage, formattedKey) = FindStorage(key);
            if (storage == null)
            {
                throw new KeyNotFoundException($"No storage found for key: {key}");
            }
            
            await storage.WriteAsync(formattedKey, data, cancellationToken);
        }

        public async UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            var (storage, formattedKey) = FindStorage(key);
            if (storage == null)
            {
                throw new KeyNotFoundException($"No storage found for key: {key}");
            }
            
            return await storage.ReadToStreamAsync(formattedKey, cancellationToken);
        }

        public async UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            var (storage, formattedKey) = FindStorage(key);
            if (storage == null)
            {
                throw new KeyNotFoundException($"No storage found for key: {key}");
            }
            
            await storage.WriteAsync(formattedKey, stream, cancellationToken);
        }

        private (IStorage storage, string formattedKey) FindStorage(string key)
        {
            // Check each branch
            foreach (var branch in _storageMapping)
            {
                if (branch.IsSupported(key))
                {
                    var formattedKey = branch.KeyFormatter?.Invoke(key) ?? key;
                    return (branch.Storage, formattedKey);
                }
            }
            
            // Return default storage
            return (_defaultStorage, key);
        }

        public struct Branch
        {
            public Func<string, bool> IsSupported { get; }
            public IStorage Storage { get; }
            public Func<string, string> KeyFormatter { get; }

            public Branch(Func<string, bool> isSupported, IStorage storage)
            {
                IsSupported = isSupported;
                Storage = storage;
                KeyFormatter = null;
            }

            public Branch(Func<string, bool> isSupported, Func<string, string> keyFormatter, IStorage storage)
            {
                IsSupported = isSupported;
                Storage = storage;
                KeyFormatter = keyFormatter;
            }
        }
    }
} 