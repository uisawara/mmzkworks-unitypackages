using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StorageSharp.Storages;
using UnityEngine.TestTools;

namespace StorageSharp.Tests.UnitTests
{

public class StorageRouterTests
{
    [UnityTest]
    public IEnumerator ReadAsync_WithMatchingBranch_ShouldUseCorrectStorage() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("http data");
        var key = "http://example.com/file.txt";
        
        await httpStorage.WriteAsync(key, testData);
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        var result = await storage.ReadAsync(key);

        // Then
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator ReadAsync_WithKeyFormatter_ShouldTransformKey() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var fileStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("file data");
        var originalKey = "file://data/test.txt";
        var transformedKey = "data/test.txt";
        
        await fileStorage.WriteAsync(transformedKey, testData);
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("file://"),
                key => key.Substring("file://".Length),
                fileStorage)
        }, defaultStorage);

        // When
        var result = await storage.ReadAsync(originalKey);

        // Then
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator ReadAsync_WithNoMatchingBranch_ShouldUseDefaultStorage() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("default data");
        var key = "regular-file.txt";
        
        await defaultStorage.WriteAsync(key, testData);
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        var result = await storage.ReadAsync(key);

        // Then
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator ReadAsync_WithNoMatchingBranchAndNoDefault_ShouldThrowException() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var key = "regular-file.txt";
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, null);

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
    public IEnumerator WriteAsync_WithMatchingBranch_ShouldWriteToCorrectStorage() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("http data");
        var key = "https://example.com/file.txt";
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        await storage.WriteAsync(key, testData);

        // Then
        var result = await httpStorage.ReadAsync(key);
        Assert.AreEqual(testData, result);
        Assert.AreEqual(0, defaultStorage.Count);
    });

    [UnityTest]
    public IEnumerator WriteAsync_WithKeyFormatter_ShouldTransformKeyBeforeWrite() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var fileStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("file data");
        var originalKey = "file://data/test.txt";
        var transformedKey = "data/test.txt";
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("file://"),
                key => key.Substring("file://".Length),
                fileStorage)
        }, defaultStorage);

        // When
        await storage.WriteAsync(originalKey, testData);

        // Then
        var result = await fileStorage.ReadAsync(transformedKey);
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator ListAll_ShouldReturnKeysFromAllStorages() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var fileStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        
        await httpStorage.WriteAsync("http://example.com/file1.txt", Encoding.UTF8.GetBytes("data1"));
        await fileStorage.WriteAsync("local/file2.txt", Encoding.UTF8.GetBytes("data2"));
        await defaultStorage.WriteAsync("regular-file3.txt", Encoding.UTF8.GetBytes("data3"));
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage),
            new StorageRouter.Branch(
                key => key.StartsWith("file://"),
                key => key.Substring("file://".Length),
                fileStorage)
        }, defaultStorage);

        // When
        var keys = await storage.ListAll();

        // Then
        Assert.Contains("http://example.com/file1.txt", keys);
        Assert.Contains("local/file2.txt", keys);
        Assert.Contains("regular-file3.txt", keys);
        Assert.AreEqual(3, keys.Length);
    });

    [UnityTest]
    public IEnumerator ListAll_WithDuplicateKeys_ShouldReturnUniqueKeys() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var duplicateKey = "duplicate.txt";
        
        await httpStorage.WriteAsync(duplicateKey, Encoding.UTF8.GetBytes("data1"));
        await defaultStorage.WriteAsync(duplicateKey, Encoding.UTF8.GetBytes("data2"));
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        var keys = await storage.ListAll();

        // Then
        Assert.AreEqual(1, keys.Count(k => k == duplicateKey));
    });

    [UnityTest]
    public IEnumerator ReadToStreamAsync_ShouldWorkWithMatchingBranch() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("stream data");
        var key = "http://example.com/stream.txt";
        
        await httpStorage.WriteAsync(key, testData);
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        using var stream = await storage.ReadToStreamAsync(key);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var result = memoryStream.ToArray();

        // Then
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator WriteAsync_WithStream_ShouldWorkWithMatchingBranch() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("stream data");
        var key = "http://example.com/stream.txt";
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage)
        }, defaultStorage);

        // When
        using var stream = new MemoryStream(testData);
        await storage.WriteAsync(key, stream);

        // Then
        var result = await httpStorage.ReadAsync(key);
        Assert.AreEqual(testData, result);
    });

    [UnityTest]
    public IEnumerator MultipleBranches_ShouldSelectFirstMatchingBranch() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var httpStorage = new MemoryStorage();
        var genericStorage = new MemoryStorage();
        var defaultStorage = new MemoryStorage();
        var testData = Encoding.UTF8.GetBytes("http data");
        var key = "http://example.com/file.txt";
        
        await httpStorage.WriteAsync(key, testData);
        await genericStorage.WriteAsync(key, Encoding.UTF8.GetBytes("generic data"));
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("http://") || key.StartsWith("https://"),
                httpStorage),
            new StorageRouter.Branch(
                key => key.Contains("example.com"),
                genericStorage)
        }, defaultStorage);

        // When
        var result = await storage.ReadAsync(key);

        // Then
        Assert.AreEqual(testData, result); // Should use httpStorage (first match)
    });

    [UnityTest]
    public IEnumerator ListAll_WithFailingStorage_ShouldContinueWithOtherStorages() => UniTask.ToCoroutine(async () =>
    {
        // Given
        var workingStorage = new MemoryStorage();
        var failingStorage = new FailingStorage();
        var defaultStorage = new MemoryStorage();
        
        await workingStorage.WriteAsync("working.txt", Encoding.UTF8.GetBytes("data"));
        await defaultStorage.WriteAsync("default.txt", Encoding.UTF8.GetBytes("data"));
        
        var storage = new StorageRouter(new[]
        {
            new StorageRouter.Branch(
                key => key.StartsWith("working"),
                workingStorage),
            new StorageRouter.Branch(
                key => key.StartsWith("failing"),
                failingStorage)
        }, defaultStorage);

        // When
        var keys = await storage.ListAll();

        // Then
        Assert.Contains("working.txt", keys);
        Assert.Contains("default.txt", keys);
        Assert.AreEqual(2, keys.Length);
    });

    // Helper class for testing error scenarios
    private class FailingStorage : IStorage
    {
        public UniTask<string[]> ListAll(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Storage failed");
        }

        public UniTask<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Storage failed");
        }

        public UniTask WriteAsync(string key, byte[] data, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Storage failed");
        }

        public UniTask<Stream> ReadToStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Storage failed");
        }

        public UniTask WriteAsync(string key, Stream stream, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Storage failed");
        }
    }
}
} 