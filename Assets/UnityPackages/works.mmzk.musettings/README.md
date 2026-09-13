English | [日本語](README.ja.md)

# muSettings

Load a [muProperty](../works.mmzk.muproperty/README.md) tree from command-line arguments, environment variables, and JSON. JSON can also be written back. Multiple sources merge left-to-right (later values overwrite).

Depends on **muProperty**. Install both packages.

Unity 2022.3 or later. MIT License.

## Installation

Add both Git URLs in Unity Package Manager:

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muproperty
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.musettings
```

The assemblies are not auto-referenced. Add `works.mmzk.muproperty` and `works.mmzk.musettings` to your asmdef.

## Usage

```csharp
using Mmzkworks.muSettings;

var tree = PropertyTreeLoader.Load(
    new JsonPropertySource("settings.json"),
    new EnvironmentVariablePropertySource("APP_"),
    new CommandLinePropertySource());
int quality = tree.GetInt("render/quality");
```

Paths use `/`. Typical cascade: JSON defaults, then environment, then CLI.

### Command line

`--render/quality=2` or `--render/quality 2`. Unity flags such as `-batchmode` are ignored. Keys that contain `/` may use a single dash: `-audio/volume 0.5`. A bare `--debug` becomes `true`.

### Environment variables

With a prefix (`APP_`), matching names are imported after the prefix is stripped (`APP_render/quality=2` → `render/quality`). Without a prefix, only names that contain `/` are imported, so `PATH` is not pulled in.

Unix shells treat `/` as a path character. Set those variables with quoting, for example `env 'RENDER/QUALITY=2' ./app`.

### JSON

Nested objects become tree nodes. Numbers without `.` / `e` are `int`; others are `float`. Arrays of 2/3/4 numbers are `Vector2/3/4`. Colors are `#RGB`, `#RRGGBB`, `#RRGGBBAA`, or `{ "r", "g", "b", "a" }`. Vectors may also be `{ "x", "y", ... }`. Writing uses nested objects, vector arrays, and `#RRGGBBAA` for colors.

```csharp
var io = new JsonPropertySource("settings.json");
io.Save(tree);
```

`JsonPropertySource.Parse` / `ToJson` work on strings.
