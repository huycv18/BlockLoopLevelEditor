# Level Design Tool — Architecture & Implementation Guide

> **Read this file before making any changes to the Level Design Tool.**

## Overview

Custom `EditorWindow` for editing level data compatible with runtime `LevelData`. Manual IMGUI rendering (no `EditorGUILayout`). All classes `internal`, namespace `BlockLoop.LevelDesign`.

**Open:** Menu → **Tools → Level → Level Design**

## Architecture

```
LevelDesignWindow (EditorWindow, orchestrates OnGUI)
├── LevelEditorContext          ← Single shared mutable state (grid, garages, connections, palette)
├── Common/
│   ├── SharedTypes             ← ToolMode, GenerateMode, CellData, PaletteEntry, GarageInfo, VehicleImportData, LevelGenerateConfig
│   ├── LevelEditorStyles       ← Layout constants, colors, shared GUIStyles
│   └── LevelEditorDrawUtils    ← ExpandRect, DrawWireRect, PackEdge/UnpackEdge, GetNumberContent
├── Left Panel (ILeftPanelZone[])
│   ├── ToolsZone               ← Toggle swatch matrix (Obstacle, Hidden, Garage, Connection)
│   ├── PalettesZone            ← Delegation to palette ToolGroups (ColorToolGroup)
│   ├── LeftPanelHelpers        ← Zone accent colors, toggle styles, zone header helper
│   ├── GaragePopupController   ← Floating popup for garage editing
│   └── ToolGroups/             ← ToolGroup base + 6 subclasses (incl. SelectToolGroup)
├── Grid Canvas
│   └── GridRenderer            ← Cell/line/axis/hover/connection drawing
├── Right Panel (IRightPanelGroup[])
│   ├── RightPanelHelpers       ← Shared styles, field helpers, BeginRightGroup/MeasureRightGroup
│   ├── GridSize/               ← GridSizeGroup (Width/Height fields + Generate Grid)
│   ├── LevelIO/                ← LevelIOGroup (Level ID, Quick Save/Load, Import/Export, Clear Board/All)
│   ├── Screenshot/             ← ScreenshotGroup (Enable toggle, From/To, Capture Range/Cancel)
│   ├── Generate/               ← GenerateGroup, GenerateCategory, IGenerateSection + 4 sections
│   └── Statistics/             ← StatisticsGroup, IStatisticsWidget, SummaryStats, ColorChart
├── Generation/                 ← LevelGenerator, IObstaclePlacer, IObstacleSymmetry, strategies (pure logic, no UI)
├── LevelImportExport           ← JSON ↔ Context mapping
├── LevelUndoSystem             ← Undo/Redo stack (50-entry cap): PushUndo/Undo/Redo/Clear
└── Common/LevelSnapshot        ← LevelSnapshot (data) + LevelSnapshotUtil (Capture/Restore)
```

## Core Data: `LevelEditorContext`

Single source of truth for all subsystems.

**Grid:** `CellData[] Cells` — flat 1D: `y * GridWidth + x`. Max 25×25 (pre-allocated 625). `CellData`: `{ int colorId, bool isObstacle, bool isHidden, int garageId }`

**Mutual exclusion — MUST be maintained by every tool:**
- `garageId ≥ 0` → `colorId = -1`, `isObstacle = false`
- `isObstacle` → `colorId = -1`, `garageId = -1`
- `colorId ≥ 0` = cube, may have `isHidden = true`
- When overwriting: clear incompatible fields + `RemoveConnectionsForCell()` if replacing cube/obstacle

**VehicleImportData[]:** `{ hasIce, iceCount, directionMode }` per cell — imported from JSON, preserved on export, not editable in tool.

**GarageMap:** `Dictionary<int, GarageInfo>` — `GarageInfo { cellX, cellY, directionType (0–3 = ↑↓←→), carColors (List<int>) }`. `GarageImportGUIDs` preserves `collectToolGUID`.

**Connections:** `HashSet<long>` — `PackEdge(a, b)` = `(min << 32) | max`. Toggle semantics. Helpers: `CellHasCube(idx)`, `CheckAdjacent(a, b)` (Manhattan = 1).

**Palette:** From `ColorConfigDataScriptableObject` at `OnEnable()`. `PaletteEntries[]: { materialId, color }`. `ColorLookup: Dictionary<int, Color>`.

