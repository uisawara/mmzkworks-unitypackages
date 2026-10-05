English | [日本語](README.ja.md)

# muValidation

An Editor extension that finds invalid assets and objects, and marks them in the Project window.

- **File names**: place `FileNameRules` assets with allowed file path patterns. Files that break them get a ✗ mark on their icon.
- **Scenes**: assign `SceneRules` assets to scene folders to check scene objects for default names (`Cube`, `GameObject`...), forbidden tags and layers, the layers allowed per tag and component, objects that are not prefab instances, and duplicate root object names.
- **Object content**: put validation attributes on ScriptableObject / Component classes and fields. Assets and prefabs with errors get a red ✗ mark, or a yellow ! mark when there are only warnings.
- **Folders**: a folder that contains assets with errors (at any depth) also gets a ✗ mark, or a ! mark if they have only warnings. Turn it off with `Tools > muValidation > Show Errors On Folders`.

Hover over the icon to see why. `Tools > muValidation > Validation` lists every problem.

![Error and warning marks in the Project window](Documentation~/img/project-marks.png)

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muvalidation
```

## Usage

1. Right-click the target folder in the Project window and select `Create > muValidation > File Name Rules`.
2. Add rules in the Inspector.
3. Files that break the rule show a ✗ mark on their icon.

The Inspector of a `FileNameRules` also lists the files that break it. Click one to ping it.

## Rule settings

| Field | Description |
| --- | --- |
| Rules | List of rules. Each has a `Path` and `Patterns` (below) |
| Rules > Path | Folder relative to the `FileNameRules` asset, e.g. `Textures` or `../Shared`. Empty means the asset's own folder |
| Rules > Patterns | Regex for allowed files, matched against the path relative to `Path`, e.g. `T_Hero.png` or `Sub/T_Hero.png`. Use `^...$` for a full match. Empty allows any file |
| Allow Other Files | Allow files under the asset's folder that no rule covers |
| Validate Folders | Also check folders, not only files |

Example: put this in `Assets/Characters`.

| Path | Patterns |
| --- | --- |
| `Textures` | `^T_[A-Za-z0-9]+\.png$` |
| `Models` | `^SM_[A-Za-z0-9]+\.fbx$`, `^SM_[A-Za-z0-9]+\.prefab$` |

With `Allow Other Files` off, files outside `Textures` and `Models` are errors too.

## Scope

- A rule covers its folder and all subfolders. Patterns see the subfolder part, so `^T_.*` does not allow `Sub/T_Hero.png`. Use `^([^/]+/)*T_.*`, or add a rule for `Textures/Sub`.
- When rules for several folders cover a file, the rule for the deepest folder decides.
- Rules for the same folder are combined: the file is allowed if it matches any of them. This also applies across several `FileNameRules` assets.
- Several `FileNameRules` assets can be placed in the same folder.
- For files that no rule covers, the `FileNameRules` assets in the nearest ancestor folder decide. If any of them has `Allow Other Files` off, the file is an error.
- The `FileNameRules` asset itself is not checked.

## Scene rules

Check GameObjects in scenes against rules for default names, tags, layers, prefab instances and duplicate names. Rules and the scenes they apply to are set in two kinds of assets, so different folders of scenes can follow different rules.

1. Create a `SceneRules` asset with `Create > muValidation > Scene Rules` and set the rules.
2. Create a `SceneRulesAssignments` asset with `Create > muValidation > Scene Rules Assignments`, and assign the `SceneRules` to folders.

`Assets/Settings/` is the recommended place for both. A `SceneRules` that no assignment refers to is not applied.

### SceneRules

| Field | Description |
| --- | --- |
| Severity | Report problems as errors or warnings |
| Default Names | Names Unity gives new objects (`GameObject`, `Cube`, `Main Camera`...) and whether objects may keep them. Filled with Unity's names when the asset is created; `Reset Default Names` in the Inspector fills it again. Duplicates such as `Cube (1)` count as the same name |
| Forbidden Tags | Tags that GameObjects may not use |
| Forbidden Layers | Layers that GameObjects may not be on |
| Tag Layers | Per tag, the layers GameObjects with that tag may be on |
| Component Layers | Per component type (full name, e.g. `UnityEngine.Camera`; pick one with `Select`), the layers GameObjects with that component may be on. `Include Subclasses` also applies it to derived types |
| Require Prefab Instance | GameObjects must be part of a prefab instance. Objects added to a prefab instance (added GameObject overrides) also break it. Objects tagged `EditorOnly`, and their children, are exempt. Not checked in Prefab Mode, where everything is part of the prefab |
| Unique Names | Root GameObjects in the same scene may not share a name. Child objects are not checked. Names are compared exactly, so `Cube` and `Cube (1)` are different. Objects hidden from the Hierarchy are not counted |

The Inspector shows the paths the asset is assigned to, and lists the objects in open scenes that break it. Click one to ping it.

### SceneRulesAssignments

| Field | Description |
| --- | --- |
| Assignments > Path | Folder from the project root, e.g. `Assets/Scenes/Stages`, or a scene path. `Assets` (or empty) covers all scenes |
| Assignments > Rules | `SceneRules` applied to scenes under the path |

Example:

| Path | Rules |
| --- | --- |
| `Assets` | `CommonSceneRules` |
| `Assets/Scenes/UI` | `UISceneRules` |

Scenes under `Assets/Scenes/UI` get both `CommonSceneRules` and `UISceneRules`; other scenes get only `CommonSceneRules`.

- When several assignments cover a scene, all their rules apply. A `SceneRules` assigned twice is applied once. Several `SceneRulesAssignments` assets are combined.
- Prefab Mode uses the prefab's path, e.g. `Assets/Prefabs/UI/Menu.prefab` is covered by `Assets/Prefabs/UI`. Unsaved scenes are covered only by `Assets`.
- The Inspector shows how many scenes each path covers, and warns about paths that do not exist.
- With [muHierarchy](../works.mmzk.muhierarchy/README.md), the header row of each scene in the Hierarchy window gets an info icon when rules apply to it. Click it to select the applied `SceneRules` asset (choose from a menu when several apply).

### Scope

- Checks GameObjects in open scenes, in the enabled scenes in Build Settings (Validation window and build check), and in Prefab Mode. Prefab assets are not checked.
- Each applied `SceneRules` reports its own problems. A name is an error if any of them forbids it.
- When any `SceneRules` with rules is assigned, every GameObject is validated, not only those with validated components.

## Validation attributes

Put validation attributes on a Component / ScriptableObject class or on its fields. Several can be stacked on the same target.

```csharp
using System.Collections.Generic;
using Mmzkworks.muValidation;
using UnityEngine;

