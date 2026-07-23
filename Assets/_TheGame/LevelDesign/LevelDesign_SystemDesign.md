# Level Design Tool — System Design

> Tài liệu dành cho **Game Developer**. Mô tả kiến trúc, luồng dữ liệu và cách mở rộng tool.

## Kiến trúc tổng quan

```
LevelDesignWindow (EditorWindow — OnGUI orchestrator)
│
├── Common/
│   ├── SharedTypes                 ← ToolMode, GenerateMode, CellData, PaletteEntry, GarageInfo, VehicleImportData, LevelGenerateConfig
│   ├── LevelEditorStyles           ← Layout constants, colors, shared GUIStyles, EnsureStyles()
│   └── LevelEditorDrawUtils        ← ExpandRect, DrawWireRect, PackEdge/UnpackEdge, GetNumberContent
│
├── LevelEditorContext              ← State duy nhất, chia sẻ toàn bộ
│   ├── CellData[] Cells            ← Grid 1D: y * Width + x
│   ├── Dictionary<int, GarageInfo> GarageMap
│   ├── HashSet<long> Connections
│   ├── VehicleImportData[]         ← Dữ liệu non-editable per-cell
│   ├── GarageImportGUIDs           ← Bảo toàn collectToolGUID
│   ├── ImportedJson (JObject)      ← DeepClone gốc, dùng làm base export
│   ├── LevelId (int)               ← -1 = chưa đặt; nguồn chân lý cho levelIndex khi export; xem mục Level ID
│   ├── LevelGenerateConfig         ← Cấu hình sinh ngẫu nhiên
│   └── PaletteEntries[]            ← Từ ColorConfigDataSO
│
├── Left Panel (ILeftPanelZone[])
│   ├── ToolsZone (swatch 66×66px)  ← Toggle tool matrix
│   │   ├── ObstacleToolGroup       → ToolMode.PaintObstacle
│   │   ├── HiddenCubeToolGroup      → ToolMode.ToggleHidden
│   │   ├── GarageToolGroup         → ToolMode.PlaceGarage
│   │   ├── ConnectionToolGroup     → ToolMode.LinkCube
│   │   └── SelectToolGroup         → ToolMode.Select (marquee select + move)
│   ├── PalettesZone (swatch 44×44px) ← Delegation container
│   │   └── ColorToolGroup          → ToolMode.PaintColor + Eraser
│   ├── LeftPanelHelpers            ← Shared colors, styles, constants, zone header helper
│   └── GaragePopupController      ← Popup floating cho garage (window-space, vẽ ngoài ScrollView)
│
├── Grid Canvas
│   ├── GridRenderer                ← Vẽ cell/line/axis/hover/connection/empty state
│   └── ReceiverQueueRenderer       ← Vẽ receiver queues phía trên grid (colored cells, labels, divider)
│
├── Right Panel (IRightPanelGroup[])
│   ├── RightPanelHelpers           ← Shared styles, field helpers, BeginRightGroup/MeasureRightGroup
│   ├── GridSize/
│   │   └── GridSizeGroup           ← Width/Height fields + Generate Grid
│   ├── ReceiverQueues/
│   │   └── ReceiverQueuesGroup     ← Queues Amount + Clear Ratio + Generate button
│   ├── LevelIO/
│   │   └── LevelIOGroup           ← Level ID, Quick Save/Load, Import/Export, Clear Board/All
│   ├── Screenshot/
│   │   └── ScreenshotGroup        ← Enable toggle, From/To Level ID, Capture Range/Cancel
│   ├── Generate/
│   │   ├── GenerateGroup           ← Container + 3 generate buttons
│   │   ├── GenerateCategory        ← Category header + IGenerateSection[] container
│   │   ├── AreaRatioSection        ← Min/Max % fields
│   │   ├── SymmetrySection         ← Dropdown popup (4 modes)
│   │   ├── DensitySection          ← 4 weight sliders + status bar
│   │   └── ColorWeightSection      ← Add/remove colors + weights + palette
│   └── Statistics/
│       ├── StatisticsGroup (IStatisticsWidget[])
│       ├── SummaryStatsWidget      ← Cube (grid + garage)/Obstacle/Connection/Garage counts + Colors Used (distinct/palette)
│       └── ColorChartWidget        ← Bar chart phân bố màu
│
├── Generation/                     ← Pure logic, no UI
│   ├── LevelGenerator              ← Orchestrator: Clear → Place → Fill
│   ├── IObstaclePlacer (strategy)
│   │   ├── ScatteredPlacer         ← Shuffle + pick N
│   │   ├── ClusteredPlacer         ← BFS flood-fill từ spaced seeds
│   │   ├── LinePlacer              ← Random walk + momentum + thinness check
│   │   └── FunnelPlacer            ← V-shape từ edge apex, random diagonal
│   ├── IObstacleSymmetry (mirror)
│   │   ├── NoneSymmetry            ← Full grid, mirror=1
│   │   ├── HorizontalSymmetry      ← Nửa trái → mirror phải, 1-2 cells
│   │   ├── VerticalSymmetry        ← Nửa trên → mirror dưới, 1-2 cells
│   │   └── FullSymmetry            ← 1/4 → mirror 4 góc, 1-4 cells
│   ├── ObstacleStrategyHelper      ← Static reusable buffers
│   └── ReceiverQueueGenerator     ← Sinh passengersQueuesData từ cube colors
│
├── LevelImportExport               ← JSON ↔ Context mapping
├── LevelUndoSystem                 ← Undo/Redo stack (50-entry cap), PushUndo/Undo/Redo/Clear
└── Common/LevelSnapshot            ← LevelSnapshot (data) + LevelSnapshotUtil (Capture/Restore)
```

## Data Model

### CellData (struct)

| Field | Type | Default | Mutual Exclusion |
|-------|------|---------|-----------------|
| `colorId` | `int` | -1 | ≥ 0 → `isObstacle = false`, `garageId = -1` |
| `isObstacle` | `bool` | false | `true` → `colorId = -1`, `garageId = -1` |
| `isHidden` | `bool` | false | Chỉ có nghĩa khi `colorId ≥ 0` |
| `garageId` | `int` | -1 | ≥ 0 → `colorId = -1`, `isObstacle = false` |

