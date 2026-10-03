English | [日本語](README.ja.md)

# muSceneManager

A small scene manager for Unity built on UniTask. It manages one main scene and additive sub scenes in named slots.

- Load, replace, and unload requests are queued and processed one at a time, in the order they were called
- Sub scenes are put in slots (an enum you define). Changing a slot unloads the scene that was in it
- Scenes are identified by `SceneId`, which can be set from the Inspector by picking a scene asset
- Scene loading goes through `ISceneLoader`, so you can replace it with a mock in tests

Unity 2022.3 or later. MIT License.

## Installation

This package depends on [UniTask](https://github.com/Cysharp/UniTask) and [muLogger](../works.mmzk.mulogger/README.md). Install them first.

Then add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muscenemanager
```

A sample can be imported from the Samples tab in Package Manager.

## Usage

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

        // Unload only the scene in the Ui slot
        await _sceneManager.UnloadSubScene(SceneSlots.Ui);
    }
}
```

You can also create a `SceneId` from code: `new SceneId("Title")`.

### Main scene and sub scenes

| Method | Behavior |
| --- | --- |
| `ChangeMainScene(id)` | Loads the scene with `LoadSceneMode.Single`. All other scenes, including sub scenes, are unloaded |
| `ChangeSubScene(slot, id)` | Unloads the scene in the slot (if any), then loads the new one with `LoadSceneMode.Additive` |
| `UnloadSubScene(slot)` | Unloads the scene in the slot. Does nothing if the slot is empty |

- A request for the scene that is already loaded (main scene, or the same slot) is ignored. This check is done when the request is processed, so it sees the result of earlier requests in the queue.
- If loading or unloading fails, the returned task fails with an exception and the next request in the queue runs. The manager only records a scene after it has loaded, so its state stays in line with the scenes actually loaded.
- Passing an invalid `SceneId` (`default`) fails with `ArgumentException`.

### State

| Property | Description |
| --- | --- |
| `Status` | `Idle`, `Loading`, or `Unloading` |
| `CurrentMainSceneId` | The current main scene. `default` before the first `ChangeMainScene` |
| `HasMainScene` | Whether a main scene has been loaded by this manager |

## SceneId

`SceneId` is a struct that holds a scene name. It is passed to `UnityEngine.SceneManagement.SceneManager` as is, so the scene must be in **Build Settings**.

- `new SceneId(name)` throws `ArgumentException` for `null` or an empty string
- `default(SceneId)` means "no scene" (`IsValid` is `false`)
- Comparison is by name and is case-sensitive

In the Inspector, a `SceneId` field is shown as a scene asset picker. A warning is shown under the field when:

- No scene with the saved name is found
- The scene is not enabled in Build Settings

Only the name is saved, so renaming the scene file breaks the reference. The Inspector shows the "not found" warning in that case.

## Testing

Pass your own `ISceneLoader` to the constructor to run without real scenes.

```csharp
var manager = new SceneManager<SceneSlots>(new NullLogger(), new MySceneLoader());
```

`ISceneLoader` has two methods: `LoadSceneAsync(string sceneName, LoadSceneMode mode)` and `UnloadSceneAsync(string sceneName)`. The default is `UnitySceneLoader`.