[GameObjectName("^UI_[A-Za-z0-9]+$")]
[NotSceneRoot(Severity = ValidationSeverity.Warning)]
[RequireSceneObject("Systems/EventSystem", typeof(UnityEngine.EventSystems.EventSystem))]
public class MenuPanel : MonoBehaviour
{
    [SerializeField, RequireReference, ReferenceInChildren] private Transform content;
    [SerializeField, ReferenceInChildren] private List<GameObject> pages;
}
```

### Standard attributes

| Attribute | Target | Checks |
| --- | --- | --- |
| `[GameObjectName("regex")]` | Class | The GameObject name matches the regex. Use `^...$` for a full match |
| `[SceneRootOnly]` | Class | The GameObject is at the scene root (scenes only) |
| `[NotSceneRoot]` | Class | The GameObject is not at the scene root (scenes only) |
| `[RequireSceneObject("A/B", typeof(T))]` | Class | The scene has a GameObject at path `A/B` (from a root object). With a type, it must also have that component (scenes only) |
| `[RequireReference]` | Field | The field is not null or missing. For arrays and lists, each element |
| `[ReferenceInChildren]` | Field | The reference is this GameObject or one of its descendants. Null is allowed; references outside the hierarchy or to assets are errors. For arrays and lists, each element |
| `[NotEmpty]` | Field | The string is not null, empty or whitespace. For arrays and lists, each element |
| `[RequireComponentInParent(typeof(T))]` | Class | The GameObject or one of its parents has a `T` component |
| `[SingleInScene]` | Class | The scene has only one component of this type, inactive objects included (scenes only) |

- Every attribute takes `Severity = ValidationSeverity.Warning` to report warnings instead of errors.
- "Scenes only" checks are skipped in prefab assets and Prefab Mode.
- Field attributes also work on fields of nested `[Serializable]` classes and structs, and on arrays and lists of them.

### Combining attributes

Derive from `CompositeValidationAttribute` and return the validations to combine. Use it like any other attribute.

```csharp
public class UIRootAttribute : CompositeValidationAttribute
{
    protected override IEnumerable<ValidationAttribute> CreateValidations()
    {
        yield return new GameObjectNameAttribute("^UI_");
        yield return new SceneRootOnlyAttribute();
    }
}

[UIRoot]
public class HudRoot : MonoBehaviour { }

// Passes if either name pattern matches.
public class PlayerOrEnemyNameAttribute : CompositeValidationAttribute
{
    public PlayerOrEnemyNameAttribute() { Mode = CompositeMode.Any; }