**LevelGenerateConfig:** `ObstacleMinPercent/MaxPercent` (0–100), `SymmetryMode` (0=None, 1=H, 2=V, 3=Both), `DensityWeights[4]` (Scattered/Clustered/Line/Funnel), `ColorWeights: List<{materialId, int weight}>` (ratio-based: system computes weight/totalWeight).

**LevelId:** `int` on `LevelEditorContext`, default `-1` (unset). Authoritative source for exported `levelIndex` on every export (Quick Save included, overrides `ImportedJson`'s original value); auto-populated from `levelIndex` on every import. Live-bound to the "Level ID" field in `LevelIOGroup`.

**Screenshot capture (`ScreenshotGroup` + `LevelDesignWindow` batch-capture methods):** OS-level screen capture via `System.Drawing.Graphics.CopyFromScreen` (fully-qualified, no `using System.Drawing;` — it would collide with `UnityEngine.Color`), NOT a Unity render-to-texture (this is plain IMGUI, no camera/RenderTexture to read from). Capture region = `_gridAreaRect` converted to screen space via `GUIUtility.GUIToScreenPoint()` inside `OnGUI()` (cached each frame into `_gridAreaScreenRect`, since that API only works during an OnGUI call and the batch state machine runs from `EditorApplication.update`), scaled by `EditorGUIUtility.pixelsPerPoint` for DPI. **The Editor window must be visibly on-screen and unoccluded during capture** — this is a real screen grab, not an internal render. Batch sweep ("Capture Range", From/To Level ID) drives a small state machine off `OnEditorUpdate()` (same tick as toast fade-out): load → wait `BatchSettleDelaySeconds` (0.3s, lets Unity actually redraw before the pixel read) → capture → advance; skips IDs with no matching `Level_<id>.json` in the Levels folder; does **not** push undo (treated as read-only browsing, not an edit); saves to `Assets/_TheGame/Screenshots/Level_<id>.png` (auto-created, `AssetDatabase.Refresh()` at the end).

**HasAnyBoardData() / ClearBoard():** narrower alternative to `ClearAll()` — clears every cell's colors/obstacles/garages (via `RemoveGarage()`, so `GarageMap`/`GarageImportGUIDs` stay consistent and `OnGarageRemoved` fires)/connections/hidden flags, but leaves `GridWidth/Height`, `LevelId`, `GenerateConfig`, and the palette untouched (unlike `ClearAll()`, which also resets the grid to 5×5 and clears `LevelId`). Wired to the "Clear Board" button in `LevelIOGroup`, no confirm dialog (relies on Undo/Redo as the safety net, consistent with the Quick Save/toast-era UX).

**ResizeGrid(newW, newH):** Atomic migration: remap cells + VehicleImportData → remove out-of-bounds garages (fires `OnGarageRemoved`) → reindex connections → apply new size. `DoGenerateGrid()` wraps with confirmation dialogs.

**MoveBlock(srcMinX, srcMinY, w, h, deltaX, deltaY):** Used by `SelectToolGroup`. Rigid-translates a w×h block, overwriting the destination: snapshots the block first → drops connections touching the block on only one side, keeps+translates fully-internal ones → clears source → removes a foreign garage sitting at the destination (not part of the move) → writes the block into place, repositioning any moved `GarageInfo.cellX/cellY` in place (same id, not recreated) → re-adds translated internal connections. No-ops if the destination would go out of grid bounds — caller (`SelectToolGroup`) is expected to clamp the delta beforehand.

## Reactivity

| Mechanism | Triggers | Purpose |
|-----------|----------|---------|
| `OnToolChanged(ToolMode, int)` | `SelectTool()` | Reset tool-local state (pending link, popup) |
| `OnGarageRemoved(int)` | `RemoveGarage()`, `ResizeGrid()` | Close popup if editing that garage |
| `RequestRepaint` | Tool groups after cell modification | Force `Repaint()` |
| `LayoutDirty` | Window/grid resize, splitter drag | Rebuild `CellRects[]`, panel rects, axis labels |
| `StatusDirty` | Any cell modification | Rebuild `StatisticsGroup` (counts, chart) |

**Flow:** User click → `ProcessGridEvents()` → `ToolGroup.HandleCellEvent()` → modify data → `MarkStatusDirty()` + `RequestRepaint()` → next `OnGUI` rebuilds stats.

Layout is NOT rebuilt on cell changes — only on structural changes. Cell rendering reads `CellData` directly.

## Undo/Redo

`LevelUndoSystem` (root-level) — custom, not Unity's `Undo.RecordObject` (context isn't a `UnityEngine.Object`). Stack-based, 50-entry cap. `PushUndo(ctx)` **must be called before mutating** at every checkpoint: `ProcessGridEvents()` (once per `MouseDown`, not per drag), `DoGenerate`, `ApplyGridResize`, `DoClearAll`, all 3 import paths (File/Clipboard/Quick Load), `DoGenerateReceiverQueues`, and `GaragePopupController`'s 3 direct mutation points (via an `onBeforeMutate` callback passed into its constructor, since those don't go through `ToolGroup.HandleCellEvent`).

