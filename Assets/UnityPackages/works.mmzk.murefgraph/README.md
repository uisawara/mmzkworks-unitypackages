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

- By default only the target's own components are shown. Turn on `Include Children` (below) to include child GameObjects.
- References are followed one level. When several fields reference the same object, they are merged into one line.
- References to the GameObject itself or to another of its components are drawn as orange lines. Reference lines always leave from the right side of a component; lines going back to the left loop around through the gap between nodes.
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

## Include Children

Turn on `Include Children` on the toolbar to add descendant GameObjects and their components to the graph.

![Graph including child GameObjects](Documentation~/img/children.png)

- Child GameObjects are listed in the left column in Hierarchy order, indented by depth and connected to their parent with green tree lines like the Hierarchy window.
- The second line of a child's component shows the name of the GameObject that owns it.
- References to a child or one of its components are drawn to that child's node instead of a separate reference node.

## Group by Assembly

Turn on `Group by Assembly` on the toolbar to group the components into frames by the assembly their type belongs to. The color bar on the left of each component shows its assembly.

Manual layouts are kept separately for when `Group by Assembly` is on and off. `Reset Layout` restores the default layout.

## Other features

### Follow Selection

Turn on `Follow Selection` on the toolbar to follow the GameObject selected in the Hierarchy or Project window.
