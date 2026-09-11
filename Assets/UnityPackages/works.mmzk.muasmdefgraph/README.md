English | [日本語](README.ja.md)

# muAsmdefgraph

An Editor extension that shows Assembly Definition references as a graph.

You can see which asmdef references which other ones as nodes and lines.

![Asmdef reference graph](Documentation~/img/graph.png)

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muasmdefgraph
```

## How to open

Select a `.asmdef` in the Project window and open it from `Assets/Open Asmdef Graph`.

From a GameObject in the Hierarchy, `GameObject/muAsmdefgraph/Open Component Asmdef Graph` opens the asmdef graph for its attached scripts.

Combined with [muHierarchy](../works.mmzk.muhierarchy/README.md) Asmdef View, it is easier to follow which scene objects belong to which asmdef.

## Graph

Drag nodes to rearrange them, and pan and zoom to see the whole graph. Double-click a node to select the asmdef in the Project window.

Layout and comments are saved per open root.

## Other features

### DLL / unresolved

Use `Show DLLs` and `Show unresolved` on the toolbar to toggle precompiled and unresolved references.

### Comments

Place comment boxes on the graph to group related nodes.