### GarageInfo (class)

| Field | Type | Mô tả |
|-------|------|--------|
| `cellX, cellY` | `int` | Vị trí trên grid |
| `directionType` | `int` | 0=↑, 1=↓, 2=←, 3=→ |
| `carColors` | `List<int>` | Hàng đợi xe (materialId) |
| `cachedCountStr` | `string` | Cache chuỗi count, cập nhật qua `UpdateGarageCountCache()` |

### VehicleImportData (struct)

| Field | Type | Mô tả |
|-------|------|--------|
| `hasData` | `bool` | Có dữ liệu import không |
| `hasIce` | `bool` | Cube có băng |
| `iceCount` | `int` | Số lớp băng |
| `directionMode` | `int` | 0=FREE, 1=UP, 2=DOWN, 3=LEFT, 4=RIGHT |

### LevelGenerateConfig (sealed class)

| Field | Type | Default | Mô tả |
|-------|------|---------|--------|
| `ObstacleMinPercent` | `int` | 10 | % diện tích tối thiểu |
| `ObstacleMaxPercent` | `int` | 25 | % diện tích tối đa |
| `SymmetryMode` | `int` | 0 | 0=None, 1=H, 2=V, 3=Both |
| `DensityWeights[4]` | `float[]` | [1,0,0,0] | Scattered/Clustered/Line/Funnel |
| `ColorWeights` | `List<ColorWeightEntry>` | empty | Danh sách màu + trọng số |
| `CellIndexBuffer` | `List<int>` | — | Buffer tái sử dụng |
| `ShowAddColorPalette` | `bool` | false | Toggle palette picker UI |

**ColorWeightEntry** (nested struct): `{ int materialId, int weight }`

### ReceiverQueueResult (struct)

| Field | Type | Mô tả |
|-------|------|--------|
| `queueIndex` | `int` | Index hàng chờ (0-based) |
| `colorTypesQueue` | `int[]` | Mảng colorId các receiver trong hàng chờ |

Lưu trên `LevelEditorContext.GeneratedReceiverQueues`. Được populate khi generate hoặc khi import level data có `passengersQueuesData`. Null khi chưa generate/import, hoặc khi Clear All.

### LevelEditorContext Constants

| Constant | Giá trị | Mô tả |
|----------|---------|--------|
| `DefaultGridSize` | 5 | Kích thước mặc định |
| `MinGridSize` | 2 | Tối thiểu |
| `MaxGridSize` | 25 | Tối đa |
| `MaxCellCount` | 625 | 25×25, pre-allocated |
| Cell index | `y * gridWidth + x` | Encoding toàn hệ thống |
| Connection | `PackEdge(a,b) = (min << 32) \| max` | Edge encoding |

### Grid Rendering (GridRenderer)

| Cell Type | Render |
|-----------|--------|
| Empty | Checkerboard (2 sắc xám xen kẽ) |
| Cube (color) | Color fill + inner shadow (bottom/right) + highlight strip (top) |
| Cube (hidden) | Color + purple overlay + "?" symbol |
| Obstacle | Dark bg + red "X" icon |
| Garage | Green bg + direction arrow + car count (bottom) |
| Hover | Translucent fill + wire border trên ô hover |
| Preview | Translucent color/obstacle preview (PaintColor/PaintObstacle mode) |
| Empty state | Grid icon (fontSize 48) + "No level data\nSet Width and Height to begin" |

## Luồng dữ liệu chính

### User Interaction Flow

```
Click/Drag trên grid
  → LevelDesignWindow.ProcessGridEvents()
    → GetCellUnderMouse() → cell index
    → Garage check: nếu ô có garage + tool không phải Eraser/LinkCube → CellRectToWindow() → GaragePopupController.Open()
    → ToolGroup.HandleCellEvent(idx, cx, cy, ref cell, isClick, isDrag, hasGarage)
      → Modify CellData / GarageMap / Connections
      → MarkStatusDirty() + RequestRepaint()

Right-click trên grid
  → HandleRightClick()
    → Pending link? → CancelPendingLink()
    → Has garage? → RemoveGarage() → fires OnGarageRemoved
    → Has cube/obstacle? → Clear cell + RemoveConnectionsForCell()
```

### Import Flow

```
DoImport() → file dialog → confirm replace → read file
  → LevelImportExport.ImportFromJson(json)
    → ClearEditorStateForImport()
    → Parse JObject → gridWidth/Height (clamped 2..25)
    → gridSlotsData[] → type 3 = obstacle
    → vehiclesData[] → colorId, isHidden + VehicleImportData
    → garagesData[] → CreateGarage + GarageInfo + GUID
    → vehicleConnectionsData[] → PackEdge
    → passengersQueuesData[] → ReceiverQueueResult[] (queueIndex + colorTypesQueue)
    → Store JObject as ImportedJson
    → GridActive = true, LayoutDirty = true
```

### Export Flow

```
DoExport() → HasAnyData()? → save file dialog
  → LevelImportExport.BuildExportJson()
    → Base = ImportedJson?.DeepClone() ?? new JObject (defaults)
    → Overwrite: gridWidth, gridHeight
    → BuildGridSlotsArray(): obstacle=3, cube/garage=1, empty=0
    → BuildVehiclesArray(): merge VehicleImportData + DeepClone original entry
    → BuildGaragesArray(): preserve original JSON + overwrite editable fields
    → BuildConnectionsArray(): firstConnectedIndex/secondConnectedIndex
    → If GeneratedReceiverQueues != null:
        → BuildReceiverQueuesArray() → overwrite passengersQueuesData + passengerQueuesCount
```

### Generate Flow

```
DoGenerate(mode) → validate grid/colors/weights → LevelGenerator.Generate(ctx, mode)
  ├── All:           ClearAll → PlaceObstacles → FillColors
  ├── ObstaclesOnly: ClearObstacles → PlaceObstacles (post: remove obstacles trên garages)
  └── ColorsOnly:    ClearColors → FillColors

PlaceObstacles:
  → Target = Random(min%, max%) × totalCells
  → IObstacleSymmetry.BuildCandidateZone() → danh sách cell ứng viên
  → EstimateAvgMirror() → maxPicks = target / avgMirror
  → Distribute picks by DensityWeights → IObstaclePlacer.SelectCells()
  → ApplyMirror() per obstacle
  → EnsureReachability(): BFS từ row 0, xóa tường chặn đường

FillColors:
  → Collect free cells (not obstacle, not garage)
  → Shuffle → distribute by ColorWeights ratio
  → Last color gets remainder
```

