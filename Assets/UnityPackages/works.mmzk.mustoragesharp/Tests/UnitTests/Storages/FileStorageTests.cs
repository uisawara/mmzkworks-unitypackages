using System;
using System.Collections;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StorageSharp.Storages;
using UnityEngine.TestTools;

namespace StorageSharp.Tests.UnitTests
{

    public class FileStorageTests : IDisposable
    {
        private readonly string _testDirectory;

        public FileStorageTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "StorageSharpTests", Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }

        [UnityTest]
        public IEnumerator WriteAsync_ShouldCreateFile() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";

            // When
            await storage.WriteAsync(key, testData);

            // Then
            var filePath = Path.Combine(_testDirectory, key);
            Assert.IsTrue(File.Exists(filePath));
        });

        [UnityTest]
        public IEnumerator ReadAsync_ShouldReturnCorrectData() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
            var expectedData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await storage.WriteAsync(key, expectedData);

            // When
            var actualData = await storage.ReadAsync(key);

            // Then
            Assert.AreEqual(expectedData, actualData);
        });

        [UnityTest]
        public IEnumerator ReadAsync_WithNonExistentKey_ShouldThrowFileNotFoundException() =>
            UniTask.ToCoroutine(async () =>
            {
                // Given
                var storage = new FileStorage(_testDirectory);
                var key = "nonexistent.txt";

                // When & Then
                try
                {
                    await storage.ReadAsync(key);
                    Assert.Fail("Expected FileNotFoundException was not thrown");
                }
                catch (FileNotFoundException)
                {
                    // Expected exception
                }
            });

        [UnityTest]
        public IEnumerator ListAll_ShouldReturnAllKeys() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
            var testData = Encoding.UTF8.GetBytes("test data");
            await storage.WriteAsync("file1.txt", testData);
            await storage.WriteAsync("file2.txt", testData);
            await storage.WriteAsync("subdir/file3.txt", testData);

            // When
            var keys = await storage.ListAll();

            // Then
            Assert.Contains("file1.txt", keys);
            Assert.Contains("file2.txt", keys);
            Assert.Contains("subdir/file3.txt", keys);
            Assert.AreEqual(3, keys.Length);
        });

        [UnityTest]
        public IEnumerator WriteAsync_WithEmptyData_ShouldDeleteFile() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
            var testData = Encoding.UTF8.GetBytes("test data");
            var key = "test.txt";
            await storage.WriteAsync(key, testData);
            Assert.IsTrue(File.Exists(Path.Combine(_testDirectory, key)));

            // When
            await storage.WriteAsync(key, new byte[0]);

            // Then
            Assert.IsFalse(File.Exists(Path.Combine(_testDirectory, key)));
        });

        [UnityTest]
        public IEnumerator WriteAsync_WithStream_ShouldCreateFile() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
            var testData = Encoding.UTF8.GetBytes("test stream data");
            var key = "stream.txt";
            using var stream = new MemoryStream(testData);

            // When
            await storage.WriteAsync(key, stream);

            // Then
            var filePath = Path.Combine(_testDirectory, key);
            Assert.IsTrue(File.Exists(filePath));
            var fileData = await File.ReadAllBytesAsync(filePath);
            Assert.AreEqual(testData, fileData);
        });

        [UnityTest]
        public IEnumerator ReadToStreamAsync_ShouldReturnStream() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var storage = new FileStorage(_testDirectory);
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
    }
}