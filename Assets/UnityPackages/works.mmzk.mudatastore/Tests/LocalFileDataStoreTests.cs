using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Mmzkworks.muDatastore;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

namespace Mmzkworks.muDatastore.Tests
{
    public class LocalFileDataStoreTests
{
    private static string TestFilePath => Path.Combine(Application.temporaryCachePath, "LocalFileDataStore_tests", "data.json");

    [UnityTest]
    public IEnumerator DataStore_New() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
    });

    [UnityTest]
    public IEnumerator DataStore_CanSetAndGetString_AndCreatesFile() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
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
        using var store = new LocalFileDataStore(TestFilePath);
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
        using var store = new LocalFileDataStore(TestFilePath);
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
        using var store = new LocalFileDataStore(TestFilePath);
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
        using var store = new LocalFileDataStore(TestFilePath);
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
        using var store = new LocalFileDataStore(TestFilePath);
        await store.SetAsync("k1", "v1");
        var exists1 = await store.ExistsAsync("k1");
        Assert.IsTrue(exists1);
        await store.DeleteAsync("k1");
        var exists2 = await store.ExistsAsync("k1");
        Assert.IsFalse(exists2);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_SetInt_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.SetAsync("i1", 1);
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_GetInt_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.GetIntAsync("i1");
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_SetFloat_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.SetAsync("f1", 1.23f);
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_GetFloat_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.GetFloatAsync("f1");
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_SetBool_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.SetAsync("b1", true);
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_GetBool_ThrowsException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using var store = new LocalFileDataStore(TestFilePath);
        var success = false;
        try
        {
            await store.GetBoolAsync("b1");
        }
        catch (System.NotImplementedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_Dispose_KeepsSavedValues() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        using (var store = new LocalFileDataStore(TestFilePath))
        {
            await store.SetAsync("k1", "v1");
        }
        using var reopened = new LocalFileDataStore(TestFilePath);
        Assert.AreEqual("v1", await reopened.GetStringAsync("k1"));
        Cleanup();
    });

    [UnityTest]
    public IEnumerator DataStore_AfterDispose_ThrowsObjectDisposedException() => UniTask.ToCoroutine(async () =>
    {
        Cleanup();
        var store = new LocalFileDataStore(TestFilePath);
        store.Dispose();
        store.Dispose();
        var success = false;
        try
        {
            await store.GetStringAsync("k1");
        }
        catch (System.ObjectDisposedException)
        {
            success = true;
        }
        Assert.IsTrue(success);
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