### Generate Receiver Queues Flow

```
DoGenerateReceiverQueues(queueCount, clearRatio)
  → ReceiverQueueGenerator.Generate(ctx, queueCount, clearRatio, out error)
    → Validate: queueCount > 0, clearRatio > 0
    → CollectCubeColors(): grid cubes (CellHasCube) + garage cars (GarageMap)
    → Validate: cubeCount > 0, queueCount ≤ clearRatio × cubeCount
    → Replicate: mỗi colorId × clearRatio → flat list (totalReceivers)
    → Shuffle (Fisher-Yates)
    → DistributeIntoQueues(): base = total / queueCount, remainder phân bổ đều
    → Return ReceiverQueueResult[] (chênh tối đa 1 receiver/queue)
  → ctx.GeneratedReceiverQueues = result
  → Export: BuildReceiverQueuesArray() ghi đè passengersQueuesData + passengerQueuesCount
```

**Import:** Khi JSON có `passengersQueuesData` (JArray, count > 0), parse thành `ReceiverQueueResult[]` → gán vào `ctx.GeneratedReceiverQueues` → tự động visualize.

**Clearing:** `GeneratedReceiverQueues` = null khi `ClearAll()` hoặc `ClearEditorStateForImport()`.

## Obstacle Strategy Algorithms

| Placer | Thuật toán | Tham số chính |
|--------|-----------|---------------|
| **ScatteredPlacer** | Fisher-Yates shuffle candidates → pick N đầu tiên | — |
| **ClusteredPlacer** | Chọn seeds cách nhau ≥ `minSpacing = max(3, (min(W,H)+1)/2)`. Từ mỗi seed, BFS flood-fill với neighbor order ngẫu nhiên | `seedCount = max(1, min(3, count/8))` |
| **LinePlacer** | Chọn seeds có 0 placed neighbors. Random walk: 55% giữ hướng, 45% rẽ. Chỉ chọn ô "thin" (≤1 placed neighbor) để tránh dày | `lineCount = max(1, min(4, count/4))` |
| **FunnelPlacer** | Chọn apex gần edge trên/dưới. 2 arms đi chéo: dx = baseDx (70%), 0 (20%), 2×baseDx (10%). dy luôn +1 | `funnelCount = max(1, min(3, count/(H×2)))` |

## Event System

### Event Subscriptions (đăng ký trong `OnEnable`)

| Event | Subscribers | Hành vi |
|-------|------------|---------|
| `ctx.OnToolChanged(ToolMode, int)` | `LevelDesignWindow` | `Repaint()` |
| | `GaragePopupController` | Đóng popup |
| | `ConnectionToolGroup` | `CancelPendingLink()` |
| | Các ToolGroup khác | No-op (sẵn sàng override) |
| `ctx.OnGarageRemoved(int)` | `GaragePopupController` | Đóng nếu đang edit garage đó |
| `ctx.RequestRepaint` | = `LevelDesignWindow.Repaint` | Force repaint mỗi OnGUI |

### Reactivity Flags

| Flag | Trigger | Kết quả |
|------|---------|---------|
| `LayoutDirty` | Resize window/grid, kéo splitter, import | Rebuild `CellRects[]`, panel rects, axis labels, font sizes |
| `StatusDirty` | Bất kỳ thay đổi cell nào | `StatisticsGroup.Rebuild()` (counts, chart) |

**Quy tắc:** Layout KHÔNG rebuild khi thay đổi cell — chỉ khi thay đổi cấu trúc. Cell rendering đọc trực tiếp `CellData`.

## Dialog Boxes

| Dialog | Điều kiện | Nút |
|--------|-----------|-----|
| "Resize Warning" (chi tiết data bị mất) | Thu nhỏ grid có data ở vùng cắt | Resize / Cancel |
| "Resize Grid" (thông báo) | Mở rộng hoặc không mất data | OK |
| "Import will replace..." | Import khi đang có data | Import / Cancel |
| "Import Error" | JSON parse fail | OK |
| "No data to export" | Export khi grid trống | OK |
| "Export Complete" (kèm path) | Export thành công | OK |
| "Export Error" | File write fail | OK |
| "Create a grid first" | Generate khi chưa có grid | OK |
| "Add at least one color" | Generate colors chưa có color | OK |
| "Enter weight for every color" | Generate colors có màu weight ≤ 0 | OK |
| "Clear Level Data" | Bấm Clear All | Clear / Cancel |
| "Generate Receiver Queues" (lỗi) | Thiếu input, không có cube, hoặc queue vượt quá giới hạn | OK |
| "Generate Receiver Queues" (thành công) | Generate xong, hiện tổng queue + receiver | OK |
| "Quick Save" (lỗi) | Chưa nhập Level ID, hoặc chưa có data | OK |
| "Quick Save" (ghi đè) | File `Level_<ID>.json` đã tồn tại | Overwrite / Cancel |
| "Quick Load" (lỗi) | Chưa nhập Level ID, hoặc không tìm thấy file | OK |

> Quick Save **không** hiện dialog "thành công" (khác Export File/Clipboard) — chủ đích để giữ thao tác nhanh, chỉ dialog khi cần xác nhận/báo lỗi.

## Undo/Redo System

`LevelUndoSystem` (root-level, không phải Unity `Undo.RecordObject` vì `LevelEditorContext` không phải `UnityEngine.Object` và `Dictionary`/`HashSet` bên trong không serialize được). Stack-based, cap **50 entries** (drop oldest khi vượt). API: `PushUndo(ctx)` (capture snapshot + xóa redo stack), `Undo(ctx)`, `Redo(ctx)`, `Clear()`.

