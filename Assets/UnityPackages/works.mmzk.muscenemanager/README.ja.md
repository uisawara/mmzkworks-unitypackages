[English](README.md) | 日本語

# muSceneManager

UniTask を使った、Unity 用の小さなシーン管理です。メインシーン1つと、名前付きのスロットに置く追加読み込みのサブシーンを管理します。

- 読み込み・切り替え・破棄の要求はキューに積まれ、呼び出した順に1つずつ処理されます
- サブシーンは自分で定義した enum のスロットに置きます。スロットを切り替えると、そこにあったシーンは破棄されます
- シーンは `SceneId` で指定します。Inspector ではシーンアセットを選んで設定できます
- シーンの読み込みは `ISceneLoader` 経由なので、テストではモックに差し替えられます

Unity 2022.3 以降。MIT License。

## インストール

[UniTask](https://github.com/Cysharp/UniTask) と [muLogger](../works.mmzk.mulogger/README.ja.md) に依存しています。先にこれらを入れてください。

そのうえで、Unity の Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muscenemanager
```

サンプルは Package Manager の Samples タブからインポートできます。

## 使い方

```csharp
using Mmzkworks.muSceneManager;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    private enum SceneSlots
    {
        Level,
        Ui
    }

    [SerializeField] private SceneId _titleScene;
    [SerializeField] private SceneId _levelScene;
    [SerializeField] private SceneId _hudScene;

    private readonly SceneManager<SceneSlots> _sceneManager = new();

    private async void Start()
    {
        await _sceneManager.ChangeMainScene(_titleScene);
        await _sceneManager.ChangeSubScene(SceneSlots.Level, _levelScene);
        await _sceneManager.ChangeSubScene(SceneSlots.Ui, _hudScene);

        // Ui スロットのシーンだけを破棄する
        await _sceneManager.UnloadSubScene(SceneSlots.Ui);
    }
}
```

`SceneId` はコードから `new SceneId("Title")` で作ることもできます。

### メインシーンとサブシーン

| メソッド | 動作 |
| --- | --- |
| `ChangeMainScene(id)` | `LoadSceneMode.Single` で読み込む。サブシーンを含め、ほかのシーンはすべて破棄される |
| `ChangeSubScene(slot, id)` | スロットにシーンがあれば破棄してから、新しいシーンを `LoadSceneMode.Additive` で読み込む |
| `UnloadSubScene(slot)` | スロットのシーンを破棄する。スロットが空なら何もしない |

- すでに読み込まれているシーン（メインシーン、または同じスロットのシーン）を指定した要求は無視されます。この判定は要求を処理する時点で行うので、キューにある前の要求の結果が反映されます。
- 読み込み・破棄に失敗すると、返されたタスクは例外で失敗し、キューの次の要求に進みます。シーンを記録するのは読み込みに成功してからなので、管理している状態と実際に読み込まれているシーンはずれません。
- 無効な `SceneId`（`default`）を渡すと `ArgumentException` で失敗します。

### 状態

| プロパティ | 内容 |
| --- | --- |
| `Status` | `Idle`、`Loading`、`Unloading` のいずれか |
| `CurrentMainSceneId` | 現在のメインシーン。最初の `ChangeMainScene` の前は `default` |
| `HasMainScene` | このマネージャーでメインシーンを読み込んだかどうか |

## SceneId

`SceneId` はシーン名を持つ struct です。名前はそのまま `UnityEngine.SceneManagement.SceneManager` に渡されるので、シーンは **Build Settings** に登録しておく必要があります。

- `new SceneId(name)` は `null` や空文字だと `ArgumentException` を投げます
- `default(SceneId)` は「シーンなし」を表します（`IsValid` が `false`）
- 比較は名前で行い、大文字・小文字を区別します

Inspector では、`SceneId` のフィールドはシーンアセットの選択欄として表示されます。次のときはフィールドの下に警告が出ます。

- 保存されている名前のシーンが見つからない
- そのシーンが Build Settings で有効になっていない

保存するのは名前だけなので、シーンファイルの名前を変えると参照が外れます。そのときは Inspector に「見つからない」警告が出ます。

## テスト

コンストラクタに独自の `ISceneLoader` を渡すと、実際のシーンなしで動かせます。

```csharp
var manager = new SceneManager<SceneSlots>(new NullLogger(), new MySceneLoader());
```

`ISceneLoader` のメソッドは `LoadSceneAsync(string sceneName, LoadSceneMode mode)` と `UnloadSceneAsync(string sceneName)` の2つです。既定の実装は `UnitySceneLoader` です。
