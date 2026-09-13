using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using StorageSharp.Storages;
using UnityEngine;

namespace StorageSharpExample
{
    internal class Program
    {
        private static async UniTask Main(string[] args)
        {
            Debug.Log("StorageSharp Sample Program");
            Debug.Log("================================");

            // Basic storage usage example
            await BasicStorageExample();

            // Cached storage usage example
            await CachedStorageExample();

            // ZIP packages usage example
            //await ZippedPackagesExample();

            // Encrypted storage usage example
            await EncryptedStorageExample();

            Debug.Log("All samples completed.");
        }

        private static async UniTask BasicStorageExample()
        {
            Debug.Log("1. Basic Storage Usage Example");
            Debug.Log("-------------------------------");

            // File storage usage
            var fileStorage = new FileStorage("ExampleStorage");

            // Data writing
            var testData = Encoding.UTF8.GetBytes("Hello, StorageSharp!");
            await fileStorage.WriteAsync("test.txt", testData);
            Debug.Log("✓ Data written to file storage");

            // Data reading
            var readData = await fileStorage.ReadAsync("test.txt");
            var readText = Encoding.UTF8.GetString(readData);
            Debug.Log($"✓ Read data: {readText}");

            // Memory storage usage
            var memoryStorage = new MemoryStorage();
            await memoryStorage.WriteAsync("memory-test.txt", testData);
            Debug.Log("✓ Data written to memory storage");

            var memoryData = await memoryStorage.ReadAsync("memory-test.txt");
            var memoryText = Encoding.UTF8.GetString(memoryData);
            Debug.Log($"✓ Data read from memory: {memoryText}");
        }

        private static async UniTask CachedStorageExample()
        {
            Debug.Log("2. Cached Storage Usage Example");
            Debug.Log("-----------------------------------");

            // Cached storage setup
            var cache = new MemoryStorage();
            var origin = new FileStorage("OriginStorage");
            var cachedStorage = new CachedStorage(cache, origin);

            // Data writing
            var data = Encoding.UTF8.GetBytes("Cached data example");
            await cachedStorage.WriteAsync("cached-file.txt", data);
            Debug.Log("✓ Data written to cached storage");

            // First read (cache miss)
            var readData1 = await cachedStorage.ReadAsync("cached-file.txt");
            Debug.Log($"✓ First read: {Encoding.UTF8.GetString(readData1)}");

            // Second read (cache hit)
            var readData2 = await cachedStorage.ReadAsync("cached-file.txt");
            Debug.Log($"✓ Second read: {Encoding.UTF8.GetString(readData2)}");

            Debug.Log($"✓ Cache hit count: {cachedStorage.CacheHitCount}");
        }

        /*
        private static async UniTask ZippedPackagesExample()
        {
            Debug.Log("3. ZIP Packages Usage Example");
            Debug.Log("-------------------------");

            // Create test directory
            var testDir = "TestDirectory";
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            // Create test files
            var testFile1 = Path.Combine(testDir, "file1.txt");
            var testFile2 = Path.Combine(testDir, "file2.txt");
            var subDir = Path.Combine(testDir, "subdir");
            Directory.CreateDirectory(subDir);
            var testFile3 = Path.Combine(subDir, "file3.txt");

            await File.WriteAllTextAsync(testFile1, "This is file 1");
            await File.WriteAllTextAsync(testFile2, "This is file 2");
            await File.WriteAllTextAsync(testFile3, "This is file 3 in subdir");

            Debug.Log("✓ Test directory and files created");

            // ZIP packages setup
            var storage = new FileStorage("ZippedPackages");
            var packages = new ZippedPacks(
                new ZippedPacks.Settings("Tmp/Packages/"),
                storage
            );

            // Add directory to archive
            var archiveScheme = await packages.Add(testDir);
            Debug.Log($"✓ Directory added to archive: {archiveScheme.DirectoryPath}");

            // Load archive
            var loadedPath = await packages.Load(archiveScheme);
            Debug.Log($"✓ Archive loaded: {loadedPath}");

            // Check loaded file
            var loadedFile1 = Path.Combine(loadedPath, "file1.txt");
            if (File.Exists(loadedFile1))
            {
                var content = await File.ReadAllTextAsync(loadedFile1);
                Debug.Log($"✓ Loaded file content: {content}");
            }

            // Unload archive
            await packages.Unload(archiveScheme);
            Debug.Log("✓ Archive unloaded");

            // Delete archive
            await packages.Delete(archiveScheme);
            Debug.Log("✓ Archive deleted");

            // Clean up test directory
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
        */

