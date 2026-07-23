using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal static class LevelGenerator
    {
        static readonly IObstaclePlacer[] s_placers =
        {
            new ScatteredPlacer(),    // bit 0
            new ClusteredPlacer(),    // bit 1
            new LinePlacer(),         // bit 2
            new FunnelPlacer(),       // bit 3
        };

        static readonly IObstacleSymmetry[] s_symmetries =
        {
            new NoneSymmetry(),           // 0 = None
            new HorizontalSymmetry(),     // 1 = Horizontal
            new VerticalSymmetry(),       // 2 = Vertical
            new FullSymmetry(),           // 3 = Both
        };

        public static void Generate(LevelEditorContext ctx, GenerateMode mode = GenerateMode.All)
        {
            var cfg = ctx.GenerateConfig;
            var rng = new System.Random();
            int gridW = ctx.GridWidth;
            int gridH = ctx.GridHeight;
            int total = gridW * gridH;

            if (mode == GenerateMode.All)
            {
                GenerateAll(ctx, cfg, rng, gridW, gridH, total);
            }
            else if (mode == GenerateMode.ObstaclesOnly)
            {
                GenerateObstacles(ctx, cfg, rng, gridW, gridH, total);
            }
            else
            {
                GenerateColors(ctx, cfg, rng, gridW, gridH, total);
            }

            ctx.GridActive = true;
            ctx.LayoutDirty = true;
            ctx.MarkStatusDirty();
        }

        static void GenerateAll(LevelEditorContext ctx, LevelGenerateConfig cfg,
            System.Random rng, int gridW, int gridH, int total)
        {
            // Clear all cell data
            for (int i = 0; i < total; i++)
            {
                ctx.Cells[i].colorId = -1;
                ctx.Cells[i].isObstacle = false;
                ctx.Cells[i].isHidden = false;
                ctx.Cells[i].garageId = -1;
                ctx.VehicleImportData[i] = default;
            }

            ctx.GarageMap.Clear();
            ctx.NextGarageId = 0;
            ctx.GarageImportGUIDs.Clear();
            ctx.Connections.Clear();

            PlaceObstacles(ctx, cfg, rng, gridW, gridH, total);
            FillColors(ctx, cfg, rng, total);
        }

        static void GenerateObstacles(LevelEditorContext ctx, LevelGenerateConfig cfg,
            System.Random rng, int gridW, int gridH, int total)
        {
            // Clear only obstacle flags — preserve colors, garages, connections
            for (int i = 0; i < total; i++)
                ctx.Cells[i].isObstacle = false;

            PlaceObstacles(ctx, cfg, rng, gridW, gridH, total);

            for (int i = 0; i < total; i++)
            {
                if (ctx.Cells[i].isObstacle)
                {
                    // Don't place obstacles on garage cells
                    if (ctx.Cells[i].garageId >= 0)
                        ctx.Cells[i].isObstacle = false;
                    else
                        ctx.Cells[i].colorId = -1;
                }
            }
        }

        static void GenerateColors(LevelEditorContext ctx, LevelGenerateConfig cfg,
            System.Random rng, int gridW, int gridH, int total)
        {
            // Clear colors on non-obstacle cells only — preserve obstacles, garages, connections
            for (int i = 0; i < total; i++)
            {
                if (!ctx.Cells[i].isObstacle && ctx.Cells[i].garageId < 0)
                    ctx.Cells[i].colorId = -1;
            }

            FillColors(ctx, cfg, rng, total);
        }

        static void PlaceObstacles(LevelEditorContext ctx, LevelGenerateConfig cfg,
            System.Random rng, int gridW, int gridH, int total)
        {
            int minPct = Mathf.Clamp(cfg.ObstacleMinPercent, 0, 100);
            int maxPct = Mathf.Clamp(cfg.ObstacleMaxPercent, minPct, 100);
            int minObs = total * minPct / 100;
            int maxObs = total * maxPct / 100;
            int targetObs = minObs >= maxObs ? minObs : rng.Next(minObs, maxObs + 1);

            var symmetry = s_symmetries[Mathf.Clamp(cfg.SymmetryMode, 0, s_symmetries.Length - 1)];

            var candidatePool = ObstacleStrategyHelper.CandidatePool;
            symmetry.BuildCandidateZone(gridW, gridH, candidatePool);

            int avgMirror = EstimateAvgMirror(symmetry, candidatePool, ctx.Cells, gridW, gridH);
            int maxPicks = avgMirror > 0 ? (targetObs + avgMirror - 1) / avgMirror : targetObs;

            float totalDW = 0f;
            for (int i = 0; i < cfg.DensityWeights.Length; i++)
                totalDW += Mathf.Max(0f, cfg.DensityWeights[i]);
            if (totalDW <= 0f) totalDW = 1f;

            int lastActive = -1;
            int activeCount = 0;
            for (int i = 0; i < s_placers.Length; i++)
            {
                if (cfg.DensityWeights[i] > 0f)
                {
                    lastActive = i;
                    activeCount++;
                }
            }

            var selected = ObstacleStrategyHelper.SelectedCells;
            selected.Clear();

            if (activeCount <= 1)
            {
                int idx = lastActive >= 0 ? lastActive : 0;
                s_placers[idx].SelectCells(candidatePool, maxPicks, gridW, gridH, rng, selected);
            }
            else
            {
                var remaining = ObstacleStrategyHelper.RemainingCandidates;
                remaining.Clear();
                for (int i = 0; i < candidatePool.Count; i++)
                    remaining.Add(candidatePool[i]);

                var usedSet = ObstacleStrategyHelper.UsedSet;
                var partialOutput = ObstacleStrategyHelper.PartialOutput;
                int assigned = 0;

                for (int i = 0; i < s_placers.Length; i++)
                {
                    float w = Mathf.Max(0f, cfg.DensityWeights[i]);
                    if (w <= 0f) continue;

                    int share = i == lastActive
                        ? maxPicks - assigned
                        : Mathf.RoundToInt(maxPicks * w / totalDW);

                    if (share <= 0) continue;

                    s_placers[i].SelectCells(remaining, share, gridW, gridH, rng, partialOutput);

                    usedSet.Clear();
                    for (int j = 0; j < partialOutput.Count; j++)
                    {
                        selected.Add(partialOutput[j]);
                        usedSet.Add(partialOutput[j]);
                    }
                    assigned += partialOutput.Count;

                    int write = 0;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        if (!usedSet.Contains(remaining[j]))
                            remaining[write++] = remaining[j];
                    }
                    remaining.RemoveRange(write, remaining.Count - write);
                }
            }

            int placed = 0;
            for (int i = 0; i < selected.Count && placed < targetObs; i++)
                placed += symmetry.ApplyMirror(selected[i], ctx.Cells, gridW, gridH);

            EnsureReachability(ctx.Cells, gridW, gridH, rng);
        }

        static bool[] s_reachable;
        static readonly System.Collections.Generic.Queue<int> s_bfsQueue
            = new System.Collections.Generic.Queue<int>();
        static readonly System.Collections.Generic.List<int> s_bridgeCandidates
            = new System.Collections.Generic.List<int>();

        static bool IsFree(CellData[] cells, int idx)
        {
            return !cells[idx].isObstacle && cells[idx].garageId < 0;
        }

        static void EnsureReachability(CellData[] cells, int gridW, int gridH, System.Random rng)
        {
            int total = gridW * gridH;
            if (s_reachable == null || s_reachable.Length < total)
                s_reachable = new bool[total];

            while (true)
            {
                System.Array.Clear(s_reachable, 0, total);
                s_bfsQueue.Clear();

                for (int x = 0; x < gridW; x++)
                {
                    if (IsFree(cells, x))
                    {
                        s_reachable[x] = true;
                        s_bfsQueue.Enqueue(x);
                    }
                }

                while (s_bfsQueue.Count > 0)
                {
                    int cell = s_bfsQueue.Dequeue();
                    int cx = cell % gridW;
                    int cy = cell / gridW;

                    if (cx > 0) TryEnqueueFree(cells, cell - 1);
                    if (cx < gridW - 1) TryEnqueueFree(cells, cell + 1);
                    if (cy > 0) TryEnqueueFree(cells, cell - gridW);
                    if (cy < gridH - 1) TryEnqueueFree(cells, cell + gridW);
                }

                int trappedIdx = -1;
                for (int i = 0; i < total; i++)
                {
                    if (IsFree(cells, i) && !s_reachable[i])
                    {
                        trappedIdx = i;
                        break;
                    }
                }

                if (trappedIdx < 0)
                    break;

                s_bridgeCandidates.Clear();
                for (int i = 0; i < total; i++)
                {
                    if (!cells[i].isObstacle) continue;

                    int cx = i % gridW;
                    int cy = i / gridW;
                    bool hasReachable = false;
                    bool hasTrapped = false;

                    if (cx > 0) CheckNeighbor(cells, i - 1, ref hasReachable, ref hasTrapped);
                    if (cx < gridW - 1) CheckNeighbor(cells, i + 1, ref hasReachable, ref hasTrapped);
                    if (cy > 0) CheckNeighbor(cells, i - gridW, ref hasReachable, ref hasTrapped);
                    if (cy < gridH - 1) CheckNeighbor(cells, i + gridW, ref hasReachable, ref hasTrapped);

                    if (hasReachable && hasTrapped)
                        s_bridgeCandidates.Add(i);
                }

                if (s_bridgeCandidates.Count > 0)
                {
                    int pick = s_bridgeCandidates[rng.Next(s_bridgeCandidates.Count)];
                    cells[pick].isObstacle = false;
                }
                else
                {
                    int tx = trappedIdx % gridW;
                    int ty = trappedIdx / gridW;
                    s_bridgeCandidates.Clear();
                    if (tx > 0 && cells[trappedIdx - 1].isObstacle)
                        s_bridgeCandidates.Add(trappedIdx - 1);
                    if (tx < gridW - 1 && cells[trappedIdx + 1].isObstacle)
                        s_bridgeCandidates.Add(trappedIdx + 1);
                    if (ty > 0 && cells[trappedIdx - gridW].isObstacle)
                        s_bridgeCandidates.Add(trappedIdx - gridW);
                    if (ty < gridH - 1 && cells[trappedIdx + gridW].isObstacle)
                        s_bridgeCandidates.Add(trappedIdx + gridW);

                    if (s_bridgeCandidates.Count > 0)
                    {
                        int pick = s_bridgeCandidates[rng.Next(s_bridgeCandidates.Count)];
                        cells[pick].isObstacle = false;
                    }
                }
            }
        }

        static void TryEnqueueFree(CellData[] cells, int idx)
        {
            if (!s_reachable[idx] && IsFree(cells, idx))
            {
                s_reachable[idx] = true;
                s_bfsQueue.Enqueue(idx);
            }
        }

        static void CheckNeighbor(CellData[] cells, int idx,
            ref bool hasReachable, ref bool hasTrapped)
        {
            if (IsFree(cells, idx))
            {
                if (s_reachable[idx]) hasReachable = true;
                else hasTrapped = true;
            }
        }

        static void FillColors(LevelEditorContext ctx, LevelGenerateConfig cfg,
            System.Random rng, int total)
        {
            var buffer = cfg.CellIndexBuffer;
            buffer.Clear();
            for (int i = 0; i < total; i++)
            {
                if (!ctx.Cells[i].isObstacle && ctx.Cells[i].garageId < 0)
                    buffer.Add(i);
            }

            if (cfg.ColorWeights.Count == 0 || buffer.Count == 0)
                return;

            ObstacleStrategyHelper.Shuffle(buffer, rng);

            int totalWeight = 0;
            for (int i = 0; i < cfg.ColorWeights.Count; i++)
                totalWeight += cfg.ColorWeights[i].weight;

            if (totalWeight <= 0)
                return;

            int nonObsCount = buffer.Count;
            int assigned = 0;

            for (int c = 0; c < cfg.ColorWeights.Count; c++)
            {
                var entry = cfg.ColorWeights[c];
                float w = entry.weight;

                int count;
                if (c == cfg.ColorWeights.Count - 1)
                    count = nonObsCount - assigned;
                else
                    count = Mathf.RoundToInt(nonObsCount * w / totalWeight);

                int end = Mathf.Min(assigned + count, nonObsCount);
                for (int i = assigned; i < end; i++)
                    ctx.Cells[buffer[i]].colorId = entry.materialId;

                assigned = end;
            }
        }

        static int EstimateAvgMirror(IObstacleSymmetry symmetry, System.Collections.Generic.List<int> candidates,
            CellData[] cells, int gridW, int gridH)
        {
            if (candidates.Count == 0)
                return 1;

            int testIdx = candidates[candidates.Count / 2];
            int tx = testIdx % gridW;
            int ty = testIdx / gridW;
            int mx = gridW - 1 - tx;
            int my = gridH - 1 - ty;

            int count = 1;
            if (symmetry is HorizontalSymmetry && mx != tx) count = 2;
            else if (symmetry is VerticalSymmetry && my != ty && my > 0) count = 2;
            else if (symmetry is FullSymmetry)
            {
                count = 1;
                if (mx != tx) count++;
                if (my != ty && my > 0) count++;
                if (mx != tx && my != ty && my > 0) count++;
            }

            return count;
        }
    }
}
