using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Mmzkworks.muDatastore
{
    /// <summary>
    /// Key-value data store. Dispose flushes pending changes and closes the store;
    /// any call after Dispose throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    public interface IDataStore : IDisposable
    {
        UniTask SetAsync(string key, int value);
        UniTask SetAsync(string key, float value);
        UniTask SetAsync(string key, bool value);
        UniTask SetAsync(string key, string value);
        UniTask FlushAsync();
        UniTask<IEnumerable<string>> GetListAsync();
        UniTask<bool> ExistsAsync(string key);
        UniTask<int> GetIntAsync(string key);
        UniTask<float> GetFloatAsync(string key);
        UniTask<bool> GetBoolAsync(string key);
        UniTask<string> GetStringAsync(string key);
        UniTask DeleteAllAsync();
        UniTask DeleteAsync(string key);
    }
}
