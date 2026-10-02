using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mmzkworks.muStorage.Samples
{
    /// <summary>
    /// muStorage の基本的な使い方をまとめたサンプル。
    /// 空の GameObject に追加して Play すると、Console に結果が出力されます。
    /// </summary>
    public class BasicSample : MonoBehaviour
    {
        private string _rootDirectory;

        private async UniTaskVoid Start()
        {
            _rootDirectory = Path.Combine(Application.temporaryCachePath, "muStorageSample");

            await BasicStorageExample();
            await CachedStorageExample();
            await EncryptedStorageExample();
            await StorageRouterExample();
            await ZippedPacksExample();

            Debug.Log("[muStorage] All samples completed.");
        }

        private async UniTask BasicStorageExample()
        {
            // ファイルストレージ
            var fileStorage = new FileStorage(Path.Combine(_rootDirectory, "FileStorage"));
            var data = Encoding.UTF8.GetBytes("Hello, muStorage!");
            await fileStorage.WriteAsync("test.txt", data);
            var read = await fileStorage.ReadAsync("test.txt");
            Debug.Log($"[FileStorage] {Encoding.UTF8.GetString(read)}");

            // メモリストレージ
            var memoryStorage = new MemoryStorage();
            await memoryStorage.WriteAsync("memory-test.txt", data);
            read = await memoryStorage.ReadAsync("memory-test.txt");
            Debug.Log($"[MemoryStorage] {Encoding.UTF8.GetString(read)}");
        }

        private async UniTask CachedStorageExample()
        {
            var cachedStorage = new CachedStorage(
                new MemoryStorage(),
                new FileStorage(Path.Combine(_rootDirectory, "Origin")));

            await cachedStorage.WriteAsync("cached-file.txt", Encoding.UTF8.GetBytes("Cached data example"));
            var read = await cachedStorage.ReadAsync("cached-file.txt");
            Debug.Log($"[CachedStorage] {Encoding.UTF8.GetString(read)} (cache hit count: {cachedStorage.CacheHitCount})");
        }

        private async UniTask EncryptedStorageExample()
        {
            var baseStorage = new FileStorage(Path.Combine(_rootDirectory, "Encrypted"));
            var encryptedStorage = new EncryptedStorage(baseStorage, "my-secret-password-123");

            var data = Encoding.UTF8.GetBytes("これは暗号化されるテストデータです！");
            await encryptedStorage.WriteAsync("secret.txt", data);

            var decrypted = await encryptedStorage.ReadAsync("secret.txt");
            var raw = await baseStorage.ReadAsync("secret.txt");
            Debug.Log($"[EncryptedStorage] {Encoding.UTF8.GetString(decrypted)} (stored encrypted: {!data.SequenceEqual(raw)})");

            // 異なるパスワードでは復号できない
            try
            {
                await new EncryptedStorage(baseStorage, "wrong-password").ReadAsync("secret.txt");
                Debug.LogError("[EncryptedStorage] Should have failed with wrong password");
            }
            catch (CryptographicException)
            {
                Debug.Log("[EncryptedStorage] Failed to decrypt with wrong password, as expected");
            }
        }

        private async UniTask StorageRouterExample()
        {
            var memoryStorage = new MemoryStorage();
            var router = new StorageRouter(new[]
                {
                    // "mem://" で始まるキーはプレフィックスを取り除いてメモリへ
                    new StorageRouter.Branch(
                        key => key.StartsWith("mem://"),
                        key => key.Substring("mem://".Length),
                        memoryStorage)
                },
                new FileStorage(Path.Combine(_rootDirectory, "Default")));

            await router.WriteAsync("mem://a.txt", Encoding.UTF8.GetBytes("in memory"));
            await router.WriteAsync("b.txt", Encoding.UTF8.GetBytes("in file"));
            Debug.Log($"[StorageRouter] memory keys: {string.Join(", ", await memoryStorage.ListAll())}");
        }

        private async UniTask ZippedPacksExample()
        {
            // パック対象のディレクトリを用意
            var sourceDir = Path.Combine(_rootDirectory, "PackSource");
            Directory.CreateDirectory(Path.Combine(sourceDir, "subdir"));
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "This is file 1");
            File.WriteAllText(Path.Combine(sourceDir, "subdir", "file2.txt"), "This is file 2 in subdir");

            var packs = new ZippedPacks(
                new ZippedPacks.Settings(Path.Combine(_rootDirectory, "PacksTemp")),
                new FileStorage(Path.Combine(_rootDirectory, "Packs")));

            var scheme = await packs.Add(sourceDir);
            var loadedPath = await packs.Load(scheme);
            Debug.Log($"[ZippedPacks] {File.ReadAllText(Path.Combine(loadedPath, "subdir", "file2.txt"))}");

            await packs.Unload(scheme);
            await packs.Delete(scheme);
        }
    }
}
