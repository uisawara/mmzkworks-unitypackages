English | [日本語](README.ja.md)

# muProject

An Editor extension that makes the Project window easier to read.

Folder icons change based on what the folder contains. For example, folders with a `package.json` (UPM packages) show a package icon.

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muproject
```

## Folder icons

| Folder | Icon |
| --- | --- |
| Matches a path pattern in a `FolderSettings` asset | Icon set for that pattern |
| Contains `package.json` (UPM package root) | Package icon |

Toggle it with `Tools > muProject > Custom Folder Icons`.

Icons are drawn before other Project window extensions, so marks such as muValidation's ✗ stay on top.

### Icons by path (FolderSettings)

Create a `FolderSettings` asset in the folder you want it to cover, with `Create > muProject > Folder Settings`, then add rules to `Folder Icons`.

| Field | Description |
| --- | --- |
| Path Pattern | Regular expression matched against the folder's project path (e.g. `Assets/Scenes`, `Packages/com.example.foo`) |
| Icon | Texture to show for matching folders |

Examples:

| Path Pattern | Matches |
| --- | --- |
| `^Assets/Scenes$` | `Assets/Scenes` only |
| `/Editor$` | Every folder named `Editor` |
| `^Assets/Art(/.*)?$` | `Assets/Art` and everything under it |

- Rules are checked from top to bottom. The first matching rule wins.
- A `FolderSettings` asset applies only to the folder it is in and the folders below it. Patterns are still matched against the full project path.
- You can have several `FolderSettings` assets. When more than one applies, the one in the deepest folder is used first.
- Path rules take priority over the package icon.
- An invalid pattern is skipped with a warning in the Console.

### Adding your own icons

Implement `IFolderIconProvider` in an Editor assembly. It is found automatically.

```csharp
using System.IO;
using Mmzkworks.muProject.Editor;
using UnityEditor;
using UnityEngine;

public class SceneFolderIconProvider : IFolderIconProvider
{
    public int Order => 100;

    public Texture GetIcon(string folderPath)
    {
        return Directory.GetFiles(folderPath, "*.unity").Length > 0
            ? EditorGUIUtility.IconContent("SceneAsset Icon").image
            : null;
    }
}
```

- Providers are asked in ascending `Order`. The first non-null icon is used. The built-in package icon uses `Order` 0, and `FolderSettings` rules use `Order` -100.
- Return null to keep the default folder icon.
- Results are cached and cleared when any asset changes, so `GetIcon` may read the file system.
