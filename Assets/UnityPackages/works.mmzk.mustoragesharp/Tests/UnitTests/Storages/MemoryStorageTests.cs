using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StorageSharp.Storages;
using UnityEngine.TestTools;

namespace StorageSharp.Tests.UnitTests
{

public class MemoryStorageTests
{
    [UnityTest]
    public IEnumerator WriteAsync_ShouldStoreData() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("test data");
        var key = "test.txt";

        // When
        await storage.WriteAsync(key, testData);

        // Then
        Assert.AreEqual(1, storage.Count);
    });

    [UnityTest]
    public IEnumerator ReadAsync_ShouldReturnCorrectData() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var expectedData = Encoding.UTF8.GetBytes("test data");
        var key = "test.txt";
        await storage.WriteAsync(key, expectedData);

        // When
        var actualData = await storage.ReadAsync(key);

        // Then
        Assert.AreEqual(expectedData, actualData);
    });

    [UnityTest]
    public IEnumerator ReadAsync_WithNonExistentKey_ShouldThrowKeyNotFoundException() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var key = "nonexistent.txt";

        // When & Then
        try
        {
            await storage.ReadAsync(key);
            Assert.Fail("Expected KeyNotFoundException was not thrown");
        }
        catch (KeyNotFoundException)
        {
            // Expected exception
        }
    });

    [UnityTest]
    public IEnumerator ListAll_ShouldReturnAllKeys() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("test data");
        await storage.WriteAsync("file1.txt", testData);
        await storage.WriteAsync("file2.txt", testData);

        // When
        var keys = await storage.ListAll();

        // Then
        Assert.Contains("file1.txt", keys);
        Assert.Contains("file2.txt", keys);
        Assert.AreEqual(2, keys.Length);
    });

    [UnityTest]
    public IEnumerator WriteAsync_WithEmptyData_ShouldRemoveKey() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("test data");
        var key = "test.txt";
        await storage.WriteAsync(key, testData);
        Assert.AreEqual(1, storage.Count);

        // When
        await storage.WriteAsync(key, new byte[0]);

        // Then
        Assert.AreEqual(0, storage.Count);
    });

    [UnityTest]
    public IEnumerator WriteAsync_WithStream_ShouldStoreData() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("test stream data");
        var key = "stream.txt";
        using var stream = new MemoryStream(testData);

        // When
        await storage.WriteAsync(key, stream);

        // Then
        var storedData = await storage.ReadAsync(key);
        Assert.AreEqual(testData, storedData);
    });

    [UnityTest]
    public IEnumerator ReadToStreamAsync_ShouldReturnStream() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var storage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("test stream data");
        var key = "stream.txt";
        await storage.WriteAsync(key, testData);

        // When
        using var stream = await storage.ReadToStreamAsync(key);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var readData = memoryStream.ToArray();

        // Then
        Assert.AreEqual(testData, readData);
    });

    [Test]
    public void Clear_ShouldRemoveAllData()
    {
        // Given
        var storage = new MemoryStorage();
        storage.WriteAsync("file1.txt", Encoding.UTF8.GetBytes("data1")).GetAwaiter().GetResult();
        storage.WriteAsync("file2.txt", Encoding.UTF8.GetBytes("data2")).GetAwaiter().GetResult();
        Assert.AreEqual(2, storage.Count);

        // When
        storage.Clear();

        // Then
        Assert.AreEqual(0, storage.Count);
    }

    [Test]
    public void Count_ShouldReturnCorrectNumberOfItems()
    {
        // Given
        var storage = new MemoryStorage();
        Assert.AreEqual(0, storage.Count);

        // When
        storage.WriteAsync("file1.txt", Encoding.UTF8.GetBytes("data1")).GetAwaiter().GetResult();

        // Then
        Assert.AreEqual(1, storage.Count);

        // When
        storage.WriteAsync("file2.txt", Encoding.UTF8.GetBytes("data2")).GetAwaiter().GetResult();

        // Then
        Assert.AreEqual(2, storage.Count);
    }
}
}