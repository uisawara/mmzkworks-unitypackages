using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Mmzkworks.muDataStore
{
    public sealed class LocalFileDataStore : IDataStore
    {
        private readonly string _filePath;

        public LocalFileDataStore(string filePath)
        {
            _filePath = filePath;
        }

        public UniTask SetAsync(string key, int value)
        {
            return SetStringValue(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public UniTask SetAsync(string key, float value)
        {
            return SetStringValue(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public UniTask SetAsync(string key, bool value)
        {
            return SetStringValue(key, value ? "1" : "0");
        }

        public UniTask SetAsync(string key, string value)
        {
            return SetStringValue(key, value);
        }

        public UniTask FlushAsync()
        {
            return UniTask.CompletedTask;
        }

        public UniTask<IEnumerable<string>> GetListAsync()
        {
            var dict = LoadAll();
            return UniTask.FromResult((IEnumerable<string>)dict.Keys);
        }

        public UniTask<bool> ExistsAsync(string key)
        {
            var dict = LoadAll();
            return UniTask.FromResult(dict.ContainsKey(key));
        }

        public UniTask<int> GetIntAsync(string key)
        {
            var raw = GetStringValue(key);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return UniTask.FromResult(value);
            }
            return UniTask.FromResult(0);
        }

        public UniTask<float> GetFloatAsync(string key)
        {
            var raw = GetStringValue(key);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return UniTask.FromResult(value);
            }
            return UniTask.FromResult(0f);
        }

        public UniTask<bool> GetBoolAsync(string key)
        {
            var raw = GetStringValue(key);
            if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return UniTask.FromResult(true);
            }
            return UniTask.FromResult(false);
        }

        public UniTask<string> GetStringAsync(string key)
        {
            return UniTask.FromResult(GetStringValue(key));
        }

        public UniTask DeleteAllAsync()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            return UniTask.CompletedTask;
        }

        public UniTask DeleteAsync(string key)
        {
            var dict = LoadAll();
            if (dict.Remove(key))
            {
                if (dict.Count == 0)
                {
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                }
                else
                {
                    SaveAll(dict);
                }
            }
            return UniTask.CompletedTask;
        }

        private Dictionary<string, string> LoadAll()
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var container = StoreContainer.FromJson(json);
                return container.ToDictionary();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private UniTask SetStringValue(string key, string value)
        {
            var dict = LoadAll();
            dict[key] = value ?? string.Empty;
            SaveAll(dict);
            return UniTask.CompletedTask;
        }

        private string GetStringValue(string key)
        {
            var dict = LoadAll();
            return dict.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private void SaveAll(Dictionary<string, string> dict)
        {
            var container = StoreContainer.FromDictionary(dict);
            var json = container.ToJson();
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(_filePath, json);
        }

        [Serializable]
        private class StoreContainer
        {
            public List<Entry> entries = new List<Entry>();

            [Serializable]
            public class Entry
            {
                public string key;
                public string value;
            }

            public static StoreContainer FromDictionary(Dictionary<string, string> dict)
            {
                var c = new StoreContainer();
                foreach (var kv in dict)
                {
                    c.entries.Add(new Entry { key = kv.Key, value = kv.Value });
                }
                return c;
            }

            public Dictionary<string, string> ToDictionary()
            {
                var d = new Dictionary<string, string>();
                if (entries != null)
                {
                    foreach (var e in entries)
                    {
                        if (e != null && e.key != null)
                        {
                            d[e.key] = e.value ?? string.Empty;
                        }
                    }
                }
                return d;
            }

            public string ToJson()
            {
                return UnityEngine.JsonUtility.ToJson(this);
            }

            public static StoreContainer FromJson(string json)
            {
                if (string.IsNullOrEmpty(json))
                {
                    return new StoreContainer();
                }
                try
                {
                    return UnityEngine.JsonUtility.FromJson<StoreContainer>(json) ?? new StoreContainer();
                }
                catch
                {
                    return new StoreContainer();
                }
            }
        }
    }
}

