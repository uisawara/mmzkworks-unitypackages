# muDataStore

キーと値の保存を、同じインターフェースで扱える小さなライブラリです。

`PlayerPrefs` とローカルファイルの実装があり、JSON でオブジェクトを読み書きするアダプタも付きます。非同期 API には UniTask が必要です。

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mudatastore
```

## 使い方

`IDataStore` の `SetAsync` / `Get*Async` / `ExistsAsync` / `DeleteAsync` で、int / float / bool / string を読み書きします。

```csharp
using Mmzkworks.muDataStore;
using UnityEngine;

IDataStore store = new PlayerPrefsDataStore("Game_");
await store.SetAsync("score", 123);
await store.SetAsync("name", "player");
await store.FlushAsync();

var score = await store.GetIntAsync("score");
var name = await store.GetStringAsync("name");
```

`LocalFileDataStore` は JSON ファイルに保存します。パスはディレクトリ付きで渡します。

```csharp
var path = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
IDataStore store = new LocalFileDataStore(path);
await store.SetAsync("volume", 0.8f);
await store.SetAsync("muted", false);
```

## JsonDataStoreAdapter

`IDataStore` の上に載せて、`JsonUtility` でオブジェクトを保存・読み込みできます。保存する型は `[Serializable]` である必要があります。

```csharp
[System.Serializable]
public class PlayerData
{
    public int score;
    public string name;
    public bool flag;
}

var adapter = new JsonDataStoreAdapter(store);
await adapter.Save("player", new PlayerData { score = 123, name = "abc", flag = true });
var loaded = await adapter.Load<PlayerData>("player");
```
