English | [日本語](README.ja.md)

# muUnityExtensions

Extension methods for Unity types.

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muunityextensions
```

## Contents

All APIs are in the `Mmzkworks.muUnityExtensions` namespace.

| Class | Description |
| --- | --- |
| `GameObjectBuilder` / `GameObjectBuilderExtensions` | Build GameObject hierarchies by method chaining |
| `Vector2Extensions` | `ToVector3XY` / `ToVector3XZ` / `ToVector3YZ`, `ToArray` |
| `Vector3Extensions` | `WithX` / `WithY` / `WithZ`, `XY` / `XZ` / `YZ`, `ToArray` |
| `RectExtensions` | `ToUVRect` : pixel rect to UV rect |
| `Texture2DExtensions` | `CreateResized` / `CreateCropped` / `CreateReadable` |
| `RenderTextureExtensions` | `CopyFrom` / `ToTexture2D` |

## Usage

```csharp
using Mmzkworks.muUnityExtensions;
using UnityEngine;

// GameObjectBuilder converts implicitly to GameObject (or use .Target)
GameObject model = new GameObject("Model")
    .WithChildren(
        new GameObject("AnimatorA")
            .WithComponent<Animator>(animator => animator.applyRootMotion = true)
            .WithChild(new GameObject("root")),
        new GameObject("AnimatorB")
            .WithComponent<Animator>()
            .WithChild(new GameObject("root")));
```

`Configure<T>` throws `MissingComponentException` when the component is not attached.
