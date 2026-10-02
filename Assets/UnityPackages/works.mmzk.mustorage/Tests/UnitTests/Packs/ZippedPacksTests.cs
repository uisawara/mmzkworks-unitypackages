using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mmzkworks.muStorage.Tests.UnitTests
{
    public class ZippedPacksTests
    {
        private string _tempDirectory;
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "muStorageTests", Guid.NewGuid().ToString());
            _tempDirectory = Path.Combine(Path.GetTempPath(), "muStorageTests", Guid.NewGuid().ToString());
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, true);
                if (Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private ZippedPacks CreatePacks()
        {
            return new ZippedPacks(new ZippedPacks.Settings(_tempDirectory), new MemoryStorage());
        }

        private string CreateTestDirectory(string name, params (string path, string content)[] files)
        {
            var dir = Path.Combine(_testDirectory, name);
            Directory.CreateDirectory(dir);
            foreach (var (path, content) in files)
            {
                var filePath = Path.Combine(dir, path);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, content);
            }

            return dir;
        }

        [UnityTest]
        public IEnumerator Add_ShouldCreateZipFile() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir = CreateTestDirectory("test_package", ("test.txt", "test data"));

            // When
            var scheme = await packages.Add(testDir);

            // Then
            Assert.IsNotNull(scheme);
            Assert.AreEqual(testDir, scheme.DirectoryPath);
        });

        [Test]
        public void Add_ShouldThrowForMissingDirectory()
        {
            var packages = CreatePacks();
            var missingDir = Path.Combine(_testDirectory, "missing");

            Assert.Throws<DirectoryNotFoundException>(() => packages.Add(missingDir).GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator Load_ShouldExtractFiles() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir = CreateTestDirectory("test_package",
                ("test.txt", "test data"),
                ("sub/nested.txt", "nested data"));
            var scheme = await packages.Add(testDir);

            // When
            var loadedPath = await packages.Load(scheme);

            // Then
            Assert.IsTrue(Directory.Exists(loadedPath));
            Assert.AreEqual("test data", File.ReadAllText(Path.Combine(loadedPath, "test.txt")));
            Assert.AreEqual("nested data", File.ReadAllText(Path.Combine(loadedPath, "sub", "nested.txt")));
        });

        [UnityTest]
        public IEnumerator ListAll_ShouldReturnSchemes() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir1 = CreateTestDirectory("test_package1", ("test1.txt", "test data 1"));
            var testDir2 = CreateTestDirectory("test_package2", ("test2.txt", "test data 2"));
            await packages.Add(testDir1);
            await packages.Add(testDir2);

            // When
            var schemes = await packages.ListAll();

            // Then
            Assert.AreEqual(2, schemes.Length);
            Assert.IsTrue(schemes.Any(s => s.DirectoryPath == testDir1));
            Assert.IsTrue(schemes.Any(s => s.DirectoryPath == testDir2));
        });

        [UnityTest]
        public IEnumerator Delete_ShouldRemovePackage() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir = CreateTestDirectory("test_package", ("test.txt", "test data"));
            var scheme = await packages.Add(testDir);
            Assert.AreEqual(1, (await packages.ListAll()).Length);

            // When
            await packages.Delete(scheme);

            // Then
            Assert.AreEqual(0, (await packages.ListAll()).Length);
        });

        [UnityTest]
        public IEnumerator Clear_ShouldRemoveAllPackages() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir1 = CreateTestDirectory("test_package1", ("test1.txt", "test data 1"));
            var testDir2 = CreateTestDirectory("test_package2", ("test2.txt", "test data 2"));
            await packages.Add(testDir1);
            var scheme2 = await packages.Add(testDir2);
            var loadedPath = await packages.Load(scheme2);
            Assert.AreEqual(2, (await packages.ListAll()).Length);

            // When
            await packages.Clear();

            // Then
            Assert.AreEqual(0, (await packages.ListAll()).Length);
            Assert.IsFalse(Directory.Exists(loadedPath));
        });

        [UnityTest]
        public IEnumerator Unload_ShouldRemoveExtractedFiles() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir = CreateTestDirectory("test_package", ("test.txt", "test data"));
            var scheme = await packages.Add(testDir);
            var loadedPath = await packages.Load(scheme);
            Assert.IsTrue(Directory.Exists(loadedPath));

            // When
            await packages.Unload(scheme);

            // Then
            Assert.IsFalse(Directory.Exists(loadedPath));
        });

        [UnityTest]
        public IEnumerator Load_ShouldReturnSamePathForSameScheme() => UniTask.ToCoroutine(async () =>
        {
            // Given
            var packages = CreatePacks();
            var testDir = CreateTestDirectory("test_package", ("test.txt", "test data"));
            var scheme = await packages.Add(testDir);

            // When
            var path1 = await packages.Load(scheme);
            var path2 = await packages.Load(scheme);

            // Then
            Assert.AreEqual(path1, path2);
        });
    }
}
