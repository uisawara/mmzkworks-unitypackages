[English](README.md) | 日本語

# muSettings

[muProperty](../works.mmzk.muproperty/README.ja.md) の木を、コマンドライン引数・環境変数・JSON から読み込みます。JSON は書き込みもできます。複数ソースは左から順にマージし、後から来た値が上書きします。

**muProperty に依存**します。両方インストールしてください。

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager に、次の Git URL を両方追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muproperty
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.musettings
```

アセンブリは auto-referenced ではありません。使う側の asmdef に `works.mmzk.muproperty` と `works.mmzk.musettings` を追加してください。

## 使い方

```csharp
using Mmzkworks.muSettings;

var tree = PropertyTreeLoader.Load(
    new JsonPropertySource("settings.json"),
    new EnvironmentVariablePropertySource("APP_"),
    new CommandLinePropertySource());
int quality = tree.GetInt("render/quality");
```

パスは `/` 区切りです。典型的なカスケードは JSON → 環境変数 → CLI です。

### コマンドライン

`--render/quality=2` または `--render/quality 2`。`-batchmode` のような Unity 組み込みフラグは取り込みません。キーに `/` が含まれる場合は単一ダッシュも可（`-audio/volume 0.5`）。値のない `--debug` は `true` になります。

### 環境変数

プレフィックス（`APP_`）があるときは、一致した名前からプレフィックスを除いて取り込みます（`APP_render/quality=2` → `render/quality`）。プレフィックスなしのときは、名前に `/` を含む変数だけを取り込み、`PATH` などは入りません。

Unix のシェルでは `/` がパス文字なので、`env 'RENDER/QUALITY=2' ./app` のようにクォートして設定します。

### JSON

ネストしたオブジェクトが木のノードになります。`.` / `e` のない数値は `int`、それ以外は `float`。長さ 2/3/4 の数値配列は `Vector2/3/4`。Color は `#RGB` / `#RRGGBB` / `#RRGGBBAA`、または `{ "r", "g", "b", "a" }`。Vector は `{ "x", "y", ... }` でも読めます。書き出しはネストオブジェクト、Vector は配列、Color は `#RRGGBBAA` です。

```csharp
var io = new JsonPropertySource("settings.json");
io.Save(tree);
```

文字列向けに `JsonPropertySource.Parse` / `ToJson` もあります。
