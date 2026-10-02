English | [日本語](README.ja.md)

# muPrimitive

Helper primitive shapes for Unity. Add a component to a GameObject and it draws a shape — useful for debug visualization (ranges, directions, links between objects) and simple in-game effects.

Unity 2022.3 or later. MIT License. Works with the Built-in Render Pipeline and URP.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muprimitive
```

## Shapes

All shapes are added from **Add Component > muPrimitive**. They render in both Edit mode and Play mode.

### PieShape

A pie: a ring sector with thickness. Good for showing attack ranges, field of view on the ground, and so on.

| Field | Description |
| --- | --- |
| Inner Radius | Inner radius. `0` fills the sector to the center |
| Outer Radius | Outer radius |
| Angle | Opening angle in degrees (0–360). The sector opens around local +Z |
| Bottom / Top | Local Y of the bottom and top faces. If they are equal, the pie is a flat face |
| Segments | Divisions per full circle |

### ConeShape

A cone along local +Y.

| Field | Description |
| --- | --- |
| Radius / Height | Base radius and height |
| Apex At Origin | Off: the base is at the origin. On: the apex is at the origin (for vision cones and spotlight ranges) |
| Capped | Close the base |
| Segments | Divisions per full circle |

### LineShape

A cylinder from the GameObject to a target. It follows the target every frame.

| Field | Description |
| --- | --- |
| Target | Target Transform. If not set, Target Position is used |
| Target Position | Target position in world space |
| Radius | Cylinder radius (affected by the GameObject's scale) |
| Capped | Close both ends |
| Segments | Divisions per full circle |

## Appearance

These fields are common to every shape.

| Field | Description |
| --- | --- |
| Color | Color. If alpha is less than 1, the shape is drawn as semi-transparent |
| Shading | Strength of the camera-based shading (`0` = flat color). Shapes stay readable without scene lights |
| Always On Top | Draw on top of other objects |
| Custom Material | Use your own material instead of the default one. Color is passed to `_Color` and `_BaseColor` |

## Usage from code

```csharp
using Mmzkworks.muPrimitive;

var pie = gameObject.AddComponent<PieShape>();
pie.InnerRadius = 0.5f;
pie.OuterRadius = 2f;
pie.Angle = 120f;
pie.Color = new Color(1f, 0.3f, 0.2f, 0.4f);

var line = gameObject.AddComponent<LineShape>();
line.Target = enemy.transform;
```

Changes are applied in `LateUpdate`. Call `Refresh()` to apply them immediately.

### Random points inside a shape

`GetRandomPoint()` returns a uniformly distributed random point inside the shape, in world space. `GetRandomLocalPoint()` returns one in local space. Useful for spawn positions, particle emitters, and so on.

```csharp
var spawnPosition = pie.GetRandomPoint();

// Pass a System.Random for reproducible results (UnityEngine.Random is used when omitted)
var random = new System.Random(seed);
var p = cone.GetRandomPoint(random);
```

`ShapeSampler` has the same sampling as static methods (`SamplePie`, `SampleCone`, `SampleCylinder`), so you can use it without a component. These return points in local space.

## Notes

- Each shape uses the `MeshFilter` and `MeshRenderer` on the same GameObject. Shadows and probes are turned off.
- The generated mesh is not saved in the scene; it is rebuilt when the component is enabled.
