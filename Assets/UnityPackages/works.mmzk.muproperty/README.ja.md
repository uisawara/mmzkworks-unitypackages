[English](README.md) | 日本語

# muProperty

Unity 向けの入れ子キー・バリュー木です。葉は `bool`, `int`, `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `string`。入れ子オブジェクトが木になります。

パスは `/` 区切り（`render/quality`）。マージは左結合で、左を台に右が上書きします。

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muproperty
```

アセンブリは auto-referenced ではありません。使う側の asmdef に `works.mmzk.muproperty` を追加してください。

## 使い方

```csharp
using Mmzkworks.muProperty;
using UnityEngine;

var tree = new PropertyTree();
tree.Set("render/quality", 2);
tree.Set("render/tint", Color.red);
tree.Set("name", "hero");

int quality = tree.GetInt("render/quality");
```

入力を変更せずにマージします。

```csharp
var merged = PropertyTreeMerger.Merge(defaults, overlay);
var also = defaults.Merge(overlay, cli);
```
