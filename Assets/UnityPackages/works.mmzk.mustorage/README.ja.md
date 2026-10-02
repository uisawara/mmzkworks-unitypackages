[English](README.md) | 日本語

# muStorage

Unity 向けの、組み合わせて使うバイナリストレージです。API は非同期(UniTask)です。

どのストレージも `IStorage` インターフェース経由で、キーを指定して `byte[]` / `Stream` を読み書きします。ストレージ同士はラップして組み合わせられるので、「ファイルの手前にメモリキャッシュを置く」「暗号化したファイルストレージ」といった構成を小さな部品から組み立てられます。ディレクトリを ZIP アーカイブ(パック)として保存することもできます。

Unity 2022.3 以降。MIT License。

muStorage は [StorageSharp](https://github.com/uisawara/storageSharp)(MIT License)を Unity 向けに再編した派生物です。詳しくは [LICENSE](LICENSE) を参照してください。

設計の背景やキャッシュ構成の例は [overview](Documentation~/overview.md) を参照してください。

## インストール

muStorage は [UniTask](https://github.com/Cysharp/UniTask) に依存しています。先に UniTask をインストールしてください。

そのうえで、Unity の Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mustorage
```

サンプルは Package Manager の Samples タブからインポートできます。

## 使い方

```csharp
using Mmzkworks.muStorage;

var storage = new FileStorage(Path.Combine(Application.persistentDataPath, "Storage"));

await storage.WriteAsync("dir/data.bin", data);
var loaded = await storage.ReadAsync("dir/data.bin");
var keys = await storage.ListAll();
```

空のデータ(`null` または長さ 0 の配列)を書き込むと、そのキーは削除されます。

## ストレージ (IStorage)

| クラス | 説明 |
| --- | --- |
| `FileStorage` | キーごとにディレクトリ下のファイルとして保存。キー内の `/` はサブディレクトリになる |
| `MemoryStorage` | メモリ上に保存。アプリ終了で消える |
| `CachedStorage` | まずキャッシュから読み、なければオリジンから読む |
| `EncryptedStorage` | 任意の `IStorage` をラップし、AES-256 で暗号化する |
| `StorageRouter` | 条件に応じてキーごとに保存先を振り分ける |

### CachedStorage

```csharp
var storage = new CachedStorage(
    cache: new MemoryStorage(),
    origin: new FileStorage(path));
```

書き込みはオリジンとキャッシュの両方に行われます。

### EncryptedStorage

```csharp
var storage = new EncryptedStorage(new FileStorage(path), "your-secure-password");

// 32 バイトの鍵と 16 バイトの IV を渡すこともできる
var storage2 = new EncryptedStorage(new FileStorage(path), key, iv);
```

- AES-256、CBC モード、PKCS7 パディング。
- IV はインスタンスごとに固定です。パスワードや鍵をソースコードに直書きしないでください。

### StorageRouter

```csharp
var router = new StorageRouter(new[]
{
    // "mem://" で始まるキーは、プレフィックスを取り除いてメモリへ
    new StorageRouter.Branch(
        key => key.StartsWith("mem://"),
        key => key.Substring("mem://".Length),
        new MemoryStorage()),
},
new FileStorage(path)); // デフォルトの保存先

await router.WriteAsync("mem://a.txt", data); // -> MemoryStorage、キーは "a.txt"
await router.WriteAsync("b.txt", data);       // -> FileStorage
```

## パック (IPacks)

`ZippedPacks` はディレクトリを ZIP に圧縮して、任意の `IStorage` に保存します。`Load` で一時ディレクトリに展開します。

```csharp
var packs = new ZippedPacks(
    new ZippedPacks.Settings(Path.Combine(Application.temporaryCachePath, "Packs")),
    new FileStorage(Path.Combine(Application.persistentDataPath, "Packs")));

var scheme = await packs.Add(directoryPath);
var extractedPath = await packs.Load(scheme);
// extractedPath 以下のファイルを使う ...
await packs.Unload(scheme);  // 展開したファイルを削除
await packs.Delete(scheme);  // ストレージからアーカイブを削除
```

`System.IO.Compression` と `JsonUtility` を使っているので、追加のライブラリは不要です。

## IStorage API

| メソッド | 説明 |
| --- | --- |
| `ListAll()` | 全キーを列挙 |
| `ReadAsync(key)` | `byte[]` で読み込む |
| `WriteAsync(key, byte[])` | 書き込む。空データならキーを削除 |
| `ReadToStreamAsync(key)` | `Stream` で読み込む |
| `WriteAsync(key, Stream)` | `Stream` から書き込む |

## 注意

- 存在しないキーを読むと例外になります(`FileNotFoundException` / `KeyNotFoundException`)。
- `MemoryStorage` と `CachedStorage` はデータをメモリに保持します。大きなデータを扱うときはメモリ使用量に注意してください。
