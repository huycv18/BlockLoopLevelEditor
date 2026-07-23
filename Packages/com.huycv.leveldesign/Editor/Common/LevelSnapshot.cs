using System;
using System.Collections.Generic;

namespace Huycv.LevelDesign
{
    // ════════════════════════════════════════════════════════
    //  LevelSnapshot — deep-copy snapshot of undoable grid state
    // ════════════════════════════════════════════════════════

    /// <summary>Plain data holder for one undo/redo step. Does not reference the live context.</summary>
    internal sealed class LevelSnapshot
    {
        public CellData[] Cells;
        public VehicleImportData[] VehicleImportData;
        public int GridWidth;
        public int GridHeight;
        public bool GridActive;
        public int NextGarageId;
        public int LevelId;
        public Dictionary<int, GarageInfo> GarageMap;
        public Dictionary<int, int> GarageImportGUIDs;
        public HashSet<long> Connections;
        public ReceiverQueueResult[] GeneratedReceiverQueues;
    }

    internal static class LevelSnapshotUtil
    {
        public static LevelSnapshot Capture(LevelEditorContext ctx)
        {
            var s = new LevelSnapshot
            {
                Cells = new CellData[LevelEditorContext.MaxCellCount],
                VehicleImportData = new VehicleImportData[LevelEditorContext.MaxCellCount],
                GridWidth = ctx.GridWidth,
                GridHeight = ctx.GridHeight,
                GridActive = ctx.GridActive,
                NextGarageId = ctx.NextGarageId,
                LevelId = ctx.LevelId,
                GarageMap = new Dictionary<int, GarageInfo>(ctx.GarageMap.Count),
                GarageImportGUIDs = new Dictionary<int, int>(ctx.GarageImportGUIDs),
                Connections = new HashSet<long>(ctx.Connections),
            };

            Array.Copy(ctx.Cells, s.Cells, LevelEditorContext.MaxCellCount);
            Array.Copy(ctx.VehicleImportData, s.VehicleImportData, LevelEditorContext.MaxCellCount);

            foreach (var kv in ctx.GarageMap)
                s.GarageMap[kv.Key] = CloneGarageInfo(kv.Value);

            s.GeneratedReceiverQueues = CloneQueues(ctx.GeneratedReceiverQueues);

            return s;
        }

        public static void Restore(LevelSnapshot s, LevelEditorContext ctx)
        {
            // Cells / VehicleImportData are fixed-size (MaxCellCount) arrays: copy in place, never reallocate.
            Array.Copy(s.Cells, ctx.Cells, LevelEditorContext.MaxCellCount);
            Array.Copy(s.VehicleImportData, ctx.VehicleImportData, LevelEditorContext.MaxCellCount);

            ctx.GridWidth = s.GridWidth;
            ctx.GridHeight = s.GridHeight;
            ctx.GridActive = s.GridActive;
            ctx.NextGarageId = s.NextGarageId;
            ctx.LevelId = s.LevelId;

            // GarageMap / GarageImportGUIDs / Connections are readonly fields on the context:
            // clear + repopulate in place, never reassign the field itself.
            ctx.GarageMap.Clear();
            foreach (var kv in s.GarageMap)
                ctx.GarageMap[kv.Key] = CloneGarageInfo(kv.Value);

            ctx.GarageImportGUIDs.Clear();
            foreach (var kv in s.GarageImportGUIDs)
                ctx.GarageImportGUIDs[kv.Key] = kv.Value;

            ctx.Connections.Clear();
            foreach (long edge in s.Connections)
                ctx.Connections.Add(edge);

            // GeneratedReceiverQueues is a plain reassignable field.
            ctx.GeneratedReceiverQueues = CloneQueues(s.GeneratedReceiverQueues);

            // Post-restore reactivity, matching Generate/ClearAll/ImportFromJson convention.
            ctx.LayoutDirty = true;
            ctx.MarkStatusDirty();
            ctx.SelectTool(ToolMode.None);
        }

        static GarageInfo CloneGarageInfo(GarageInfo g)
        {
            var copy = new GarageInfo
            {
                cellX = g.cellX,
                cellY = g.cellY,
                directionType = g.directionType,
                cachedCountStr = g.cachedCountStr,
            };
            copy.carColors.AddRange(g.carColors);
            return copy;
        }

        static ReceiverQueueResult[] CloneQueues(ReceiverQueueResult[] src)
        {
            if (src == null) return null;
            var arr = new ReceiverQueueResult[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var q = src[i];
                var colorsCopy = new int[q.colorTypesQueue.Length];
                Array.Copy(q.colorTypesQueue, colorsCopy, colorsCopy.Length);
                arr[i] = new ReceiverQueueResult { queueIndex = q.queueIndex, colorTypesQueue = colorsCopy };
            }
            return arr;
        }
    }
}
