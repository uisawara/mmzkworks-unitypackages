using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Mmzkworks.muDataStore
{
    public interface IDataStore
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