`LevelSnapshot`/`LevelSnapshotUtil` (`Common/LevelSnapshot.cs`) — `Capture(ctx)`/`Restore(snapshot, ctx)` copy toàn bộ: `Cells[]`, `VehicleImportData[]`, `GridWidth/Height`, `GarageMap` (deep-copy từng `GarageInfo`), `GarageImportGUIDs`, `Connections`, `GeneratedReceiverQueues`, `LevelId`. **Không** gồm `ImportedJson`/`LastImportPath` (bookkeeping cho round-trip export, không phải state hiển thị). `GarageMap`/`Connections`/`GarageImportGUIDs` là `readonly` trên context → Restore phải `Clear()` + add lại, không được gán lại field. Sau Restore: `LayoutDirty = true`, `MarkStatusDirty()`, `SelectTool(ToolMode.None)`.

**Checkpoint gọi `PushUndo(ctx)` (trước khi mutate):**

| Vị trí | Thời điểm |
|---|---|
| `ProcessGridEvents()` | 1 lần/`MouseDown` (không lặp lại mỗi `MouseDrag` — cả nét kéo = 1 bước), che cả click trái và phải |
| `DoGenerate()` | Trước `LevelGenerator.Generate`, sau validate |
| `ApplyGridResize()` | Trước `ResizeGrid` |
| `DoClearAll()` | Trước `ClearAll`, sau dialog xác nhận |
| `DoImport()` / `DoImportString()` / `DoQuickLoad()` | Trước `ImportFromJson`, sau dialog xác nhận |
| `DoGenerateReceiverQueues()` | Sau validate, trước khi gán `GeneratedReceiverQueues` |
| `GaragePopupController` (3 điểm: đổi hướng, thêm xe, xóa xe) | Qua callback `Action onBeforeMutate` truyền vào constructor (không đi qua `ToolGroup.HandleCellEvent`) |

**Phím tắt:** `Ctrl+Z` = Undo, `Ctrl+Y`/`Ctrl+Shift+Z` = Redo, xử lý trong `HandleUndoRedoShortcuts()` đầu `OnGUI()`. Chỉ `Use()` event khi thực sự có undo/redo để làm — nếu stack rỗng, để native text-undo trong ô nhập liệu hoạt động bình thường. Redo stack bị xóa mỗi khi có `PushUndo` mới (chuẩn semantics). Undo/redo stack **reset rỗng mỗi `OnEnable`** (snapshot cũ không còn hợp lệ với `LevelEditorContext` mới). Chưa có nút Undo/Redo trên UI (chỉ phím tắt) trong đợt này.

> **Quan trọng cho người thêm tính năng mới:** Mọi thao tác mutate dữ liệu mới (tool mới, generate step mới, import path mới) **phải** gọi `LevelUndoSystem.PushUndo(ctx)` trước khi mutate để không phá vỡ Undo/Redo.

## Select Mode (chọn vùng + di chuyển)

`SelectToolGroup` (`LeftPanel/ToolGroups/SelectToolGroup.cs`) — toggle tool trong ToolsZone, `ToolMode.Select`, phím tắt `V`.

**Trạng thái local:** `_hasSelection` + bounds (`_selMinX/Y`, `_selMaxX/Y`); khi đang kéo: `_isDragging`, `_isMoving`, điểm bắt đầu/hiện tại (`_dragStartX/Y`, `_dragCurX/Y`); khi đang di chuyển: `_moveBuffer` (snapshot `CellData[]` của khối, chụp 1 lần lúc bắt đầu kéo, KHÔNG mutate `ctx.Cells` cho tới lúc thả chuột).

**Luồng tương tác:**
1. `MouseDown` ngoài vùng đã chọn (hoặc chưa có vùng chọn) → bắt đầu vẽ vùng chọn mới (`_isDragging=true, _isMoving=false`).
2. `MouseDown` **trong** vùng đã chọn → bắt đầu di chuyển (`_isMoving=true`), chụp snapshot khối vào `_moveBuffer`.
3. `MouseDrag` → cập nhật điểm hiện tại; vẽ overlay preview (vùng chọn đang vẽ, hoặc "ghost" mờ của khối tại vị trí đích khi đang di chuyển — dùng `Ctx.ColorLookup`/màu obstacle/garage để tô gần đúng nội dung thật).
4. `OnMouseUp` (hook mới trên `ToolGroup`, xem dưới) → nếu đang vẽ chọn: chốt bounds → `_hasSelection=true`. Nếu đang di chuyển: tính delta (kẹp trong biên lưới qua `ClampDelta`) → gọi `_onBeforeMutate` (push undo) → `Ctx.MoveBlock(...)` → dịch chuyển bounds theo delta, **vùng chọn vẫn giữ nguyên tại vị trí mới** (không tự bỏ chọn).

**`ToolGroup.OnMouseUp()`** (hook mới, virtual, no-op mặc định): `LevelDesignWindow.ProcessGridEvents()` bắt `EventType.MouseUp` và gọi hook này cho tool đang active, **bất kể** con trỏ chuột có đang nằm trong grid hay không (để commit đúng ngay cả khi thả chuột ngoài lưới). Đây là điểm mở rộng mới cho các tool cần xử lý thao tác kéo nhiều bước (trước đây `ProcessGridEvents` chỉ xử lý `MouseDown`/`MouseDrag`).

**`Ctx.MoveBlock(...)`** (xem bảng method `LevelEditorContext` ở trên): snapshot khối → loại bỏ connection chỉ có 1 đầu trong khối (giữ + dịch chuyển connection có cả 2 đầu trong khối) → xóa nguồn → nếu đích có garage khác (không thuộc khối di chuyển) thì xóa garage đó (`RemoveGarage`) → ghi khối vào đích, garage được di chuyển giữ nguyên `GarageInfo` (chỉ đổi `cellX/cellY`, không tạo lại) → thêm lại connection nội bộ đã dịch chuyển.

**Ngoại lệ garage-popup:** `ProcessGridEvents()` loại `ToolMode.Select` khỏi điều kiện mở popup garage khi click (giống `Eraser`/`LinkCube`), để click/kéo trên ô garage hoạt động như chọn vùng bình thường thay vì mở popup.

**`Escape`** (trong `HandleToolShortcuts()`): nếu đang ở Select và có vùng chọn → `ClearSelection()`.

## Toast Notifications

