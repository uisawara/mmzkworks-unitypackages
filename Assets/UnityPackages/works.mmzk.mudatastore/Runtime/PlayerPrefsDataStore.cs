using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mmzkworks.muDataStore
{
    public sealed class PlayerPrefsDataStore : IDataStore
    {
        private string _keyPrefix;
        private const string KeyListSuffix = "__KeyList__";
        
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
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetInt(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;        
        }

        public UniTask SetAsync(string key, float value)
        {
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetFloat(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;        
        }

        public UniTask SetAsync(string key, bool value)
        {
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetInt(persistentKey, value? 1 : 0);
            AddToKeyList(key);
            return UniTask.CompletedTask;
        }

        public UniTask SetAsync(string key, string value)
        {
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.SetString(persistentKey, value);
            AddToKeyList(key);
            return UniTask.CompletedTask;
        }

        public UniTask FlushAsync()
        {
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<IEnumerable<string>> GetListAsync()
        {
            var keyList = LoadKeyList();
            return UniTask.FromResult<IEnumerable<string>>(keyList);
        }

        public UniTask<bool> ExistsAsync(string key)
        {
            var persistentKey = GetPersistentKey(key);
            return UniTask.FromResult(PlayerPrefs.HasKey(persistentKey));
        }

        public UniTask<int> GetIntAsync(string key)
        {
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetInt(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask<float> GetFloatAsync(string key)
        {
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetFloat(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask<bool> GetBoolAsync(string key)
        {
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetInt(persistentKey) == 1;
            return UniTask.FromResult(value);
        }

        public UniTask<string> GetStringAsync(string key)
        {
            var persistentKey = GetPersistentKey(key);
            var value = PlayerPrefs.GetString(persistentKey);
            return UniTask.FromResult(value);
        }

        public UniTask DeleteAllAsync()
        {
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
            var persistentKey = GetPersistentKey(key);
            PlayerPrefs.DeleteKey(persistentKey);
            RemoveFromKeyList(key);
            return UniTask.CompletedTask;
        }

        private string GetPersistentKey(string key)
        {
            return _keyPrefix + key;
        }
    }
}
