English | [日本語](README.ja.md)

# muRefgraph

An Editor extension that shows the components of a GameObject or Prefab, and the objects their fields reference, as a graph.

You can also regroup the components by the assembly (asmdef) they belong to.

![Components and references grouped by assembly](Documentation~/img/graph.png)

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.murefgraph
```

## How to open

From a GameObject in the Hierarchy, open it with `GameObject/muRefgraph/Open Ref Graph`.

Select a Prefab in the Project window and open it from `Assets/Open Ref Graph`.

You can also open it from `Window/muRefgraph/Ref Graph` (the selected GameObject becomes the target).

## Graph

From left to right: the target GameObject, its components, and the objects referenced by their fields.

- Only the target's own components are shown (child GameObjects are not included).
- References are followed one level. When several fields reference the same object, they are merged into one line.
- References to the GameObject itself or to another of its components are drawn as orange lines.
- Hover over a node to highlight its lines and show the field names.

Drag nodes to rearrange them, and pan and zoom to see the whole graph. Double-click a node to select that object. Press `Home` to center the graph.

| Action | Result |
| --- | --- |
| Left-drag on a node | Move nodes |
| Left-drag on the background | Box selection |
| Ctrl / Cmd + click | Add to / remove from selection |
| Middle button / Alt + left-drag | Pan |
| Mouse wheel | Zoom |
| Double-click | Select and ping the object |

## Group by Assembly

Turn on `Group by Assembly` on the toolbar to group the components into frames by the assembly their type belongs to. The color bar on the left of each component shows its assembly.

Manual layouts are kept separately for when `Group by Assembly` is on and off. `Reset Layout` restores the default layout.

## Other features

### Follow Selection

Turn on `Follow Selection` on the toolbar to follow the GameObject selected in the Hierarchy or Project window.
