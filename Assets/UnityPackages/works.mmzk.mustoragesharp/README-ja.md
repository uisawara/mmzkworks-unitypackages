# muStorageSharp

キーとバイト列の保存を、ファイル / メモリ / キャッシュ / 暗号化などで組み合わせられるライブラリです。非同期 API には UniTask が必要です。

Unity 2022.3 以降。MIT License。

[README](./README.md) / [overview](./Documents/overview.md)

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mustoragesharp
```

## 機能

- **FileStorage**: ファイルシステムベースのストレージ
- **MemoryStorage**: メモリベースのストレージ
- **CachedStorage**: キャッシュ機能付きストレージ
- **EncryptedStorage**: 任意の IStorage をラップする AES-256 暗号化ストレージ
- **StorageRouter**: キーのパターンに基づいて異なるストレージへ振り分ける

## 使い方

```csharp
using StorageSharp.Storages;

var fileStorage = new FileStorage("StorageDirectory");
var bytes = System.Text.Encoding.UTF8.GetBytes("hello");
await fileStorage.WriteAsync("key.txt", bytes);
var loaded = await fileStorage.ReadAsync("key.txt");

var cached = new CachedStorage(
    cache: new MemoryStorage(),
    origin: new FileStorage("OriginStorage"));

var encrypted = new EncryptedStorage(fileStorage, "your-secure-password");
```

設計の背景やキャッシュ構成の例は [overview](./Documents/overview.md) を参照してください。
