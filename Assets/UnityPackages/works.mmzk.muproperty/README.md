English | [日本語](README.ja.md)

# muProperty

A nested key-value tree for Unity. Leaves can be `bool`, `int`, `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, or `string`. Nested objects form the tree.

Paths use `/` (`render/quality`). Trees merge left-to-right: the left tree is the base, the right tree overwrites.

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muproperty
```

The assembly is not auto-referenced. Add `works.mmzk.muproperty` to your asmdef.

## Usage

```csharp
using Mmzkworks.muProperty;
using UnityEngine;

var tree = new PropertyTree();
tree.Set("render/quality", 2);
tree.Set("render/tint", Color.red);
tree.Set("name", "hero");

int quality = tree.GetInt("render/quality");
```

Merge without mutating the inputs:

```csharp
var merged = PropertyTreeMerger.Merge(defaults, overlay);
var also = defaults.Merge(overlay, cli);
```
