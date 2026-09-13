using System.Collections;
using Cysharp.Threading.Tasks;
using Mmzkworks.muDataStore;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

namespace Mmzkworks.muDataStore.Tests
{
    public class JsonDataStoreAdapterTestScript
{
	private const string TestKeyPrefix = "DataStore_";

	[UnityTest]
	public IEnumerator Adapter_New() => UniTask.ToCoroutine(async () =>
	{
		IDataStore dataStore = new PlayerPrefsDataStore(TestKeyPrefix);
		var adapter = new JsonDataStoreAdapter(dataStore);
	});

	[UnityTest]
	public IEnumerator Adapter_CanSaveAndLoad_RoundTrip() => UniTask.ToCoroutine(async () =>
	{
		const string key = "json_roundtrip";
		var dataStore = new PlayerPrefsDataStore(TestKeyPrefix);
		var adapter = new JsonDataStoreAdapter(dataStore);

		await dataStore.DeleteAsync(key);
		var src = new SampleData { score = 123, name = "abc", flag = true };
		await adapter.Save(key, src);
		var dst = await adapter.Load<SampleData>(key);

		Assert.AreEqual(src.score, dst.score);
		Assert.AreEqual(src.name, dst.name);
		Assert.AreEqual(src.flag, dst.flag);
		await dataStore.DeleteAsync(key);
	});

	[UnityTest]
	public IEnumerator Adapter_Save_Overwrite_Wins() => UniTask.ToCoroutine(async () =>
	{
		const string key = "json_overwrite";
		var dataStore = new PlayerPrefsDataStore(TestKeyPrefix);
		var adapter = new JsonDataStoreAdapter(dataStore);

		await dataStore.DeleteAsync(key);
		var v1 = new SampleData { score = 1, name = "v1", flag = false };
		var v2 = new SampleData { score = 2, name = "v2", flag = true };
		await adapter.Save(key, v1);
		await adapter.Save(key, v2);
		var loaded = await adapter.Load<SampleData>(key);

		Assert.AreEqual(v2.score, loaded.score);
		Assert.AreEqual(v2.name, loaded.name);
		Assert.AreEqual(v2.flag, loaded.flag);
		await dataStore.DeleteAsync(key);
	});

	[System.Serializable]
	private struct SampleData
	{
		public int score;
		public string name;
		public bool flag;
	}
}
}
