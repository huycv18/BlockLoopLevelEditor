using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Huycv.LevelDesign
{
    // ════════════════════════════════════════════════════════
    //  LevelEditorContext — shared mutable state
    // ════════════════════════════════════════════════════════

    internal sealed class LevelEditorContext
    {
        // ── Constants ──
        public const int DefaultGridSize = 5;
        public const int MinGridSize = 2;
        public const int MaxGridSize = 25;
        public const int MaxCellCount = MaxGridSize * MaxGridSize;

        // ── Grid data ──
        public CellData[] Cells;
        public int GridWidth;
        public int GridHeight;
        public bool GridActive;

        // ── Palette ──
        public PaletteEntry[] PaletteEntries;
        public int PaletteCount;
        public GUIContent[] PaletteTooltips;
        public readonly Dictionary<int, Color> ColorLookup = new Dictionary<int, Color>();

        // ── Vehicles import data ──
        public VehicleImportData[] VehicleImportData;

        // ── Garage data ──
        public int NextGarageId;
        public readonly Dictionary<int, GarageInfo> GarageMap = new Dictionary<int, GarageInfo>();

        // ── Generate Random ──
        public readonly LevelGenerateConfig GenerateConfig = new LevelGenerateConfig();
        public readonly Dictionary<int, int> GarageImportGUIDs = new Dictionary<int, int>();

        // ── Connections ──
        public readonly HashSet<long> Connections = new HashSet<long>();

        // ── Receiver Queues (generated) ──
        public ReceiverQueueResult[] GeneratedReceiverQueues;

        // ── Import/Export ──
        public JObject ImportedJson;
        public string LastImportPath;

        // ── Level identity ──
        public int LevelId = -1;   // -1 = unset/new level

        // ── Tool state ──
        public ToolMode ActiveTool = ToolMode.None;
        public int SelectedColorId = -1;

        // ── Hover ──
        public int HoverX = -1;
        public int HoverY = -1;

        // ── Layout cache ──
        public Rect[] CellRects;
        public int CachedCellCount;
        public float CellSize;
        public float GridOriginX, GridOriginY;
        public float TotalGridWidth, TotalGridHeight;
        public float WindowWidth, WindowHeight;
        public bool LayoutDirty = true;

        // ── Status ──
        public bool StatusDirty = true;

        // ── Events ──
        public event Action<ToolMode, int> OnToolChanged;
        public event Action<int> OnGarageRemoved;
        public Action RequestRepaint;
        public Func<Rect, Rect> CellRectToWindow;
        public Action<string> ShowToast;

        // ════════════════════════════════════════════════════════
        //  Init
        // ════════════════════════════════════════════════════════

        public void InitCells()
        {
            if (Cells != null && Cells.Length >= MaxCellCount)
                return;
            var nc = new CellData[MaxCellCount];
            for (int i = 0; i < MaxCellCount; i++)
            {
                nc[i].colorId = -1;
                nc[i].garageId = -1;
            }
            if (Cells != null)
            {
                int c = Mathf.Min(Cells.Length, MaxCellCount);
                for (int i = 0; i < c; i++)
                    nc[i] = Cells[i];
            }
            Cells = nc;
        }

        public void InitVehicleImportData()
        {
            if (VehicleImportData == null || VehicleImportData.Length < MaxCellCount)
                VehicleImportData = new VehicleImportData[MaxCellCount];
        }

        // ════════════════════════════════════════════════════════
        //  Tool selection (central — fires event for cross-reset)
        // ════════════════════════════════════════════════════════

        public void SelectTool(ToolMode mode, int colorId = -1)
        {
            ActiveTool = mode;
            SelectedColorId = colorId;
            OnToolChanged?.Invoke(mode, colorId);
        }

        // ════════════════════════════════════════════════════════
        //  Status
        // ════════════════════════════════════════════════════════

        public void MarkStatusDirty() => StatusDirty = true;

        // ════════════════════════════════════════════════════════
        //  Garage operations
        // ════════════════════════════════════════════════════════

        public int CreateGarage(int cx, int cy)
        {
            int id = NextGarageId++;
            GarageMap[id] = new GarageInfo { cellX = cx, cellY = cy, directionType = 0 };
            return id;
        }

        public void RemoveGarage(int garageId)
        {
            if (!GarageMap.TryGetValue(garageId, out var g))
                return;
            int idx = g.cellY * GridWidth + g.cellX;
            if (idx >= 0 && idx < MaxCellCount && Cells[idx].garageId == garageId)
            {
                Cells[idx].garageId = -1;
                Cells[idx].colorId = -1;
                Cells[idx].isObstacle = false;
                Cells[idx].isHidden = false;
            }
            GarageMap.Remove(garageId);
            GarageImportGUIDs.Remove(garageId);
            OnGarageRemoved?.Invoke(garageId);
        }

        public static void UpdateGarageCountCache(GarageInfo g)
        {
            int n = g.carColors.Count;
            g.cachedCountStr = n < LevelEditorDrawUtils.MaxCachedNumberStrings ? LevelEditorDrawUtils.NumberContents[n].text : n.ToString();
        }

        // ════════════════════════════════════════════════════════
        //  Connection operations
        // ════════════════════════════════════════════════════════

        public void RemoveConnectionsForCell(int cellIdx)
        {
            int x = cellIdx % GridWidth, y = cellIdx / GridWidth;
            if (x > 0) Connections.Remove(LevelEditorDrawUtils.PackEdge(cellIdx, cellIdx - 1));
            if (x < GridWidth - 1) Connections.Remove(LevelEditorDrawUtils.PackEdge(cellIdx, cellIdx + 1));
            if (y > 0) Connections.Remove(LevelEditorDrawUtils.PackEdge(cellIdx, cellIdx - GridWidth));
            if (y < GridHeight - 1) Connections.Remove(LevelEditorDrawUtils.PackEdge(cellIdx, cellIdx + GridWidth));
        }

        // ════════════════════════════════════════════════════════
        //  Cell queries
        // ════════════════════════════════════════════════════════

        public bool CellHasCube(int idx)
        {
            return idx >= 0 && idx < MaxCellCount &&
                   Cells[idx].colorId >= 0 && !Cells[idx].isObstacle && Cells[idx].garageId < 0;
        }

        public bool CheckAdjacent(int idxA, int idxB)
        {
            int ax = idxA % GridWidth, ay = idxA / GridWidth;
            int bx = idxB % GridWidth, by = idxB / GridWidth;
            return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) == 1;
        }

        // ════════════════════════════════════════════════════════
        //  Move block (Select tool — rigid translate, overwrites destination)
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Moves a w×h block of cells (colors, obstacles, hidden, garages, VehicleImportData) by
        /// (deltaX, deltaY), overwriting whatever is at the destination. Garages inside the block
        /// are repositioned in place (keep their id/car queue), not recreated. A garage sitting at
        /// the destination that is NOT part of the moving block is removed. Connections fully
        /// inside the block are preserved (translated); any connection touching the block on only
        /// one side is dropped rather than left dangling. Caller is responsible for clamping the
        /// destination to stay within grid bounds — this is a defensive no-op guard, not a clamp.
        /// </summary>
        public void MoveBlock(int srcMinX, int srcMinY, int w, int h, int deltaX, int deltaY)
        {
            if (deltaX == 0 && deltaY == 0) return;

            int destMinX = srcMinX + deltaX;
            int destMinY = srcMinY + deltaY;
            if (destMinX < 0 || destMinY < 0 || destMinX + w > GridWidth || destMinY + h > GridHeight)
                return;

            // 1. Snapshot block contents (relative-indexed) before touching anything.
            var buffer = new CellData[w * h];
            var vBuffer = new VehicleImportData[w * h];
            var movingGarageIds = new HashSet<int>();
            for (int ry = 0; ry < h; ry++)
            for (int rx = 0; rx < w; rx++)
            {
                int srcIdx = (srcMinY + ry) * GridWidth + (srcMinX + rx);
                buffer[ry * w + rx] = Cells[srcIdx];
                vBuffer[ry * w + rx] = VehicleImportData[srcIdx];
                if (buffer[ry * w + rx].garageId >= 0)
                    movingGarageIds.Add(buffer[ry * w + rx].garageId);
            }

            // 2. Keep connections fully inside the block (translated); drop any connection that
            //    touches the block on only one side, since its other endpoint isn't moving with it.
            var internalEdges = new List<(int rax, int ray, int rbx, int rby)>();
            var edgesToRemove = new List<long>();
            foreach (long edge in Connections)
            {
                LevelEditorDrawUtils.UnpackEdge(edge, out int a, out int b);
                int ax = a % GridWidth, ay = a / GridWidth;
                int bx = b % GridWidth, by = b / GridWidth;
                bool aIn = ax >= srcMinX && ax < srcMinX + w && ay >= srcMinY && ay < srcMinY + h;
                bool bIn = bx >= srcMinX && bx < srcMinX + w && by >= srcMinY && by < srcMinY + h;
                if (!aIn && !bIn) continue;
                edgesToRemove.Add(edge);
                if (aIn && bIn)
                    internalEdges.Add((ax - srcMinX, ay - srcMinY, bx - srcMinX, by - srcMinY));
            }
            for (int i = 0; i < edgesToRemove.Count; i++)
                Connections.Remove(edgesToRemove[i]);

            // 3. Clear source cells. Garages being moved are repositioned below, not deleted —
            //    do NOT call RemoveGarage() for ids in movingGarageIds.
            for (int ry = 0; ry < h; ry++)
            for (int rx = 0; rx < w; rx++)
            {
                int srcIdx = (srcMinY + ry) * GridWidth + (srcMinX + rx);
                Cells[srcIdx].colorId = -1;
                Cells[srcIdx].isObstacle = false;
                Cells[srcIdx].isHidden = false;
                Cells[srcIdx].garageId = -1;
                VehicleImportData[srcIdx] = default;
            }

            // 4. Clear destination: remove a foreign garage (not part of this move) that would
            //    otherwise be silently overwritten, and drop connections on any cube/obstacle
            //    about to be overwritten. Cells inside the source/destination overlap already
            //    read as empty here (cleared in step 3), so this only affects non-overlapping
            //    destination cells — correct, since overlapping cells belong to the moving block.
            for (int ry = 0; ry < h; ry++)
            for (int rx = 0; rx < w; rx++)
            {
                int destIdx = (destMinY + ry) * GridWidth + (destMinX + rx);
                int destGarageId = Cells[destIdx].garageId;
                if (destGarageId >= 0 && !movingGarageIds.Contains(destGarageId))
                    RemoveGarage(destGarageId);
                else if (Cells[destIdx].colorId >= 0 || Cells[destIdx].isObstacle)
                    RemoveConnectionsForCell(destIdx);
            }

            // 5. Write the moved block into place. Repositioned garages keep their existing
            //    GarageInfo (car queue/direction), just point at the new coordinates.
            for (int ry = 0; ry < h; ry++)
            for (int rx = 0; rx < w; rx++)
            {
                int destIdx = (destMinY + ry) * GridWidth + (destMinX + rx);
                var cell = buffer[ry * w + rx];
                Cells[destIdx] = cell;
                VehicleImportData[destIdx] = vBuffer[ry * w + rx];
                if (cell.garageId >= 0 && GarageMap.TryGetValue(cell.garageId, out var g))
                {
                    g.cellX = destMinX + rx;
                    g.cellY = destMinY + ry;
                }
            }

            // 6. Re-add internal connections translated to the new position.
            for (int i = 0; i < internalEdges.Count; i++)
            {
                var (rax, ray, rbx, rby) = internalEdges[i];
                int newA = (destMinY + ray) * GridWidth + (destMinX + rax);
                int newB = (destMinY + rby) * GridWidth + (destMinX + rbx);
                Connections.Add(LevelEditorDrawUtils.PackEdge(newA, newB));
            }

            MarkStatusDirty();
        }

        // ════════════════════════════════════════════════════════
        //  Grid resize (atomic migration of all data stores)
        // ════════════════════════════════════════════════════════

        public void ResizeGrid(int newW, int newH)
        {
            int oldW = GridWidth;
            int oldH = GridHeight;
            if (newW == oldW && newH == oldH) return;

            int minW = Mathf.Min(oldW, newW);
            int minH = Mathf.Min(oldH, newH);

            // 1. Migrate cells + importData
            var tmpCells = new CellData[MaxCellCount];
            var tmpImport = new VehicleImportData[MaxCellCount];
            Array.Copy(Cells, tmpCells, MaxCellCount);
            Array.Copy(VehicleImportData, tmpImport, MaxCellCount);

            for (int i = 0; i < MaxCellCount; i++)
            {
                Cells[i].colorId = -1;
                Cells[i].isObstacle = false;
                Cells[i].isHidden = false;
                Cells[i].garageId = -1;
                VehicleImportData[i] = default;
            }

            for (int y = 0; y < minH; y++)
            for (int x = 0; x < minW; x++)
            {
                int oldIdx = y * oldW + x;
                int newIdx = y * newW + x;
                Cells[newIdx] = tmpCells[oldIdx];
                VehicleImportData[newIdx] = tmpImport[oldIdx];
            }

            // 2. Remove garages outside new bounds
            // NOTE: Do NOT call RemoveGarage() here — it would clear cell data using
            // the old GridWidth, but cells have already been remapped to newW in step 1.
            // Instead, remove directly from maps and fire event for popup close.
            var garageRemoveList = new List<int>();
            foreach (var kv in GarageMap)
            {
                if (kv.Value.cellX >= newW || kv.Value.cellY >= newH)
                    garageRemoveList.Add(kv.Key);
            }
            for (int i = 0; i < garageRemoveList.Count; i++)
            {
                int gid = garageRemoveList[i];
                GarageMap.Remove(gid);
                GarageImportGUIDs.Remove(gid);
                OnGarageRemoved?.Invoke(gid);
            }

            // 3. Reindex connections
            var oldEdges = new long[Connections.Count];
            Connections.CopyTo(oldEdges);
            Connections.Clear();
            for (int i = 0; i < oldEdges.Length; i++)
            {
                LevelEditorDrawUtils.UnpackEdge(oldEdges[i], out int a, out int b);
                int ax = a % oldW, ay = a / oldW;
                int bx = b % oldW, by = b / oldW;
                if (ax >= newW || ay >= newH || bx >= newW || by >= newH) continue;
                Connections.Add(LevelEditorDrawUtils.PackEdge(ay * newW + ax, by * newW + bx));
            }

            // 4. Apply new size
            GridWidth = newW;
            GridHeight = newH;
            GridActive = true;
            LayoutDirty = true;
            MarkStatusDirty();
        }

        // ════════════════════════════════════════════════════════
        //  Clear board (colors + obstacles + garages + connections + hidden —
        //  everything cell-level, but NOT grid size / LevelId / GenerateConfig / palette)
        // ════════════════════════════════════════════════════════

        public bool HasAnyBoardData()
        {
            int total = GridWidth * GridHeight;
            for (int i = 0; i < total; i++)
                if (Cells[i].colorId >= 0 || Cells[i].isObstacle || Cells[i].garageId >= 0)
                    return true;
            return false;
        }

        public void ClearBoard()
        {
            // Remove garages through RemoveGarage (not just clearing the cell) so GarageMap /
            // GarageImportGUIDs stay consistent and OnGarageRemoved fires (closes any open popup).
            var garageIds = new List<int>(GarageMap.Keys);
            for (int i = 0; i < garageIds.Count; i++)
                RemoveGarage(garageIds[i]);

            int total = GridWidth * GridHeight;
            for (int i = 0; i < total; i++)
            {
                Cells[i].colorId = -1;
                Cells[i].isObstacle = false;
                Cells[i].isHidden = false;
                Cells[i].garageId = -1;
                VehicleImportData[i] = default;
            }

            Connections.Clear();
            MarkStatusDirty();
        }

        // ════════════════════════════════════════════════════════
        //  Clear all
        // ════════════════════════════════════════════════════════

        public void ClearAll(int defaultGridSize)
        {
            GridWidth = defaultGridSize;
            GridHeight = defaultGridSize;

            for (int i = 0; i < MaxCellCount; i++)
            {
                Cells[i].colorId = -1;
                Cells[i].isObstacle = false;
                Cells[i].isHidden = false;
                Cells[i].garageId = -1;
                VehicleImportData[i] = default;
            }

            GarageMap.Clear();
            NextGarageId = 0;
            GarageImportGUIDs.Clear();

            Connections.Clear();

            // Fire OnToolChanged to reset tool-local state (_linkFirstIdx, popup, etc.)
            SelectTool(ToolMode.None);

            GeneratedReceiverQueues = null;
            ImportedJson = null;
            LastImportPath = null;
            LevelId = -1;

            GridActive = false;
            LayoutDirty = true;
            MarkStatusDirty();
        }

    }
}