`LevelEditorContext.ShowToast` (`Action<string>`, cùng pattern với `RequestRepaint`/`CellRectToWindow`) — gán bằng `LevelDesignWindow.ShowToast(string)` trong `OnEnable()`. Bất kỳ ToolGroup nào cũng gọi được `Ctx.ShowToast?.Invoke("...")` mà không cần thread callback riêng qua constructor.

**Cơ chế hiển thị:** `_toastMessage` + `_toastShownAt` (timestamp `EditorApplication.timeSinceStartup`) trên `LevelDesignWindow`. `EditorApplication.update += OnEditorUpdate` (đăng ký trong `OnEnable`, hủy trong `OnDisable`) gọi `Repaint()` liên tục khi có toast đang hiện/mờ dần, vì Unity Editor **không** tự repaint theo frame như game loop. `DrawToast()` tính alpha theo thời gian trôi qua (hiện rõ `ToastVisibleSeconds=2.5s`, mờ dần `ToastFadeSeconds=0.5s`), vẽ **giữa toàn bộ cửa sổ** (dùng `_ctx.WindowWidth/WindowHeight` để căn giữa, không phải chỉ riêng vùng grid), font lớn (22px), khác với Current Tool banner (nhỏ, góc trên-trái grid, không mờ dần).

**Phạm vi thông báo (đã chốt, không phải mọi thao tác):** Generate (3 chế độ), Generate Receiver Queues, Resize, Clear All, Import (File/Clipboard), Export (File/Clipboard), Quick Save, Quick Load, Undo, Redo, tạo/xóa Garage, nối/hủy Connection, di chuyển vùng chọn (Select). **Không** toast cho từng thao tác vẽ/xóa 1 ô đơn lẻ (kể cả khi kéo) — chủ đích tránh dồn dập.

**Thay dialog cũ:** 3 dialog trước đây chỉ có nút "OK" để báo thành công đã được thay bằng toast: "Export Complete" (File + Clipboard) và thông báo thành công của "Generate Receiver Queues". Các dialog cần xác nhận (Cancel/OK: xóa dữ liệu, ghi đè, cảnh báo mất dữ liệu resize, lỗi) **giữ nguyên**, không đổi.

## Current Tool banner

`LevelDesignWindow.DrawCurrentToolBanner()` — banner nhỏ ở góc trên-trái `_gridAreaRect`, vẽ ở **window-space** (sau `GUI.EndScrollView()`) nên không cuộn theo grid. Hiển thị tên tool + màu accent, tra từ `_toolDisplayInfo` (`Dictionary<ToolMode, ToolGroup>` build 1 lần trong `OnEnable()` từ `_toolGroups[].AssociatedTool`). `PaintColor` và `Eraser` được xử lý riêng (Paint hiện đúng màu đang chọn; Eraser dùng màu xám cố định vì cả 2 dùng chung `ColorToolGroup`). Chỉ vẽ khi `GridActive`.

## Screenshot (chụp board hàng loạt theo khoảng Level ID)

`ScreenshotGroup` (`RightPanel/Screenshot/ScreenshotGroup.cs`) — toggle "Enable Auto Screenshot" (`LevelDesignWindow._autoScreenshotEnabled`, `[SerializeField]`) + field From/To + nút Capture Range/Cancel. Group nhận toàn bộ state/hành vi qua delegate (không giữ state riêng ngoài 2 field text), theo đúng pattern getter/setter đã dùng ở các group khác.

**Cơ chế chụp:** OS-level screen capture (không phải render-to-texture của Unity), dùng `System.Drawing.Graphics.CopyFromScreen` — vì đây là công cụ IMGUI thuần, không có RenderTexture để đọc pixel qua đường Unity thông thường. Vùng chụp = `_gridAreaRect` (window-space) chuyển sang tọa độ màn hình tuyệt đối qua `GUIUtility.GUIToScreenPoint()` (gọi bên trong `OnGUI()`, cache vào `_gridAreaScreenRect` mỗi frame — vì API này chỉ hoạt động đúng trong ngữ cảnh OnGUI, còn state machine bên dưới chạy ngoài OnGUI qua `EditorApplication.update`), nhân với `EditorGUIUtility.pixelsPerPoint` để bù DPI scaling. **Giới hạn thực tế:** cửa sổ phải hiển thị thật trên màn hình lúc chụp (không bị thu nhỏ/che khuất), vì đây là ảnh chụp màn hình thật.

**State machine quét hàng loạt** (driven bởi `OnEditorUpdate()`, cùng vòng lặp với toast fade):
```
DoStartBatchCapture(fromId, toId)
  → validate (enabled, from<=to), confirm nếu board đang có data
  → EnsureScreenshotsFolderExists() → AdvanceBatchLoad()

AdvanceBatchLoad() [vòng lặp while, bỏ qua ID không có file Level_<id>.json trong Levels/]
  → đọc + ImportFromJson() (không qua PushUndo — đây là thao tác duyệt/xem, không phải chỉnh sửa muốn giữ lại)
  → set _batchWaitingToSettle=true, hẹn giờ +0.3s (BatchSettleDelaySeconds), Repaint(), return

OnEditorUpdate() mỗi tick: nếu đã tới giờ hẹn
  → CaptureCurrentBoardScreenshot(id) [chụp + lưu Screenshots/Level_<id>.png]
  → _batchCurrentId++ → AdvanceBatchLoad() tiếp

Hết khoảng → FinishBatchCapture(): AssetDatabase.Refresh() + toast tổng kết (đã chụp/đã bỏ qua)
```

Delay 0.3s giữa lúc load xong 1 level và lúc thực sự chụp là để Unity kịp vẽ lại nội dung mới lên màn hình trước khi screen-capture đọc pixel (nếu không, ảnh có thể vẫn là frame cũ do OS chưa kịp swap buffer).

## Level ID và Quick Save/Load

`LevelEditorContext.LevelId` (int, mặc định -1 = chưa đặt) là nguồn chân lý cho `levelIndex` khi export: `LevelImportExport.BuildExportJson()` ghi `jo["levelIndex"] = LevelId` khi `LevelId >= 0` (ghi đè bất kể `ImportedJson` gốc có gì), áp dụng cho **mọi** kiểu export (Quick Save lẫn Export File/Clipboard). `ImportFromJson()` đọc ngược `LevelId = jo.Value<int>("levelIndex")` (clamp `>= -1`) nên UI tự điền lại sau mọi lần import.

