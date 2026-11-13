English | [日本語](README.ja.md)

# muShortcutPanel

An Editor extension for laying out shortcuts to Project assets and web pages, plus labels, freely on a panel.

Save a panel per task as `<any name>.shortcutpanel.json` and switch between them. Open the window from `Tools/muShortcutPanel/ShortcutPanel`.

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mushortcutpanel
```

## Panel files

- The dropdown at the left of the toolbar lists every `*.shortcutpanel.json` under Assets. Pick one to switch the working file.
- Create a new panel with `New Panel...` in the dropdown, or `Create/muShortcutPanel/Shortcut Panel` in the Project window.
- Double-click a `*.shortcutpanel.json` in the Project window to open it directly in the ShortcutPanel window.
- Changes are saved automatically.

## Use mode and edit mode

Switch between `Use Mode` and `Edit Mode` with the toggle button at the right of the toolbar.

The `Path` toggle in the toolbar shows or hides the full path (asset path / URL / menu path) under each shortcut.

### Use mode

- Click an asset / folder shortcut to select it in the Project window.
- Click a URL shortcut to open it in the default browser.
- Click a menu command shortcut to run that menu item (Project / Hierarchy commands act on the current selection in those windows).
- Drag a shortcut to drop the asset elsewhere, e.g. onto an Inspector field.
- Right-click for `Copy Path` / `Copy URL` and `Open Asset`.

### Edit mode

- Drag shortcuts and labels to move them, drag the bottom-right corner to resize (snaps to an 8px grid).
- Drag on empty space to box select several elements (`Shift` / `Ctrl` (`Cmd`) adds to the selection, `Shift` / `Ctrl`-click toggles one, `Ctrl+A` selects all). Drag any selected element to move them together; `Delete`, `Duplicate` and the right-click menu apply to the whole selection.
- Edit the selected element's text, URL, title, font size and rect at the bottom of the window.
- `+ Label` adds a label and `+ URL` adds a URL shortcut (uses the clipboard if it contains a URL).
- `+ Menu` (or `Add Menu Command Here...` in the right-click menu) opens a searchable picker of menu commands, grouped into `Editor Menu`, `Project Window (Assets)` and `Hierarchy Window (GameObject)`.
- Right-click to duplicate, bring to front, send to back or delete. The `Delete` key also deletes.

## Adding shortcuts

- Drag and drop folders or files from the Project window.
- Drag and drop a link or the address bar URL from a web browser. `.url` / `.webloc` internet shortcut files are accepted too.
- Or copy a URL and press `Ctrl+V` (`Cmd+V` on macOS) on the panel.
- In edit mode, copying a menu path such as `Window/General/Console`, `Assets/Create/Folder` or `GameObject/Create Empty` and pressing `Ctrl+V` adds a menu command shortcut.

Assets are stored by GUID, so shortcuts follow moves and renames.

Make a shortcut 56px tall or more to show it as a tile with a large icon.

## File format

```json
{
    "version": 1,
    "items": [
        { "id": "…", "kind": "asset", "guid": "…", "path": "Assets/Scenes", "x": 8, "y": 8, "width": 176, "height": 24, … },
        { "id": "…", "kind": "url", "url": "https://docs.unity3d.com/", "title": "Unity Docs", … },
        { "id": "…", "kind": "menu", "menu": "GameObject/Create Empty", … },
        { "id": "…", "kind": "label", "title": "Scenes", "fontSize": 14, … }
    ]
}
```
