using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Mmzkworks.muDataStore;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

namespace Mmzkworks.muDataStore.Tests
{
    public class LocalFileDataStoreTestScript
{
    private static string TestFilePath => Path.Combine(Application.temporaryCachePath, "LocalFileDataStore_tests", "data.json");

    [UnityTest]
    public IEnumerator DataStore_New() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
    });

    [UnityTest]
    public IEnumerator DataStore_CanSetAndGetString_AndCreatesFile() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        var v = await store.GetStringAsync("k1");
        Assert.AreEqual("v1", v);
        Assert.IsTrue(File.Exists(TestFilePath));
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_CanFlush() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        await store.FlushAsync();
        var v = await store.GetStringAsync("k1");
        Assert.AreEqual("v1", v);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_GetListAsync_ReturnsKeys() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        await store.SetAsync("k2", "v2");
        var list = await store.GetListAsync();
        var set = new HashSet<string>(list);
        Assert.IsTrue(set.Contains("k1"));
        Assert.IsTrue(set.Contains("k2"));
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_ExistsAsync_CanCheckExists() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("exists1", "x");
        var exists = await store.ExistsAsync("exists1");
        Assert.IsTrue(exists);
        await store.DeleteAllAsync();
        var exists2 = await store.ExistsAsync("exists1");
        Assert.IsFalse(exists2);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_DeleteAllAsync_DeletesFileAndData() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        await store.SetAsync("k2", "v2");
        Assert.IsTrue(File.Exists(TestFilePath));
        await store.DeleteAllAsync();
        Assert.IsFalse(File.Exists(TestFilePath));
        var list = await store.GetListAsync();
        Assert.IsFalse(new HashSet<string>(list).Contains("k1"));
        var v = await store.GetStringAsync("k1");
        Assert.AreEqual(string.Empty, v);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_DeleteAsync_CanDeleteKey() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        var exists1 = await store.ExistsAsync("k1");
        Assert.IsTrue(exists1);
        await store.DeleteAsync("k1");
        var exists2 = await store.ExistsAsync("k1");
        Assert.IsFalse(exists2);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_CanSetAndGetInt() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("i1", 42);
        var v = await store.GetIntAsync("i1");
        Assert.AreEqual(42, v);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_CanSetAndGetFloat() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("f1", 1.25f);
        var v = await store.GetFloatAsync("f1");
        Assert.AreEqual(1.25f, v);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_CanSetAndGetBool() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("b1", true);
        var v = await store.GetBoolAsync("b1");
        Assert.IsTrue(v);
        await store.SetAsync("b1", false);
        v = await store.GetBoolAsync("b1");
        Assert.IsFalse(v);
        Cleanup();
    });

    private static void Cleanup()
    {
        var dir = Path.GetDirectoryName(TestFilePath);
        if (File.Exists(TestFilePath))
        {
            File.Delete(TestFilePath);
        }
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
}
