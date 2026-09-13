# muStorageSharp

キーとバイト列の保存を、ファイル / メモリ / キャッシュ / 暗号化などで組み合わせられるライブラリです。非同期 API には UniTask が必要です。

Unity 2022.3 以降。MIT License。

[日本語の詳細](./README-ja.md) / [overview](./Documents/overview.md)

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mustoragesharp
```

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

`FileStorage` / `MemoryStorage` / `CachedStorage` / `EncryptedStorage` / `StorageRouter` を組み合わせて使います。設計の背景は [overview](./Documents/overview.md) を参照してください。
