# Level Design (Huycv)

Unity Editor window for authoring grid-based levels: paint colours, obstacles, garages,
connections and hidden cubes, generate boards procedurally, inspect statistics, and export
levels to JSON with matching screenshots.

Editor-only. Nothing in this package is compiled into a player build.

## Installation

### Package Manager (Git URL)

*Window ▸ Package Manager ▸ + ▸ Install package from git URL…*

```
https://github.com/huycv18/BlockLoopLevelEditor.git?path=/Packages/com.huycv.leveldesign
```

To pin a version, append a tag: `…?path=/Packages/com.huycv.leveldesign#v1.0.0`

### manifest.json

```json
{
  "dependencies": {
    "com.huycv.leveldesign": "https://github.com/huycv18/BlockLoopLevelEditor.git?path=/Packages/com.huycv.leveldesign"
  }
}
```

### Local checkout

Copy the `com.huycv.leveldesign` folder into your project's `Packages/` folder.

## Requirements

- Unity 6000.3 or newer
- `com.unity.nuget.newtonsoft-json` 3.2.2 — resolved automatically as a package dependency

## Usage

Open the window from **Tools ▸ Level ▸ Level Design (Huycv)**.

### Output folders

Quick Save writes `Level_<id>.json` and screenshot capture writes `Level_<id>.png`. Both
folders are configured per project in **Project Settings ▸ Level Design (Huycv)**, also
reachable from the **Output Folders…** button in the window's Level I/O panel.

| Setting | Default |
| --- | --- |
| Levels Folder | `Assets/LevelDesign/Levels` |
| Screenshots Folder | `Assets/LevelDesign/Screenshots` |

If the project already contains `Assets/_TheGame/Levels` or `Assets/_TheGame/Screenshots`
— the layout used before this tool became a package — those are picked up automatically
until you save a preference of your own.

Settings live in `EditorPrefs` keyed by project path, so different projects on the same
machine keep separate folders and nothing is written into version control.

### Colour palette

The palette is read from any `ScriptableObject` exposing a serialized `colorConfigDatas`
list, where each element has `materialId`, `color` and `shadowColor` fields. It is resolved
in two steps:

1. `Resources.Load` on the configured **Palette Resource Path** (default `Config/ColorConfig`)
2. failing that, an `AssetDatabase` search for the configured **Palette Type Name**
   (default `ColorConfigDataScriptableObject`)

Both are editable in Project Settings. The package holds no compile-time reference to the
config type, so a host project can supply its own class under any namespace.

If no palette is found the window still opens, with an empty palette.

## Conflict safety

Everything lives in the `Huycv.LevelDesign` namespace inside the `Huycv.LevelDesign`
Editor-only assembly, and the menu entry is suffixed `(Huycv)`. Installing alongside another
level editor will not produce duplicate type or duplicate menu conflicts.

## Documentation

`Documentation~/` holds the full manual and the system design notes. Unity ignores folders
ending in `~`, so they are never imported into the host project.
