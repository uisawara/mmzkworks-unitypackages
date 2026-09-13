using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StorageSharp.Storages;
using UnityEngine.TestTools;

namespace StorageSharp.Tests.UnitTests
{
    public class EncryptedStorageTests
    {
        [UnityTest]
        public IEnumerator WriteAsync_AndReadAsync_ShouldEncryptAndDecryptData() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var password = "test-password";
            var storage = new EncryptedStorage(baseStorage, password);
            var testData = Encoding.UTF8.GetBytes("This is test data that will be encrypted.");
            var key = "test.txt";

            // When
            await storage.WriteAsync(key, testData);
            var decryptedData = await storage.ReadAsync(key);

            // Then
            Assert.AreEqual(testData, decryptedData);

            // Verify that the underlying storage contains encrypted data (different from original)
            var encryptedData = await baseStorage.ReadAsync(key);
            Assert.AreNotEqual(testData, encryptedData);
        });

        [UnityTest]
        public IEnumerator Constructor_WithPassword_ShouldCreateValidInstance() => UniTask.ToCoroutine(() =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var password = "test-password-123";

            // When
            var storage = new EncryptedStorage(baseStorage, password);

            // Then
            Assert.IsNotNull(storage);
            return UniTask.CompletedTask;
        });

        [UnityTest]
        public IEnumerator Constructor_WithKeyAndIV_ShouldCreateValidInstance() => UniTask.ToCoroutine(() =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var key = new byte[32]; // 32-byte key for AES-256
            var iv = new byte[16]; // 16-byte IV

            // Initialize with test values
            for (var i = 0; i < 32; i++) key[i] = (byte)(i + 1);
            for (var i = 0; i < 16; i++) iv[i] = (byte)(i + 100);

            // When
            var storage = new EncryptedStorage(baseStorage, key, iv);

            // Then
            Assert.IsNotNull(storage);
            return UniTask.CompletedTask;
        });

        [Test]
        public void Constructor_WithNullBaseStorage_ShouldThrowArgumentNullException()
        {
            // Given
            IStorage nullStorage = null;
            var password = "test-password";

            // When & Then
            Assert.Throws<ArgumentNullException>(() => new EncryptedStorage(nullStorage, password));
        }

        [Test]
        public void Constructor_WithNullPassword_ShouldThrowArgumentException()
        {
            // Given
            var baseStorage = new MemoryStorage();
            string nullPassword = null;

            // When & Then
            Assert.Throws<ArgumentException>(() => new EncryptedStorage(baseStorage, nullPassword));
        }

        [Test]
        public void Constructor_WithNullKey_ShouldThrowArgumentException()
        {
            // Given
            var baseStorage = new MemoryStorage();
            byte[] nullKey = null;
            var iv = new byte[16];

            // When & Then
            Assert.Throws<ArgumentException>(() => new EncryptedStorage(baseStorage, nullKey, iv));
        }

        [Test]
        public void Constructor_WithNullIV_ShouldThrowArgumentException()
        {
            // Given
            var baseStorage = new MemoryStorage();
            var key = new byte[32];
            byte[] nullIV = null;

            // When & Then
            Assert.Throws<ArgumentException>(() => new EncryptedStorage(baseStorage, key, nullIV));
        }

        [Test]
        public void Constructor_WithInvalidKeyLength_ShouldThrowArgumentException()
        {
            // Given
            var baseStorage = new MemoryStorage();
            var invalidKey = new byte[16]; // Should be 32 bytes for AES-256
            var iv = new byte[16];

            // When & Then
            Assert.Throws<ArgumentException>(() => new EncryptedStorage(baseStorage, invalidKey, iv));
        }

        [Test]
        public void Constructor_WithInvalidIVLength_ShouldThrowArgumentException()
        {
            // Given
            var baseStorage = new MemoryStorage();
            var key = new byte[32];
            var invalidIV = new byte[8]; // Should be 16 bytes

            // When & Then
            Assert.Throws<ArgumentException>(() => new EncryptedStorage(baseStorage, key, invalidIV));
        }

        [UnityTest]
        public IEnumerator ReadAsync_WithDifferentPassword_ShouldThrowCryptographicException() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var baseStorage = new MemoryStorage();
                var storage1 = new EncryptedStorage(baseStorage, "password1");
                var storage2 = new EncryptedStorage(baseStorage, "password2");
                var testData = Encoding.UTF8.GetBytes("test data for different passwords");
                var key = "test.txt";

                // When
                await storage1.WriteAsync(key, testData);

                // Then
                try
                {
                    await storage2.ReadAsync(key);
                    Assert.Fail("Expected CryptographicException was not thrown");
                }
                catch (CryptographicException)
                {
                    // Expected exception
                }
            });

        [UnityTest]
        public IEnumerator WriteAsync_WithEmptyData_ShouldDeleteFromBaseStorage() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";

            // Write data first
            await storage.WriteAsync(key, testData);
            var keys = await storage.ListAll();
            Assert.Contains(key, keys);

            // When
            await storage.WriteAsync(key, new byte[0]);

            // Then
            keys = await storage.ListAll();
            Assert.IsFalse(keys.Contains(key));
        });

        [UnityTest]
        public IEnumerator WriteAsync_WithNullData_ShouldDeleteFromBaseStorage() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";

            // Write data first
            await storage.WriteAsync(key, testData);
            var keys = await storage.ListAll();
            Assert.Contains(key, keys);

            // When
            await storage.WriteAsync(key, (byte[])null);

            // Then
            keys = await storage.ListAll();
            Assert.IsFalse(keys.Contains(key));
        });

        [UnityTest]
        public IEnumerator ListAll_ShouldReturnKeysFromBaseStorage() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("test data");

            // When
            await storage.WriteAsync("file1.txt", testData);
            await storage.WriteAsync("file2.txt", testData);
            var keys = await storage.ListAll();

            // Then
            Assert.Contains("file1.txt", keys);
            Assert.Contains("file2.txt", keys);
            Assert.AreEqual(2, keys.Length);
        });

        [UnityTest]
        public IEnumerator ReadAsync_WithNonExistentKey_ShouldThrowKeyNotFoundException() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var baseStorage = new MemoryStorage();
                var storage = new EncryptedStorage(baseStorage, "test-password");
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
        public IEnumerator WriteAsync_WithStream_ShouldEncryptAndStore() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("stream test data");
            var key = "stream.txt";

            // When
            using var stream = new MemoryStream(testData);
            await storage.WriteAsync(key, stream);

            // Then
            var decryptedData = await storage.ReadAsync(key);
            Assert.AreEqual(testData, decryptedData);
        });

        [UnityTest]
        public IEnumerator ReadToStreamAsync_ShouldDecryptAndReturnStream() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("stream test data");
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

        [UnityTest]
        public IEnumerator EncryptionDecryption_WithCustomKeyIV_ShouldWork() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var key = new byte[32];
            var iv = new byte[16];

            // Initialize with specific values
            for (var i = 0; i < 32; i++) key[i] = (byte)(i + 1);
            for (var i = 0; i < 16; i++) iv[i] = (byte)(i + 100);

            var storage = new EncryptedStorage(baseStorage, key, iv);
            var testData = Encoding.UTF8.GetBytes("custom key/IV test data");
            var dataKey = "custom.txt";

            // When
            await storage.WriteAsync(dataKey, testData);
            var decryptedData = await storage.ReadAsync(dataKey);

            // Then
            Assert.AreEqual(testData, decryptedData);
        });

        [UnityTest]
        public IEnumerator EncryptionDecryption_WithSameKeyIV_ShouldProduceSameResults() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var baseStorage = new MemoryStorage();
                var key = new byte[32];
                var iv = new byte[16];

                // Initialize with specific values
                for (var i = 0; i < 32; i++) key[i] = (byte)(i + 1);
                for (var i = 0; i < 16; i++) iv[i] = (byte)(i + 100);

                var storage1 = new EncryptedStorage(baseStorage, key, iv);
                var storage2 = new EncryptedStorage(baseStorage, key, iv);
                var testData = Encoding.UTF8.GetBytes("consistency test data");
                var dataKey = "consistency.txt";

                // When
                await storage1.WriteAsync(dataKey, testData);
                var decryptedData = await storage2.ReadAsync(dataKey);

                // Then
                Assert.AreEqual(testData, decryptedData);
            });

        [UnityTest]
        public IEnumerator EncryptionDecryption_WithLargeData_ShouldWork() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "large-data-password");
            var largeData = new byte[10000]; // 10KB of data
            new Random().NextBytes(largeData);
            var key = "large.bin";

            // When
            await storage.WriteAsync(key, largeData);
            var decryptedData = await storage.ReadAsync(key);

            // Then
            Assert.AreEqual(largeData, decryptedData);

            // Verify encryption occurred
            var encryptedData = await baseStorage.ReadAsync(key);
            Assert.AreNotEqual(largeData, encryptedData);
            Assert.True(encryptedData.Length > largeData.Length); // Due to padding
        });

        [UnityTest]
        public IEnumerator EncryptionDecryption_WithDifferentBackendStorages_ShouldWork() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var testData = Encoding.UTF8.GetBytes("backend compatibility test");
                var password = "backend-test-password";
                var key = "backend.txt";

                // Test with MemoryStorage
                var memoryBackend = new MemoryStorage();
                var encryptedMemory = new EncryptedStorage(memoryBackend, password);
                await encryptedMemory.WriteAsync(key, testData);
                var memoryResult = await encryptedMemory.ReadAsync(key);
                Assert.AreEqual(testData, memoryResult);

                // Test with CachedStorage
                var cache = new MemoryStorage();
                var origin = new MemoryStorage();
                var cachedBackend = new CachedStorage(cache, origin);
                var encryptedCached = new EncryptedStorage(cachedBackend, password);
                await encryptedCached.WriteAsync(key, testData);
                var cachedResult = await encryptedCached.ReadAsync(key);
                Assert.AreEqual(testData, cachedResult);
            });

        [UnityTest]
        public IEnumerator WriteAsync_WithEmptyStream_ShouldDeleteFromBaseStorage() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var baseStorage = new MemoryStorage();
            var storage = new EncryptedStorage(baseStorage, "test-password");
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";

            // Write data first
            await storage.WriteAsync(key, testData);
            var keys = await storage.ListAll();
            Assert.Contains(key, keys);

            // When
            using var emptyStream = new MemoryStream();
            await storage.WriteAsync(key, emptyStream);

            // Then
            keys = await storage.ListAll();
            Assert.IsFalse(keys.Contains(key));
        });

        [UnityTest]
        public IEnumerator ReadToStreamAsync_WithNonExistentKey_ShouldThrowKeyNotFoundException() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var baseStorage = new MemoryStorage();
                var storage = new EncryptedStorage(baseStorage, "test-password");
                var key = "nonexistent.txt";

                // When & Then
                try
                {
                    await storage.ReadToStreamAsync(key);
                    Assert.Fail("Expected KeyNotFoundException was not thrown");
                }
                catch (KeyNotFoundException)
                {
                    // Expected exception
                }
            });
    }
}