    protected override IEnumerable<ValidationAttribute> CreateValidations()
    {
        yield return new GameObjectNameAttribute("^Player");
        yield return new GameObjectNameAttribute("^Enemy");
    }
}
```

- `CompositeMode.All` (default): every validation must pass. Each problem keeps its own severity, capped at the composite's `Severity`.
- `CompositeMode.Any`: one passing validation is enough. Otherwise a single problem listing all failures is reported at the composite's `Severity`.

### Adding your own attribute

Derive from `ValidationAttribute` and report problems with `context.Report`.

```csharp
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class PositiveAttribute : ValidationAttribute
{
    public override void Validate(ValidationContext context)
    {
        if (context.Value is int i && i <= 0) context.Report("Must be positive");
    }
}
```

`ValidationContext` provides:

| Member | Description |
| --- | --- |
| `Target` / `GameObject` | The Component or ScriptableObject, and its GameObject (null for a ScriptableObject) |
| `Location` | `Asset` (ScriptableObject), `Prefab` (prefab asset or Prefab Mode) or `Scene` |
| `Field` / `Value` | The field and its value, for field attributes. Null for class attributes |
| `Elements()` | The value as elements with labels (`[0]`, `[1]`...) for arrays and lists, or the value itself |
| `Report(message)` | Reports a problem at the attribute's `Severity` |

If the result depends on other objects (their names, existence and so on), override `DependsOnOtherObjects` to return `true`, as `RequireSceneObject` does. Such validations are re-run after any change in the scene; others only when their own object changes.

### Where results show

- Errors show a red ✗ mark and warnings a yellow ! mark, drawn over the asset icon. In tooltips, warning lines start with `Warning:`.
- ScriptableObject: the asset gets the mark. Sub-assets with validation attributes are checked too.
- Component: a prefab gets the mark if any component in it reports a problem, including inactive children and nested prefabs. The message shows the GameObject path, component type and field.
- `Validate` runs only in the Editor. Keep it free of side effects. Exceptions are logged and shown as errors.
- Results are cached. An import or save re-checks only the changed assets (and prefabs that nest them); other results are reused while the asset is unchanged. Editing a ScriptableObject in the Inspector updates it right away. Prefab changes show after the prefab is saved. Deleting or moving assets re-checks everything.
- Asset results are also saved to `Library/muValidation/`, so they survive script recompiles, entering Play Mode and Editor restarts. Saved results are reused only for unchanged assets, and are discarded when validation code changes. Clear them with `Tools > muValidation > Clear Validation Cache`.
- Folder marks come from a scan of the whole `Assets` folder. The scan is spread over several frames, so the marks can lag a moment after an import.

## Validation window

`Tools > muValidation > Validation` lists every problem in one place.

![Validation window](Documentation~/img/validation-window.png)

- Covers assets (FileNameRules and validation attributes) and GameObjects in open scenes and Prefab Mode (validation attributes and SceneRules). Turn each on or off with the `Assets` / `Scenes` buttons.
- Filter by errors or warnings, and search by path or message.
- Click a row to ping the object. Double-click to select it (assets are also opened). The full message shows at the bottom.
- Results are collected when the window opens and when you press `Refresh`.

## Validation before builds

Validation runs before every player build. By default the build stops if there are errors.

- Checks all assets and the enabled scenes in Build Settings. Scenes that are not open are opened in the background and closed again.
- Problems are logged to the Console (click to ping). Outside batch mode, the Validation window opens with the results.
- In batch mode (CI), the build fails with a `BuildFailedException`.
- `Tools > muValidation > Run Build Check` runs the same check without building.

Change it in `Project Settings > muValidation`. Settings are saved to `ProjectSettings/muValidationSettings.asset`, so commit them to share with your team.

| Setting | Default | Description |
| --- | --- | --- |
| Validate Before Build | On | Run the check before builds |
| Include Assets | On | Check all assets under `Assets` |
| Include Build Scenes | On | Check the enabled scenes in Build Settings |
| Fail On Errors | On | Stop the build when there are errors |
| Fail On Warnings | Off | Stop the build when there are warnings |

## With muHierarchy

When [muHierarchy](../works.mmzk.muhierarchy/README.md) is also installed, scene objects whose validation attributes or SceneRules report errors get the red error icon in the Hierarchy window, and those with only warnings get the yellow warning icon (ComponentView, with the Prefab icon shown). Like Missing Script, it also shows on parents. Hover to see the messages.

When changing the Tag / Layer by clicking muHierarchy's labels, Tags / Layers forbidden by SceneRules are grayed out and cannot be chosen.

Results are cached. Editing an object re-validates only that object; adding, deleting or moving objects rebuilds the results, visiting only objects with validated components (all objects if there are SceneRules).

Scene objects are not validated in Play Mode by default. Turn it on with `Tools > muValidation > Validate In Play Mode`; results then refresh at most every 0.5 seconds.
