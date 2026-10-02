[English](README.md) | 日本語

# muUnityExtensions

Unity の型に対する拡張メソッド集です（muMisc から分離）。

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muunityextensions
```

## 内容

API はすべて `Mmzkworks.muUnityExtensions` 名前空間にあります。

| クラス | 内容 |
| --- | --- |
| `GameObjectBuilder` / `GameObjectBuilderExtensions` | メソッドチェーンで GameObject の階層を組み立てる |
| `Vector2Extensions` | `ToVector3XY` / `ToVector3XZ` / `ToVector3YZ`、`ToArray` |
| `Vector3Extensions` | `WithX` / `WithY` / `WithZ`、`XY` / `XZ` / `YZ`、`ToArray` |
| `RectExtensions` | `ToUVRect` : ピクセル座標の Rect を UV に変換 |
| `Texture2DExtensions` | `CreateResized` / `CreateCropped` / `CreateReadable` |
| `RenderTextureExtensions` | `CopyFrom` / `ToTexture2D` |

## 使い方

```csharp
using Mmzkworks.muUnityExtensions;
using UnityEngine;

// GameObjectBuilder は GameObject に暗黙変換されます（.Target でも取得可）
GameObject model = new GameObject("Model")
    .WithChildren(
        new GameObject("AnimatorA")
            .WithComponent<Animator>(animator => animator.applyRootMotion = true)
            .WithChild(new GameObject("root")),
        new GameObject("AnimatorB")
            .WithComponent<Animator>()
            .WithChild(new GameObject("root")));
```

`Configure<T>` は、対象コンポーネントが付いていない場合 `MissingComponentException` を投げます。
