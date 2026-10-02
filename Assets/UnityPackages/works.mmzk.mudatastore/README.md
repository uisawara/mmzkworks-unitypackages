English | [日本語](README.ja.md)

# muDatastore

A small key-value data store for Unity, with an async (UniTask) API.

Save and load values through the `IDataStore` interface. You can swap the backend between PlayerPrefs and a local file without changing the calling code.

Unity 2022.3 or later. MIT License.

## Installation

muDatastore depends on [UniTask](https://github.com/Cysharp/UniTask). Install UniTask first.

Then add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mudatastore
```

## Usage

```csharp
using Mmzkworks.muDatastore;

using IDataStore store = new PlayerPrefsDataStore("MyGame_");

await store.SetAsync("score", 100);
await store.SetAsync("playerName", "mmzk");

var score = await store.GetIntAsync("score");
var name = await store.GetStringAsync("playerName");
```

`IDataStore` is `IDisposable`. `Dispose` flushes pending changes and closes the store, so wrap it in `using` and you won't forget to flush. Calling any method after `Dispose` throws `ObjectDisposedException`.

## Data stores

### PlayerPrefsDataStore

Stores values in `PlayerPrefs`. Keys are saved with the prefix you pass to the constructor, so multiple stores can live side by side.

Supports `int` / `float` / `bool` / `string`. Saved keys are tracked, so `GetListAsync` and `DeleteAllAsync` only touch keys under that prefix. Call `FlushAsync` to write to disk (`PlayerPrefs.Save`).

### LocalFileDataStore

Stores values in a single JSON file at the given path.

```csharp
var path = Path.Combine(Application.persistentDataPath, "save.json");
using IDataStore store = new LocalFileDataStore(path);
```

Currently only `string` values are supported. The `int` / `float` / `bool` methods throw `NotImplementedException`. To save objects, use `JsonDataStoreAdapter` below.

## JsonDataStoreAdapter

Saves and loads any serializable object as JSON, using `JsonUtility`. Works with any `IDataStore`.

```csharp
[Serializable]
public class SaveData
{
    public int score;
    public string name;
}

var adapter = new JsonDataStoreAdapter(store);
await adapter.Save("save", new SaveData { score = 100, name = "mmzk" });
var data = await adapter.Load<SaveData>("save");
```

## IDataStore API

| Method | Description |
| --- | --- |
| `SetAsync(key, value)` | Save a value (`int` / `float` / `bool` / `string`) |
| `GetIntAsync` / `GetFloatAsync` / `GetBoolAsync` / `GetStringAsync` | Load a value |
| `ExistsAsync(key)` | Whether the key exists |
| `GetListAsync()` | List saved keys |
| `DeleteAsync(key)` | Delete a key |
| `DeleteAllAsync()` | Delete all keys in this store |
| `FlushAsync()` | Write pending changes |
| `Dispose()` | Flush and close the store |
