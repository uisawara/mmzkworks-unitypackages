using System.Collections;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StorageSharp.Storages;
using UnityEngine.TestTools;

namespace StorageSharp.Tests.UnitTests
{
    public class CachedStorageTests
    {
        [UnityTest]
        public IEnumerator ReadAsync_WithCacheHit_ShouldReturnFromCache() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await cachedStorage.WriteAsync(key, testData);

            // When
            var result = await cachedStorage.ReadAsync(key);

            // Then
            Assert.AreEqual(testData, result);
            Assert.AreEqual(1, cachedStorage.CacheHitCount);
        });

        [UnityTest]
        public IEnumerator ReadAsync_WithCacheMiss_ShouldReturnFromOriginAndCache() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await origin.WriteAsync(key, testData);

            // When
            var result = await cachedStorage.ReadAsync(key);

            // Then
            Assert.AreEqual(testData, result);
            Assert.AreEqual(0, cachedStorage.CacheHitCount);

            var cachedData = await cache.ReadAsync(key);
            Assert.AreEqual(testData, cachedData);
        });

        [UnityTest]
        public IEnumerator WriteAsync_ShouldWriteToBothCacheAndOrigin() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";

            // When
            await cachedStorage.WriteAsync(key, testData);

            // Then
            var cacheData = await cache.ReadAsync(key);
            var originData = await origin.ReadAsync(key);

            Assert.AreEqual(testData, cacheData);
            Assert.AreEqual(testData, originData);
        });

        [UnityTest]
        public IEnumerator ListAll_ShouldReturnFromOrigin() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            await origin.WriteAsync("file1.txt", testData);
            await origin.WriteAsync("file2.txt", testData);

            // When
            var keys = await cachedStorage.ListAll();

            // Then
            Assert.Contains("file1.txt", keys);
            Assert.Contains("file2.txt", keys);
            Assert.AreEqual(2, keys.Length);
        });

        [UnityTest]
        public IEnumerator ClearCache_ShouldClearCache() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await cachedStorage.WriteAsync(key, testData);
            Assert.AreEqual(1, cachedStorage.CacheHitCount);

            // When
            await cachedStorage.ClearCache();

            // Then
            Assert.AreEqual(0, cachedStorage.CacheHitCount);
            Assert.AreEqual(0, cache.Count);
        });

        [UnityTest]
        public IEnumerator ReadToStreamAsync_WithCacheHit_ShouldReturnFromCache() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test stream data");
            var key = "stream.txt";
            await cachedStorage.WriteAsync(key, testData);

            // When
            using var stream = await cachedStorage.ReadToStreamAsync(key);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var readData = memoryStream.ToArray();

            // Then
            Assert.AreEqual(testData, readData);
            Assert.AreEqual(1, cachedStorage.CacheHitCount);
        });

        [UnityTest]
        public IEnumerator WriteAsync_WithStream_ShouldWriteToBothCacheAndOrigin() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test stream data");
            var key = "stream.txt";
            using var stream = new MemoryStream(testData);

            // When
            await cachedStorage.WriteAsync(key, stream);

            // Then
            var cacheData = await cache.ReadAsync(key);
            var originData = await origin.ReadAsync(key);

            Assert.AreEqual(testData, cacheData);
            Assert.AreEqual(testData, originData);
        });

        [UnityTest]
        public IEnumerator CacheHitCount_ShouldTrackCacheHits() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var cache = new MemoryStorage();
            var origin = new MemoryStorage();
            var cachedStorage = new CachedStorage(cache, origin);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await cachedStorage.WriteAsync(key, testData);
            Assert.AreEqual(1, cachedStorage.CacheHitCount);

            // When
            await cachedStorage.ReadAsync(key);

            // Then
            Assert.AreEqual(1, cachedStorage.CacheHitCount);

            // When
            await cachedStorage.ReadAsync(key);

            // Then
            Assert.AreEqual(1, cachedStorage.CacheHitCount);
        });
    }
}