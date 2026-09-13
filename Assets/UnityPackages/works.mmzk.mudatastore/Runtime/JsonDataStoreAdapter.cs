using Cysharp.Threading.Tasks;
using Mmzkworks.muDataStore;
using UnityEngine;

namespace Mmzkworks.muDataStore
{
    public class JsonDataStoreAdapter
    {
        private IDataStore _dataStore;

        public JsonDataStoreAdapter(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }
        
        public async UniTask Save<T>(string key, T obj)
        {
            var json = JsonUtility.ToJson(obj);
            await _dataStore.SetAsync(key, json);
        }

        public async UniTask<T> Load<T>(string key)
        {
            var json = await _dataStore.GetStringAsync(key);
            var obj = JsonUtility.FromJson<T>(json);
            return obj;
        }
    }
}