        private static async UniTask EncryptedStorageExample()
        {
            Debug.Log("4. Encrypted Storage Usage Example");
            Debug.Log("-----------------------------------");

            // Setup encrypted storage with file storage as backend
            var baseStorage = new FileStorage("EncryptedStorage");
            var password = "my-secret-password-123";
            var encryptedStorage = new EncryptedStorage(baseStorage, password);

            // Test data
            var testData = Encoding.UTF8.GetBytes("これは暗号化されるテストデータです！");
            var key = "encrypted-file.txt";

            // Write encrypted data
            await encryptedStorage.WriteAsync(key, testData);
            Debug.Log("✓ Data written to encrypted storage");

            // Read decrypted data
            var decryptedData = await encryptedStorage.ReadAsync(key);
            var decryptedText = Encoding.UTF8.GetString(decryptedData);
            Debug.Log($"✓ Read decrypted data: {decryptedText}");

            // Verify that the underlying storage contains encrypted data
            var encryptedRawData = await baseStorage.ReadAsync(key);
            Debug.Log($"✓ Raw encrypted data length: {encryptedRawData.Length} bytes");
            Debug.Log($"✓ Data is actually encrypted: {!testData.SequenceEqual(encryptedRawData)}");

            // Test with different password (should fail)
            var wrongPasswordStorage = new EncryptedStorage(baseStorage, "wrong-password");
            try
            {
                await wrongPasswordStorage.ReadAsync(key);
                Debug.Log("✗ ERROR: Should have failed with wrong password!");
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Debug.Log("✓ Correctly failed to decrypt with wrong password");
            }

            // Test with stream API
            var streamKey = "stream-encrypted.txt";
            var streamData = Encoding.UTF8.GetBytes("ストリームで暗号化されたデータ");

            using (var inputStream = new MemoryStream(streamData))
            {
                await encryptedStorage.WriteAsync(streamKey, inputStream);
            }

            Debug.Log("✓ Data written via stream to encrypted storage");

            using (var outputStream = await encryptedStorage.ReadToStreamAsync(streamKey))
            using (var memoryStream = new MemoryStream())
            {
                await outputStream.CopyToAsync(memoryStream);
                var readStreamData = memoryStream.ToArray();
                var readStreamText = Encoding.UTF8.GetString(readStreamData);
                Debug.Log($"✓ Read data via stream: {readStreamText}");
            }

            // List all files
            var allKeys = await encryptedStorage.ListAll();
            Debug.Log($"✓ Total encrypted files: {allKeys.Length}");
            foreach (var fileKey in allKeys)
            {
                Debug.Log($"  - {fileKey}");
            }

            // Test with byte array key/IV constructor
            var key32 = new byte[32];
            var iv16 = new byte[16];
            // Initialize with some values for demo
            for (int i = 0; i < 32; i++) key32[i] = (byte)(i + 1);
            for (int i = 0; i < 16; i++) iv16[i] = (byte)(i + 100);

            var customEncryptedStorage = new EncryptedStorage(new MemoryStorage(), key32, iv16);
            await customEncryptedStorage.WriteAsync("custom-key.txt", testData);
            var customDecrypted = await customEncryptedStorage.ReadAsync("custom-key.txt");

            Debug.Log($"✓ Custom key/IV encryption works: {testData.SequenceEqual(customDecrypted)}");
        }
    }
}