`LevelSnapshot`/`LevelSnapshotUtil` (`Common/LevelSnapshot.cs`) capture/restore `Cells`, `VehicleImportData`, grid size, `GarageMap`, `GarageImportGUIDs`, `Connections`, `GeneratedReceiverQueues`, `LevelId` — NOT `ImportedJson`/`LastImportPath` (export bookkeeping, not undoable state). `GarageMap`/`Connections`/`GarageImportGUIDs` are `readonly` on the context: restore via `Clear()` + repopulate, never reassign.

Keyboard: `Ctrl+Z` / `Ctrl+Y` / `Ctrl+Shift+Z`, handled in `HandleUndoRedoShortcuts()` early in `OnGUI()`. Event is only consumed when there's actually something to undo/redo, so native text-field undo still works when a field is focused and the app-level stack is empty. Stacks reset on every `OnEnable` (see Autosave below).

**Tool-switch shortcuts** (`HandleToolShortcuts()`, also called early in `OnGUI()`): `1`=PaintColor, `2`=Obstacle, `3`=ToggleHidden, `4`=PlaceGarage, `5`=LinkCube, `D`=Eraser, `V`=Select, `Escape`=clear Select's current selection. Only active when `GUI.GetNameOfFocusedControl()` is empty (no text field focused), so digits still type normally into Grid Width/Level ID/weight fields.

**Current Tool banner** (`DrawCurrentToolBanner()` in `LevelDesignWindow`): small window-space banner pinned to the top-left of `_gridAreaRect`, drawn after `GUI.EndScrollView()` so it never scrolls with grid content. Shows the active tool's display name + accent color, looked up from `_toolDisplayInfo` (a `Dictionary<ToolMode, ToolGroup>` built once in `OnEnable()` from `_toolGroups[].AssociatedTool`), with PaintColor/Eraser special-cased (PaintColor shows the actually-selected color; Eraser has no `ToolGroup` of its own since it shares `ColorToolGroup`).

**Toast notifications** (`LevelEditorContext.ShowToast : Action<string>`, same delegate pattern as `RequestRepaint`/`CellRectToWindow`, assigned to `LevelDesignWindow.ShowToast(string)` in `OnEnable()`): call `Ctx.ShowToast?.Invoke("message")` from anywhere (no per-class callback wiring needed) to show a large auto-fading banner, centered on the whole window (`_ctx.WindowWidth/WindowHeight`, not just the grid area). Requires `EditorApplication.update += OnEditorUpdate` (subscribed in `OnEnable`, unsubscribed in `OnDisable`) to keep repainting while a toast is visible/fading, since the Editor doesn't repaint every frame on its own. Scoped to "significant" actions only (Generate, Resize, Clear All, Import/Export, Quick Save/Load, Undo/Redo, Garage create/remove, Connection make/break, Select move) — deliberately NOT fired per-cell for paint/erase, to avoid notification spam. Replaced the 3 previous "OK"-only success dialogs (Export Complete ×2, Generate Receiver Queues success); confirm/cancel and error dialogs are unchanged. **When adding a new mutating feature that fits this "significant action" bucket, call `Ctx.ShowToast?.Invoke(...)` after the mutation succeeds — but don't add per-cell/high-frequency toasts.**

**⚠️ Any new mutating feature must call `LevelUndoSystem.PushUndo(ctx)` before mutating, or it will silently bypass Undo/Redo.**

## Level ID, Quick Save/Load, Autosave