`LevelIOGroup` mở rộng tại chỗ: field `Level ID` (live-bind trực tiếp vào `ctx.LevelId`, không qua bước "apply" như Grid Size vì không có ngữ nghĩa phá hủy) + 2 nút **Quick Save**/**Quick Load**. Layout dùng lưới 2 cột (`DrawButtonPair`) cho Quick Save/Load, Import File/Clipboard, Export File/Clipboard để gọn hơn; Clear All full-width ở cuối.

`ColorWeightSection` có thêm **Randomize Colors**: nhập `Count` (số màu) + `Weight` (trọng số áp dụng chung) → shuffle danh sách index palette (tái dùng `ObstacleStrategyHelper.Shuffle`) → lấy N phần tử đầu → **thay thế toàn bộ** `cfg.ColorWeights` bằng các entry mới (màu ngẫu nhiên, cùng weight). Sau đó vẫn sửa được từng dòng như thêm thủ công qua "+ Add Color".

Quy ước lưu: `Assets/_TheGame/Levels/Level_<ID>.json` (`LevelDesignWindow.LevelsFolderAssetPath`), thư mục tự tạo (`Directory.CreateDirectory`) khi Quick Save lần đầu, `AssetDatabase.Refresh()` sau khi ghi để Project window thấy file mới. `DoQuickSave()`/`DoQuickLoad()` tái dùng `BuildExportJson()`/`ImportFromJson()` và validate y hệt Export/Import thường (kèm dialog ghi đè cho Quick Save, dialog "replace current data" cho Quick Load). Quick Save **không** hiện dialog thành công (chủ đích, để giữ thao tác nhanh).

## Autosave và Domain-Reload Recovery

`LevelDesignWindow._autosaveJson` (`[SerializeField] string`, sống sót qua domain reload giống `_gridWidth`/`_gridHeight`) tái dùng chính `BuildExportJson()`/`ImportFromJson()` làm cơ chế autosave, thay vì làm `Dictionary`/`HashSet` trong `LevelEditorContext` serialize được (rủi ro refactor lớn hơn nhiều).

- `OnDisable()` (gọi khi Unity recompile HOẶC đóng cửa sổ): nếu `HasAnyData()` → `_autosaveJson = BuildExportJson().ToString(Formatting.None)`; ngược lại rỗng. Bọc try/catch, không throw.
- `OnEnable()`: nếu `_autosaveJson` không rỗng → `ImportFromJson(_autosaveJson)` + sync lại field UI (Grid Size, Level ID).
- **Khôi phục ở MỌI `OnEnable`**, kể cả đóng/mở cửa sổ chủ động (không chỉ recompile) — đây là thay đổi hành vi có chủ đích so với trước (trước đây đóng cửa sổ = mất dữ liệu chưa export).
- Undo/redo stack reset rỗng mỗi `OnEnable` (xem mục Undo/Redo).

## Round-Trip Preservation

| Chiến lược | Dữ liệu bảo toàn |
|-----------|------------------|
| `VehicleImportData[]` | `hasIce`, `iceCount`, `directionMode` per cube |
| `GarageImportGUIDs` | `collectToolGUID` per garage |
| `ImportedJson` (DeepClone) | `colorHexCodes`, `levelIndex`, `difficultyType`, mọi field unknown |
| `BuildVehiclesArray` DeepClone | Toàn bộ JSON entry gốc tại cùng coord (bảo toàn custom fields) |
| `GeneratedReceiverQueues` | Populated khi generate hoặc import. Ghi đè `passengersQueuesData` lúc export. Null khi ClearAll |

## Interface & Extension Points

### ToolGroup (abstract base)

| Method | Mục đích |
|--------|---------|
| `HandleCellEvent(idx, cx, cy, ref cell, isClick, isDrag, hasGarage)` | Xử lý click/drag. Return true = consumed |
| `OnMouseUp()` | Fire khi thả chuột cho tool đang active, bất kể vị trí con trỏ — dùng để chốt thao tác kéo nhiều bước (vd `SelectToolGroup`) |
| `CanHandleTool(ToolMode)` | Kiểm tra tool active (default: `mode == AssociatedTool`) |
| `DrawPanel(startY, panelWidth) → float` | Vẽ UI trên left panel. Default impl cho toggle tools: `BeginToolGroup` → `DrawSwatch` → `OnToggleClicked`. Palette tools phải override |
| `DrawGridOverlayPreHover()` / `PostHover()` | Vẽ overlay trên grid |
| `OnToolChanged(ToolMode, int)` | Reset state khi chuyển tool |
| `OnGridResized(oldW, newW, newH)` | Reindex state khi resize |
| `MeasureHeight(panelWidth) → float` | Tính chiều cao left panel |

**Phân loại:** `IsToggleTool = true` → Tools zone. `false` → Palettes zone.

**Constants:** `SwatchSize = 44`, `SwatchSpacing = 8`

**Shared method:** `DrawSwatch(rect, fill, isSel, label, style) → bool` — bordered swatch with selection highlight, returns true on click.

### ILeftPanelZone

| Method | Mục đích |
|--------|---------|
| `MeasureHeight(panelWidth) → float` | Tính chiều cao zone |
| `Draw(startY, panelWidth) → float` | Vẽ zone, trả về y cuối cùng |

**Implementations:** `ToolsZone` (toggle swatch matrix, layout cache per-width), `PalettesZone` (delegation container cho palette ToolGroups). Dùng `LeftPanelHelpers.BeginLeftZoneHeader()` cho zone header.

### IRightPanelGroup

| Method | Mục đích |
|--------|---------|
| `MeasureHeight(panelWidth) → float` | Tính chiều cao cần thiết |
| `Draw(startY, panelWidth) → float` | Vẽ UI, trả về y cuối cùng |

### GenerateCategory (sealed class)

Container UI cho một nhóm `IGenerateSection[]`. Vẽ category header (accent bar + title) rồi từng section với sub-title. Constants: `CategoryHeaderHeight = 30`, `SectionTitleHeight = 24`, `SectionSpacing = 4`.

### IGenerateSection

| Method | Mục đích |
|--------|---------|
| `Title` | Tên section (sub-header) |
| `MeasureHeight(contentWidth) → float` | Tính chiều cao |
| `Draw(x, y, contentWidth) → float` | Vẽ controls, trả về y cuối |

### IStatisticsWidget

| Method | Mục đích |
|--------|---------|
| `MeasureHeight(width) → float` | Tính chiều cao |
| `Rebuild(ctx)` | Tính toán + cache (chỉ gọi khi StatusDirty) |
| `Draw(x, y, width)` | Render cached values (mỗi OnGUI, phải zero-alloc) |

### IObstaclePlacer

```csharp
void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH, Random rng, List<int> output)
```

### IObstacleSymmetry

```csharp
void BuildCandidateZone(int gridW, int gridH, List<int> output)
int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH)  // returns cells placed count
```

## Shared Utilities

### LevelEditorDrawUtils (Common/DrawUtils.cs)

| Method | Mô tả |
|--------|--------|
| `PackEdge(a, b) → long` | Canonical edge key (smaller in high bits) |
| `UnpackEdge(long, out a, out b)` | Decode edge |
| `PackCoordKey(x, y) → long` | Pack (x,y) to long |
| `GetNumberContent(n) → GUIContent` | Cached `GUIContent` cho số 0..99 |
| `ExpandRect(rect, amount) → Rect` | Mở rộng rect (border) |
| `DrawWireRect(rect, color, width)` | Vẽ 4 cạnh border |

### LevelEditorStyles (Common/EditorStyles.cs)

| Thành phần | Mô tả |
|------------|--------|
| `GroupTitleHeight`, `GroupSpacing`, `GroupInnerPadding`, `GroupAccentBarWidth`, `PanelPadding` | Layout constants |
| `SubHeaderHeight`, `SubHeaderAccentWidth`, `SubHeaderPadLeft` | Sub-header constants |
| `GroupTitleBgColor`, `GroupContentBgColor`, `SubHeaderBgColor` | Shared colors |
| `GroupTitleStyle`, `SubHeaderStyle`, `PanelLabelStyle` | Lazy-init GUIStyles (`EnsureStyles()`) |

### LevelEditorContext (instance methods)

| Method | Mô tả |
|--------|--------|
| `CellHasCube(idx) → bool` | `colorId ≥ 0 && !isObstacle && garageId < 0` |
| `CheckAdjacent(a, b) → bool` | Manhattan distance = 1 |
| `RemoveConnectionsForCell(idx)` | Xóa mọi connection chứa cell (4 neighbors) |
| `ResizeGrid(newW, newH)` | Atomic migration toàn bộ data |
| `MoveBlock(srcMinX, srcMinY, w, h, deltaX, deltaY)` | Di chuyển cứng 1 khối w×h, ghi đè đích — xem mục Select Mode |
| `HasAnyBoardData()` / `ClearBoard()` | Kiểm tra/xóa toàn bộ nội dung cell-level (màu, obstacle, garage — qua `RemoveGarage()`, connection, hidden) — **giữ nguyên** kích thước lưới, `LevelId`, `GenerateConfig`, palette (khác `ClearAll()` vốn reset cả grid về 5×5 và xóa `LevelId`) |
| `CellRectToWindow` (`Func<Rect, Rect>`) | Chuyển CellRect từ content-space (ScrollView) sang window-space. Set bởi `LevelDesignWindow` |

### LeftPanelHelpers (static)

| Utility | Mô tả |
|---------|--------|
| `AccentToolsZone`, `AccentPalettesZone` | Zone accent colors (`static readonly Color`) |
| `ToggleSelBorder`, `ToggleSwatchBorder`, `ToggleHighlight` | Toggle swatch rendering colors |
| `ToolsZoneTitle`, `PalettesZoneTitle` | `static readonly GUIContent` labels |
| `ToggleIconStyle` | Lazy-init GUIStyle cho toggle swatch icon (fontSize 36) |
| `ToggleSwatchSize = 66`, `ToggleSwatchSpacing = 12` | Sizing constants |
| `EnsureStyles()` | Lazy-init tất cả left panel styles |
| `BeginLeftZoneHeader(startY, pw, title, accent) → float` | Vẽ zone header (title rect + accent bar), trả về contentStartY |

### RightPanelHelpers (static)

| Utility | Mô tả |
|---------|--------|
| `ButtonStyle`, `FieldStyle`, `PopupStyle`, `LabelStyle`, `PlaceholderStyle` | Lazy-init GUIStyle (`EnsureStyles()`) |
| `IntFieldWithHint(rect, ref text, fallback, min, max, hint)` | Text field + placeholder + int parse + clamp |
| `FloatFieldWithHint(...)` | Tương tự cho float, InvariantCulture |
| `SliderWithField(x, y, w, h, value, min, max, ref text, repaint)` | Slider + text field combo. Round 2 decimals |
| `DrawWeightStatusBar(rect, total, content)` | Bar xanh/vàng/đỏ + "Total: X / 1.0" |
| `NextControlName() → string` | Zero-GC control names `"_rp0".."_rp63"`, tự reset mỗi OnGUI |
| `MeasureRightGroup(innerH) → float` | Tính chiều cao chuẩn right panel group |
| `BeginRightGroup(y, pw, title, accent, innerH, out content) → float` | Vẽ header group, trả về next Y |

### ObstacleStrategyHelper (static)

Cung cấp `List<int>` và `HashSet<int>` tái sử dụng: `CandidatePool`, `SelectedCells`, `RemainingCandidates`, `PartialOutput`, `UsedSet`. Bao gồm `Shuffle(list, rng)` (Fisher-Yates).

## Hướng dẫn mở rộng

### Thêm Tool mới

| Bước | Hành động |
|------|----------|
| 1 | Tạo `MyToolGroup : ToolGroup` trong `LeftPanel/ToolGroups/` |
| 2 | Thêm `ToolMode.MyTool` vào enum trong `Common/SharedTypes.cs` |
| 3 | Set `IsToggleTool` — `true` = Tools zone (66px swatch), `false` = Palettes zone (44px swatch grid) |
| 4 | Override `HandleCellEvent()`, `DrawPanel()`, các method overlay/resize nếu cần. Cần xử lý thao tác kéo nhiều bước (như Select) thì override thêm `OnMouseUp()` |
| 5 | Đăng ký trong `LevelDesignWindow.OnEnable()` → `_toolGroups[]` (event wiring tự động qua loop) |
| 6 | **Tuân thủ CellData mutual exclusion** + gọi `RemoveConnectionsForCell()` khi xóa cube |
| 7 | Gọi `LevelUndoSystem.PushUndo(ctx)` trước khi mutate nếu tool có đường mutate không đi qua `HandleCellEvent()` |
| 7 | **Gọi `LevelUndoSystem.PushUndo(ctx)` trước khi mutate** nếu tool có đường mutate riêng không đi qua `HandleCellEvent()` (xem mục Undo/Redo) — nếu không, Undo/Redo sẽ bỏ sót thao tác của tool mới |

### Thêm Obstacle Strategy

| Bước | Hành động |
|------|----------|
| 1 | Implement `IObstaclePlacer` trong `RightPanel/Generation/ObstacleStrategies.cs` |
| 2 | Thêm vào `LevelGenerator.s_placers[]` (index tương ứng DensityWeights) |
| 3 | Mở rộng `DensityWeights[]` size + update `DensitySection` UI (thêm label + slider) |

### Thêm Generate Section

| Bước | Hành động |
|------|----------|
| 1 | Implement `IGenerateSection` (`Title`, `MeasureHeight`, `Draw`) |
| 2 | Thêm vào `IGenerateSection[]` trong `GenerateCategory` constructor tại `GenerateGroup` |
| 3 | Thêm config field vào `LevelGenerateConfig` |

### Thêm Left Panel Zone

| Bước | Hành động |
|------|----------|
| 1 | Implement `ILeftPanelZone` (`MeasureHeight`, `Draw`) trong `LeftPanel/` |
| 2 | Dùng `LeftPanelHelpers.BeginLeftZoneHeader()` cho zone header |
| 3 | Thêm vào `_leftPanelZones[]` trong `OnEnable()` |

### Thêm Right Panel Group

| Bước | Hành động |
|------|----------|
| 1 | Implement `IRightPanelGroup` (`MeasureHeight`, `Draw`) trong `RightPanel/<GroupName>/` |
| 2 | Dùng `RightPanelHelpers.BeginRightGroup()` + `RightPanelHelpers` |
| 3 | Thêm vào `_rightPanelGroups[]` trong `OnEnable()` |

### Thêm Statistics Widget

| Bước | Hành động |
|------|----------|
| 1 | Implement `IStatisticsWidget` (`MeasureHeight`, `Rebuild`, `Draw`) trong `RightPanel/Statistics/` |
| 2 | `Rebuild()` = tính toán + cache (gọi khi StatusDirty). `Draw()` = render cached (mỗi OnGUI, zero-alloc) |
| 3 | Thêm vào `IStatisticsWidget[]` trong `StatisticsGroup` constructor tại `OnEnable()` |

### Thêm Editable LevelData Field

| Bước | Hành động |
|------|----------|
| 1 | Thêm storage trên `LevelEditorContext` (hoặc `CellData`/`GarageInfo` nếu per-entity) |
| 2 | Đọc trong `ImportFromJson()`, ghi trong `Build*Array()` |
| 3 | Migrate từ preservation layer (`VehicleImportData`/`GarageImportGUIDs`/`ImportedJson`) nếu cần |
| 4 | Thêm tool/UI + gọi `MarkStatusDirty()` sau thay đổi |
| 5 | Nếu field mới ảnh hưởng state Undo/Redo, thêm vào `LevelSnapshot`/`LevelSnapshotUtil.Capture`/`Restore` |

## Backlog / Roadmap (chưa làm)

Ghi chú từ đợt review + kế hoạch cải tiến gần nhất, chưa implement, ưu tiên thấp hơn các mục ở trên:

- **Select Mode (V)** kiểu Photoshop: multi-select (Shift/Ctrl+click, rectangle drag-select) + đổi màu/xóa hàng loạt — hạng mục lớn nhất, nên làm 1 phiên riêng.
- Nút Undo/Redo hiển thị trên UI (hiện chỉ có phím tắt) + bảng lịch sử thao tác kiểu History panel.
- Bucket Fill, Eyedropper (hút màu bằng Alt+click), Copy/Paste pattern (stamp vùng đã chọn), Pan & Zoom (giữ Space kéo, scroll zoom), rectangle-fill khi kéo chuột, tô đối xứng theo thời gian thực khi vẽ tay (đang chỉ áp dụng cho Generate).
- Level Browser: liệt kê `Level_*.json` trong thư mục Levels kèm thumbnail.
- Thay bớt `EditorUtility.DisplayDialog(..., "OK")` chỉ mang tính thông báo bằng status/toast không chặn thao tác.
- Defensive clamp cho tham số `fallback` ngay trong `IntFieldWithHint`/`FloatFieldWithHint`/`ParseFieldInt` (hiện chỉ fix tại từng call site, xem `ReceiverQueuesGroup`).
- Loại bỏ hẳn giới hạn cứng của control-name pool trong `RightPanelHelpers` (hiện chỉ tăng cap 32→64, không phải fix triệt để).
- Hiển thị rõ khi % obstacle sinh ra lệch khỏi Min/Max do làm tròn (ước lượng qua `EstimateAvgMirror`).
- Rollback khi import JSON lỗi giữa chừng (hiện `ClearEditorStateForImport()` đã chạy trước khi exception được ném).
- Thêm thanh trạng thái tổng trọng số cho `ColorWeightSection` (tương tự `DensitySection`).
- Dọn các chỗ còn cấp phát GC do truyền thẳng string vào `GUI.Label`/`GUI.Button` ở `GridSizeGroup`/`ReceiverQueuesGroup`/`LevelIOGroup`.
- Giới hạn trên hợp lý + validation UX cho Level ID (hiện là `int.MaxValue`, chưa có cap theo yêu cầu sản phẩm).
- Tùy chọn "bỏ qua autosave, bắt đầu trắng" khi mở tool (hiện luôn khôi phục nếu có autosave).
