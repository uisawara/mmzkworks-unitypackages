English | [日本語](README.ja.md)

# muStorage

A composable binary storage library for Unity, with an async (UniTask) API.

Every storage reads and writes `byte[]` / `Stream` by key through the `IStorage` interface. Storages can wrap each other, so you can build things like "memory cache in front of files" or "encrypted file storage" by combining small pieces. Directories can also be stored as ZIP archives (packs).

Unity 2022.3 or later. MIT License.

muStorage is a derivative of [StorageSharp](https://github.com/uisawara/storageSharp) (MIT License), reorganized for Unity. See [LICENSE](LICENSE).

For the design background and cache configuration examples, see the [overview](Documentation~/overview.md) (Japanese).

## Installation

muStorage depends on [UniTask](https://github.com/Cysharp/UniTask). Install UniTask first.

Then add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mustorage
```

A sample can be imported from the Samples tab in Package Manager.

## Usage

```csharp
using Mmzkworks.muStorage;

var storage = new FileStorage(Path.Combine(Application.persistentDataPath, "Storage"));

await storage.WriteAsync("dir/data.bin", data);
var loaded = await storage.ReadAsync("dir/data.bin");
var keys = await storage.ListAll();
```

Writing empty data (`null` or a zero-length array) deletes the key.

## Storages (IStorage)

| Class | Description |
| --- | --- |
| `FileStorage` | Stores each key as a file under a directory. `/` in keys becomes a subdirectory |
| `MemoryStorage` | Stores data in memory. Lost when the app quits |
| `CachedStorage` | Reads from a cache storage first, and falls back to the origin storage on a miss |
| `EncryptedStorage` | Wraps any `IStorage` and encrypts data with AES-256 |
| `StorageRouter` | Routes each key to a different storage by a predicate |

### CachedStorage

```csharp
var storage = new CachedStorage(
    cache: new MemoryStorage(),
    origin: new FileStorage(path));
```

Writes go to both the origin and the cache.

### EncryptedStorage

```csharp
var storage = new EncryptedStorage(new FileStorage(path), "your-secure-password");

// Or pass a 32-byte key and a 16-byte IV
var storage2 = new EncryptedStorage(new FileStorage(path), key, iv);
```

- AES-256, CBC mode, PKCS7 padding.
- The IV is fixed per instance. Don't hardcode passwords or keys in your source code.

### StorageRouter

```csharp
var router = new StorageRouter(new[]
{
    // Keys starting with "mem://" go to memory, with the prefix removed
    new StorageRouter.Branch(
        key => key.StartsWith("mem://"),
        key => key.Substring("mem://".Length),
        new MemoryStorage()),
},
new FileStorage(path)); // default storage

await router.WriteAsync("mem://a.txt", data); // -> MemoryStorage, key "a.txt"
await router.WriteAsync("b.txt", data);       // -> FileStorage
```

## Packs (IPacks)

`ZippedPacks` compresses a directory into a ZIP and stores it in any `IStorage`. `Load` extracts it to a temporary directory.

```csharp
var packs = new ZippedPacks(
    new ZippedPacks.Settings(Path.Combine(Application.temporaryCachePath, "Packs")),
    new FileStorage(Path.Combine(Application.persistentDataPath, "Packs")));

var scheme = await packs.Add(directoryPath);
var extractedPath = await packs.Load(scheme);
// use the files under extractedPath ...
await packs.Unload(scheme);  // removes the extracted files
await packs.Delete(scheme);  // removes the archive from the storage
```

Uses `System.IO.Compression` and `JsonUtility`, so no extra libraries are needed.

## IStorage API

| Method | Description |
| --- | --- |
| `ListAll()` | List all keys |
| `ReadAsync(key)` | Read data as `byte[]` |
| `WriteAsync(key, byte[])` | Write data. Empty data deletes the key |
| `ReadToStreamAsync(key)` | Read data as a `Stream` |
| `WriteAsync(key, Stream)` | Write data from a `Stream` |

## Notes

- Reading a missing key throws (`FileNotFoundException` / `KeyNotFoundException`).
- `MemoryStorage` and `CachedStorage` keep data in memory. Watch memory usage with large data.