`LevelEditorContext.LevelId` (int, -1 = unset) is the authoritative source for exported `levelIndex` (see above). `LevelIOGroup` has a live-bound "Level ID" field plus **Quick Save**/**Quick Load** buttons that read/write `Assets/_TheGame/Levels/Level_<ID>.json` directly (folder auto-created), skipping the OS file dialog. Quick Save shows no success dialog (by design, for speed); both still show validation/overwrite/replace-confirm dialogs.

`LevelDesignWindow._autosaveJson` (`[SerializeField] string`) round-trips through `BuildExportJson()`/`ImportFromJson()` in `OnDisable()`/`OnEnable()` so unsaved data survives a Unity script recompile (domain reload) or a window close/reopen. Undo/redo stacks are always reset on `OnEnable` since old snapshots don't correspond to the freshly-constructed context.

## Import/Export

**Round-trip preservation:**

| Strategy | Preserves |
|----------|-----------|
| `VehicleImportData[]` | `hasIce`, `iceCount`, `directionMode` |
| `GarageImportGUIDs` | `collectToolGUID` |
| `ImportedJson` (DeepClone as export base) | `colorHexCodes`, `levelIndex` (overridden by `LevelId` if set), `difficultyType`, `passengerQueuesCount`, `hasCurtainCovered`, `passengersQueuesData`, any unknown fields |

**Import:** Clears all state → reads `gridWidth/Height` → `gridSlotsData` (type 3 = obstacle) → `vehiclesData` (colorId, isHidden + store VehicleImportData) → `garagesData` (CreateGarage + fill GarageInfo + store GUID) → `vehicleConnectionsData` (PackEdge) → `passengersQueuesData` (parse → `ReceiverQueueResult[]` → `GeneratedReceiverQueues`) → stores original `JObject` as `ImportedJson`.

**Export:** Base = `ImportedJson?.DeepClone()` or new JObject with defaults. Always overwrites: `gridWidth/Height`, `gridSlotsData`, `vehiclesData`, `garagesData`, `vehicleConnectionsData`. GridSlotType: obstacle → 3, cube/garage → 1, empty → 0. Vehicle: uses stored `VehicleImportData` if available; DeepClones original JSON entry at same coord; otherwise defaults.

## Tool System

`ToolMode`: `None`, `PaintColor`, `Eraser`, `PaintObstacle`, `ToggleHidden`, `PlaceGarage`, `LinkCube`, `Select`

**`ToolGroup` base:** `IsToggleTool = true` → compact Tools zone. `false` → Palettes zone with swatch grids.

| Override | Purpose |
|----------|---------|
| `HandleCellEvent(idx, cx, cy, ref cell, isClick, isDrag, hasGarage)` | Grid click/drag |
| `OnMouseUp()` | Fires on mouse release for the active tool regardless of cursor position — used to finalize multi-step drags (e.g. `SelectToolGroup`'s marquee-select/move commit) |
| `CanHandleTool(ToolMode)` | Active tool check |
| `DrawPanel(startY, panelWidth)` | Left panel drawing (default impl for toggle tools: `BeginToolGroup` → `DrawSwatch` → `OnToggleClicked`; palette tools must override) |
| `DrawGridOverlayPreHover()` / `PostHover()` | Grid overlays |
| `OnToolChanged(ToolMode, int)` | Reset local state |
| `OnGridResized(oldW, newW, newH)` | Reindex local indices |

Shared: `DrawSwatch(rect, fill, isSel, label, style)` — bordered swatch, returns `true` on click.

### Behaviors

| Tool | Click empty | Click cube | Click obstacle | Click garage | Drag |
|------|------------|----------|---------------|-------------|------|
| PaintColor | Paint | Repaint (keep hidden/import data) | Replace | — | Paint |
| Eraser | — | Erase + connections + import data | — | — | Erase |
| PaintObstacle | Place | Replace | Toggle off | — | Paint |
| ToggleHidden | — | Toggle isHidden | — | — | Set hidden |
| PlaceGarage | Create + popup | — | — | Open popup | — |
| LinkCube | Select first | Complete link (toggle edge) | — | — | — |
| Select | Start marquee-select drag | (same) | (same) | (same, excluded from garage-popup click hijack) | Drag outside selection = extend/redraw selection; drag inside selection = move block (ghost preview), commits on `OnMouseUp` via `LevelEditorContext.MoveBlock()` |

**Right-click:** garage → remove; cube/obstacle → clear + connections. LinkCube → cancel pending.

**SelectToolGroup:** Rectangle drag-select (marquee) then drag-inside-selection to move the block (colors/obstacles/hidden/garages), overwriting the destination; internal connections translate with the block, connections with only one endpoint inside are dropped. `OnMouseUp` finalizes the selection or commits the move — this is why `LevelDesignWindow.ProcessGridEvents()` dispatches `EventType.MouseUp` to `ToolGroup.OnMouseUp()` for the active tool (added specifically to support this). `Escape` clears the current selection (`HandleToolShortcuts()`). Excluded from the garage-popup click hijack in `ProcessGridEvents()` so clicking a garage cell starts/extends a selection instead of opening its popup.

**PaintColor detail:** Repainting existing cube preserves `isHidden` + `VehicleImportData`. Painting over obstacle removes connections first.

**GaragePopupController:** Floating popup next to garage cell: 4 direction buttons (↑↓←→) + car queue (swatch list + "+" to open mini palette picker). Right-click car swatch = remove. Click outside = close. Draggable via its title bar (`MouseCursor.MoveArrow`), resizable via a bottom-right grip (`MouseCursor.ResizeUpLeft`, clamped to `[MinWidth,MaxWidth] × [MinHeight,MaxHeight]`); size (`_width`/`_height`) persists across garages opened in the same session, position always re-anchors next to the clicked cell. Body content is drawn in a `GUI.BeginScrollView` (content-space coordinates, not window-space) so shrinking below the natural content height scrolls instead of clipping; `_height = -1` means "auto" (view height = content height, no scrollbar).

## Left Panel

`ILeftPanelZone`: `MeasureHeight(pw)` + `Draw(startY, pw)`. Drawn sequentially via `_leftPanelZones[]` in `LevelDesignWindow`. Use `LeftPanelHelpers.BeginLeftZoneHeader()` for consistent zone headers.

**ToolsZone:** Compact toggle swatch matrix (66×66) for single-action tools (`ObstacleToolGroup`, `HiddenCubeToolGroup`, `GarageToolGroup`, `ConnectionToolGroup`, `SelectToolGroup`). Layout cache per panel width. Click → `tool.OnToggleClicked()` → `ctx.SelectTool()`.

**PalettesZone:** Delegation container for palette `ToolGroup`s (currently `ColorToolGroup`). Draws zone header + indented sub-groups via `tool.DrawPanel()`.

**LeftPanelHelpers:** Static class with shared zone accent colors, toggle swatch colors, `GUIContent` labels, `ToggleIconStyle` (lazy-init via `EnsureStyles()`), sizing constants (`ToggleSwatchSize = 66`, `ToggleSwatchSpacing = 12`), and `BeginLeftZoneHeader()` helper.

## Right Panel

`IRightPanelGroup`: `MeasureHeight(pw)` + `Draw(startY, pw)`. Drawn sequentially. Use `BeginRightGroup()` for consistent headers.

**RightPanelHelpers:** Lazy-init styles (`ButtonStyle`, `FieldStyle`, `PopupStyle`, `LabelStyle`, `PlaceholderStyle`). Inputs: `IntFieldWithHint()`, `FloatFieldWithHint()`, `SliderWithField()`. Weight bar: `DrawWeightStatusBar()` (green/yellow/red). Pre-built control names `"_rp0".."_rp31"` for zero-GC.

**Generate subsystem:** `GenerateGroup` → two `GenerateCategory` (container with `IGenerateSection[]`) + 3 generate buttons. `IGenerateSection`: `{ Title, MeasureHeight(cw), Draw(x, y, cw) }`. Sections: `AreaRatioSection` (min/max %), `SymmetrySection` (dropdown), `DensitySection` (4 weight sliders + status bar), `ColorWeightSection` (add/remove colors + int weight input fields + palette picker).

**Statistics:** `StatisticsGroup` → `IStatisticsWidget[]`: `Rebuild(ctx)` on `StatusDirty`, `Draw()` every `OnGUI`. Widgets: `SummaryStatsWidget` (counts), `ColorChartWidget` (bar chart with per-color counts including garage cars).

## Random Generation

```
LevelGenerator.Generate(ctx, mode)
  All           → Clear all → PlaceObstacles → FillColors
  ObstaclesOnly → Clear obstacles → PlaceObstacles (preserve cubes/garages)
  ColorsOnly    → Clear colors → FillColors (preserve obstacles/garages)
```

**Validation:** Grid must be active. Color generation requires `ColorWeights.Count > 0` and all entries have `weight > 0`.

**Obstacle placement:** 1. Target count from min/max % → 2. `IObstacleSymmetry.BuildCandidateZone()` → 3. Split by `IObstaclePlacer` weights → 4. `ApplyMirror()` → 5. `EnsureReachability()` (BFS from row 0, removes bridge obstacles).

**Placers:** `ScatteredPlacer` (shuffle + pick N), `ClusteredPlacer` (BFS flood-fill from seeds), `LinePlacer` (random walk + momentum), `FunnelPlacer` (V-shape from edge).

**Symmetries:** `NoneSymmetry`, `HorizontalSymmetry` (L↔R), `VerticalSymmetry` (T↔B), `FullSymmetry` (4-corner).

**Shared buffers:** `ObstacleStrategyHelper` — static reusable `List<int>`/`HashSet<int>`.

**Color fill:** Shuffle free cells → distribute by weight ratios. Last color gets remainder.

## Documentation

| File | Audience | Update when |
|------|----------|------------|
| [`LevelDesign_Manual.md`](LevelDesign_Manual.md) | Game Designer | UX changes: tools, mouse behavior, UI controls, usage flow |
| [`LevelDesign_SystemDesign.md`](LevelDesign_SystemDesign.md) | Game Developer | Code changes: classes, data flow, interfaces, architecture |

> **⚠️ REQUIRED:** After completing Level Design Tool changes, update the corresponding documentation file(s).

## Adding New Features

**New Tool:** `MyToolGroup : ToolGroup` in `LeftPanel/ToolGroups/` → add `ToolMode.MyTool` enum → set `IsToggleTool` (true = Tools zone, false = Palettes) → override `HandleCellEvent()`, `DrawPanel()`, optionally overlay/resize/tool-changed → register in `OnEnable()` → `_toolGroups[]` → **maintain CellData mutual exclusion** → if the tool mutates outside `HandleCellEvent` (like `GaragePopupController`), thread in a way to call `LevelUndoSystem.PushUndo(ctx)` before mutating

**New Obstacle Strategy:** Implement `IObstaclePlacer` in `RightPanel/Generation/ObstacleStrategies.cs` → add to `LevelGenerator.s_placers[]` → extend `DensityWeights[]` + update `DensitySection` UI

**New Generate Section:** Implement `IGenerateSection` (`Title`, `MeasureHeight`, `Draw`) in `RightPanel/Generate/` → add to `GenerateCategory` constructor in `GenerateGroup` → store config on `LevelGenerateConfig`

**New Left Panel Zone:** Implement `ILeftPanelZone` (`MeasureHeight`, `Draw`) in `LeftPanel/` → use `LeftPanelHelpers.BeginLeftZoneHeader()` for zone header → add to `_leftPanelZones[]` in `OnEnable()`

**New Right Panel Group:** Implement `IRightPanelGroup` (`MeasureHeight`, `Draw`) in `RightPanel/<GroupName>/` → use `RightPanelHelpers.BeginRightGroup()` + `RightPanelHelpers` → add to `_rightPanelGroups[]` in `OnEnable()`

**New Statistics Widget:** Implement `IStatisticsWidget` (`MeasureHeight`, `Rebuild`, `Draw`) in `RightPanel/Statistics/` → `Rebuild()` = compute + cache (on `StatusDirty`); `Draw()` = render cached (every `OnGUI`, zero-alloc) → add to `IStatisticsWidget[]` in `OnEnable()`

**New Editable LevelData Field:** Add storage on `LevelEditorContext` (or `CellData`/`GarageInfo`) → read in `ImportFromJson()`, write in `Build*Array()` → migrate from preservation layer if applicable → add tool/UI + `MarkStatusDirty()`

## Refactoring Guidelines
- **Preserve existing behavior:** Avoid unintended side effects, especially in shared data structures and core flows.
- **Update documentation:** Reflect any architectural or behavioral changes in `LevelDesign_SystemDesign.md` and `LevelDesign_Manual.md`.