using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mmzkworks.muDatastore
{
    public sealed class PlayerPrefsDataStore : IDataStore
    {
        private readonly string _keyPrefix;
        private const string KeyListSuffix = "__KeyList__";
        private bool _disposed;
        
        public PlayerPrefsDataStore(string keyPrefix)
        {
            _keyPrefix = keyPrefix;
        }

        private string GetKeyListKey()
        {
            return _keyPrefix + KeyListSuffix;
        }

        private List<string> LoadKeyList()
        {
            var keyListKey = GetKeyListKey();
            if (!PlayerPrefs.HasKey(keyListKey))
            {
                return new List<string>();
            }

            try
            {
                var json = PlayerPrefs.GetString(keyListKey);
                if (string.IsNullOrEmpty(json))
                {
                    return new List<string>();
                }

                var container = JsonUtility.FromJson<KeyListContainer>(json);
                return container?.keys ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void SaveKeyList(List<string> keys)
        {
            var keyListKey = GetKeyListKey();
            var container = new KeyListContainer { keys = keys };
            var json = JsonUtility.ToJson(container);
            PlayerPrefs.SetString(keyListKey, json);
        }

        private void AddToKeyList(string key)
        {
            var keyList = LoadKeyList();
            if (!keyList.Contains(key))
            {
                keyList.Add(key);
                SaveKeyList(keyList);
            }
        }

        private void RemoveFromKeyList(string key)
        {
            var keyList = LoadKeyList();
            if (keyList.Remove(key))
            {
                SaveKeyList(keyList);
            }
        }

        [Serializable]
        private class KeyListContainer
        {
            public List<string> keys = new List<string>();
        }

        public UniTask SetAsync(string key, int value)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetInt(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;        
        }

        public UniTask SetAsync(string key, float value)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetFloat(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;        
        }

        public UniTask SetAsync(string key, bool value)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetInt(persistentKey, value? 1 : 0);
            AddToKeyList(key);
            return UniTask.CompletedTask;
        }

        public UniTask SetAsync(string key, string value)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetString(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;
        }

        public UniTask FlushAsync()
        {
            ThrowIfDisposed();
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<IEnumerable<string>> GetListAsync()
        {
            ThrowIfDisposed();
            var keyList = LoadKeyList();
            return UniTask.FromResult<IEnumerable<string>>(keyList);
        }

        public UniTask<bool> ExistsAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            return UniTask.FromResult(PlayerPrefs.HasKey(persistentKey));
        }

        public UniTask<int> GetIntAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetInt(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask<float> GetFloatAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetFloat(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask<bool> GetBoolAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetInt(persistentKey) == 1;
            return UniTask.FromResult(value);
        }

        public UniTask<string> GetStringAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetString(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask DeleteAllAsync()
        {
            ThrowIfDisposed();
            var keyList = LoadKeyList();
            foreach (var key in keyList)
            {
                var persistentKey = GetPersistentKey(key);
                PlayerPrefs.DeleteKey(persistentKey);
            }
            // Also delete the key list itself
            var keyListKey = GetKeyListKey();
            if (PlayerPrefs.HasKey(keyListKey))
            {
                PlayerPrefs.DeleteKey(keyListKey);
            }
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask DeleteAsync(string key)
        {
            ThrowIfDisposed();
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.DeleteKey(persistentKey);
            RemoveFromKeyList(key);
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            PlayerPrefs.Save();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private string GetPersistentKey(string key)
        {
            return _keyPrefix + key;
        }
    }
}
