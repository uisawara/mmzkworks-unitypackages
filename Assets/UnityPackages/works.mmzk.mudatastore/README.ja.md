[English](README.md) | 日本語

# muDatastore

Unity 向けの、シンプルな Key-Value データストアです。API は非同期(UniTask)です。

`IDataStore` インターフェース経由で値を保存・読み込みします。呼び出し側のコードを変えずに、保存先を PlayerPrefs とローカルファイルで切り替えられます。

Unity 2022.3 以降。MIT License。

## インストール

muDatastore は [UniTask](https://github.com/Cysharp/UniTask) に依存しています。先に UniTask をインストールしてください。

そのうえで、Unity の Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mudatastore
```

## 使い方

```csharp
using Mmzkworks.muDatastore;

using IDataStore store = new PlayerPrefsDataStore("MyGame_");

await store.SetAsync("score", 100);
await store.SetAsync("playerName", "mmzk");

var score = await store.GetIntAsync("score");
var name = await store.GetStringAsync("playerName");
```

`IDataStore` は `IDisposable` です。`Dispose` で未保存の変更を書き込み、ストアを閉じます。`using` で囲めば Flush 忘れを防げます。`Dispose` 後にメソッドを呼ぶと `ObjectDisposedException` になります。

## データストア

### PlayerPrefsDataStore

`PlayerPrefs` に保存します。キーはコンストラクタで渡したプレフィックス付きで保存されるので、複数のストアを並べて使えます。

`int` / `float` / `bool` / `string` に対応しています。保存したキーは一覧として管理されるので、`GetListAsync` や `DeleteAllAsync` はそのプレフィックスのキーだけを対象にします。ディスクへの書き込みは `FlushAsync`(`PlayerPrefs.Save`)で行います。

### LocalFileDataStore

指定したパスの JSON ファイル 1 つに保存します。

```csharp
var path = Path.Combine(Application.persistentDataPath, "save.json");
using IDataStore store = new LocalFileDataStore(path);
```

現状は `string` のみ対応しています。`int` / `float` / `bool` のメソッドは `NotImplementedException` を投げます。オブジェクトを保存したいときは、下の `JsonDataStoreAdapter` を使ってください。

## JsonDataStoreAdapter

シリアライズ可能なオブジェクトを、`JsonUtility` で JSON にして保存・読み込みします。どの `IDataStore` とも組み合わせられます。

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

| メソッド | 説明 |
| --- | --- |
| `SetAsync(key, value)` | 値を保存(`int` / `float` / `bool` / `string`) |
| `GetIntAsync` / `GetFloatAsync` / `GetBoolAsync` / `GetStringAsync` | 値を読み込み |
| `ExistsAsync(key)` | キーが存在するか |
| `GetListAsync()` | 保存済みキーの一覧 |
| `DeleteAsync(key)` | キーを削除 |
| `DeleteAllAsync()` | このストアのキーをすべて削除 |
| `FlushAsync()` | 変更を書き込み |
| `Dispose()` | 変更を書き込み、ストアを閉じる |
