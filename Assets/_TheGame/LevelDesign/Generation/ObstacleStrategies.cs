using System.Collections.Generic;

namespace BlockLoop.LevelDesign
{
    // ════════════════════════════════════════════════════════
    //  Shared helper — reusable buffers (editor-only, single-threaded)
    // ════════════════════════════════════════════════════════

    internal static class ObstacleStrategyHelper
    {
        internal static readonly List<int> CandidatePool = new List<int>();
        internal static readonly List<int> SelectedCells = new List<int>();
        internal static readonly List<int> RemainingCandidates = new List<int>();
        internal static readonly List<int> PartialOutput = new List<int>();
        internal static readonly HashSet<int> UsedSet = new HashSet<int>();

        internal static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  Placers (density)
    // ════════════════════════════════════════════════════════

    /// <summary>Random scattered placement — Fisher-Yates shuffle, pick first N.</summary>
    internal sealed class ScatteredPlacer : IObstaclePlacer
    {
        public void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH,
            System.Random rng, List<int> output)
        {
            output.Clear();
            ObstacleStrategyHelper.Shuffle(candidateZone, rng);
            int count = System.Math.Min(maxCount, candidateZone.Count);
            for (int i = 0; i < count; i++)
                output.Add(candidateZone[i]);
        }
    }

    /// <summary>
    /// Clustered placement — BFS flood-fill from spaced seed points.
    /// Seeds are kept apart (Manhattan distance) to produce distinct blob clusters.
    /// </summary>
    internal sealed class ClusteredPlacer : IObstaclePlacer
    {
        readonly Queue<int> _frontier = new Queue<int>();
        readonly HashSet<int> _visited = new HashSet<int>();
        readonly HashSet<int> _candidateSet = new HashSet<int>();
        readonly List<int> _neighbors = new List<int>(4);
        readonly List<int> _seeds = new List<int>();

        public void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH,
            System.Random rng, List<int> output)
        {
            output.Clear();
            if (candidateZone.Count == 0 || maxCount <= 0)
                return;

            _candidateSet.Clear();
            for (int i = 0; i < candidateZone.Count; i++)
                _candidateSet.Add(candidateZone[i]);

            _frontier.Clear();
            _visited.Clear();

            int seedCount = System.Math.Max(1, System.Math.Min(3, maxCount / 8));
            int minSpacing = System.Math.Max(3, (System.Math.Min(gridW, gridH) + 1) / 2);

            ObstacleStrategyHelper.Shuffle(candidateZone, rng);
            _seeds.Clear();
            for (int i = 0; i < candidateZone.Count && _seeds.Count < seedCount; i++)
            {
                int c = candidateZone[i];
                if (IsFarEnough(c, gridW, minSpacing))
                    _seeds.Add(c);
            }

            if (_seeds.Count == 0 && candidateZone.Count > 0)
                _seeds.Add(candidateZone[0]);

            for (int s = 0; s < _seeds.Count; s++)
            {
                if (_visited.Add(_seeds[s]))
                    _frontier.Enqueue(_seeds[s]);
            }

            while (_frontier.Count > 0 && output.Count < maxCount)
            {
                int cell = _frontier.Dequeue();
                output.Add(cell);

                if (output.Count >= maxCount)
                    break;

                int cx = cell % gridW;
                int cy = cell / gridW;

                _neighbors.Clear();
                if (cx > 0) TryAddNeighbor(cell - 1);
                if (cx < gridW - 1) TryAddNeighbor(cell + 1);
                if (cy > 0) TryAddNeighbor(cell - gridW);
                if (cy < gridH - 1) TryAddNeighbor(cell + gridW);

                for (int i = _neighbors.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (_neighbors[i], _neighbors[j]) = (_neighbors[j], _neighbors[i]);
                }

                for (int i = 0; i < _neighbors.Count; i++)
                    _frontier.Enqueue(_neighbors[i]);
            }
        }

        bool IsFarEnough(int cell, int gridW, int minDist)
        {
            int cx = cell % gridW;
            int cy = cell / gridW;
            for (int i = 0; i < _seeds.Count; i++)
            {
                int sx = _seeds[i] % gridW;
                int sy = _seeds[i] / gridW;
                if (System.Math.Abs(cx - sx) + System.Math.Abs(cy - sy) < minDist)
                    return false;
            }
            return true;
        }

        void TryAddNeighbor(int idx)
        {
            if (_candidateSet.Contains(idx) && _visited.Add(idx))
                _neighbors.Add(idx);
        }
    }

    /// <summary>
    /// Line placement — random walks with directional momentum.
    /// Biases toward continuing the previous direction for smooth, flowing paths.
    /// Thinness constraint prevents lines from thickening into 2×N blobs.
    /// </summary>
    internal sealed class LinePlacer : IObstaclePlacer
    {
        const int DirNone = -1;

        readonly HashSet<int> _placed = new HashSet<int>();
        readonly HashSet<int> _candidateSet = new HashSet<int>();
        readonly List<int> _neighbors = new List<int>(4);
        readonly List<int> _seeds = new List<int>();

        public void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH,
            System.Random rng, List<int> output)
        {
            output.Clear();
            if (candidateZone.Count == 0 || maxCount <= 0)
                return;

            _candidateSet.Clear();
            for (int i = 0; i < candidateZone.Count; i++)
                _candidateSet.Add(candidateZone[i]);

            _placed.Clear();

            int lineCount = System.Math.Max(1, System.Math.Min(4, maxCount / 4));
            int perLine = (maxCount + lineCount - 1) / lineCount;

            ObstacleStrategyHelper.Shuffle(candidateZone, rng);
            _seeds.Clear();
            for (int i = 0; i < candidateZone.Count && _seeds.Count < lineCount; i++)
            {
                if (!_placed.Contains(candidateZone[i]) &&
                    CountPlacedNeighbors(candidateZone[i], gridW, gridH) == 0)
                    _seeds.Add(candidateZone[i]);
            }

            for (int s = 0; s < _seeds.Count && output.Count < maxCount; s++)
            {
                int current = _seeds[s];
                if (_placed.Contains(current))
                    continue;

                _placed.Add(current);
                output.Add(current);
                int lineTarget = System.Math.Min(output.Count + perLine - 1, maxCount);
                int lastDir = DirNone;

                while (output.Count < lineTarget)
                {
                    CollectThinNeighbors(current, gridW, gridH);

                    if (_neighbors.Count == 0)
                        break;

                    int next = PickWithMomentum(current, lastDir, gridW, rng);
                    lastDir = GetDirection(current, next, gridW);
                    current = next;
                    _placed.Add(current);
                    output.Add(current);
                }
            }
        }

        int PickWithMomentum(int current, int lastDir, int gridW, System.Random rng)
        {
            if (lastDir != DirNone && rng.Next(100) < 55)
            {
                int preferred = NeighborInDir(current, lastDir, gridW);
                if (preferred >= 0)
                {
                    for (int i = 0; i < _neighbors.Count; i++)
                    {
                        if (_neighbors[i] == preferred)
                            return preferred;
                    }
                }
            }
            return _neighbors[rng.Next(_neighbors.Count)];
        }

        static int NeighborInDir(int cell, int dir, int gridW)
        {
            switch (dir)
            {
                case 0: return cell - 1;
                case 1: return cell + 1;
                case 2: return cell - gridW;
                case 3: return cell + gridW;
                default: return -1;
            }
        }

        static int GetDirection(int from, int to, int gridW)
        {
            int diff = to - from;
            if (diff == -1) return 0;
            if (diff == 1) return 1;
            if (diff == -gridW) return 2;
            if (diff == gridW) return 3;
            return DirNone;
        }

        void CollectThinNeighbors(int cell, int gridW, int gridH)
        {
            _neighbors.Clear();
            int cx = cell % gridW;
            int cy = cell / gridW;
            if (cx > 0) TryAddThin(cell - 1, gridW, gridH);
            if (cx < gridW - 1) TryAddThin(cell + 1, gridW, gridH);
            if (cy > 0) TryAddThin(cell - gridW, gridW, gridH);
            if (cy < gridH - 1) TryAddThin(cell + gridW, gridW, gridH);
        }

        void TryAddThin(int idx, int gridW, int gridH)
        {
            if (!_candidateSet.Contains(idx) || _placed.Contains(idx))
                return;
            if (CountPlacedNeighbors(idx, gridW, gridH) <= 1)
                _neighbors.Add(idx);
        }

        int CountPlacedNeighbors(int cell, int gridW, int gridH)
        {
            int cx = cell % gridW;
            int cy = cell / gridW;
            int n = 0;
            if (cx > 0 && _placed.Contains(cell - 1)) n++;
            if (cx < gridW - 1 && _placed.Contains(cell + 1)) n++;
            if (cy > 0 && _placed.Contains(cell - gridW)) n++;
            if (cy < gridH - 1 && _placed.Contains(cell + gridW)) n++;
            return n;
        }
    }

    /// <summary>
    /// Funnel placement — creates V-shaped or inverted-V diagonal walls.
    /// Apex can point upward (arms go down) or downward (arms go up),
    /// creating channels that squeeze cube movement.
    /// </summary>
    internal sealed class FunnelPlacer : IObstaclePlacer
    {
        readonly HashSet<int> _placed = new HashSet<int>();
        readonly HashSet<int> _candidateSet = new HashSet<int>();
        readonly List<int> _edgeCandidates = new List<int>();

        public void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH,
            System.Random rng, List<int> output)
        {
            output.Clear();
            if (candidateZone.Count == 0 || maxCount <= 0)
                return;

            _candidateSet.Clear();
            for (int i = 0; i < candidateZone.Count; i++)
                _candidateSet.Add(candidateZone[i]);

            _placed.Clear();

            int funnelCount = System.Math.Max(1, System.Math.Min(3, maxCount / (gridH * 2)));

            for (int f = 0; f < funnelCount && output.Count < maxCount; f++)
            {
                bool downward = rng.Next(2) == 0;
                int dy = downward ? 1 : -1;

                _edgeCandidates.Clear();
                for (int i = 0; i < candidateZone.Count; i++)
                {
                    int cy = candidateZone[i] / gridW;
                    if (downward ? cy <= 2 : cy >= gridH - 3)
                        _edgeCandidates.Add(candidateZone[i]);
                }

                if (_edgeCandidates.Count == 0)
                {
                    for (int i = 0; i < candidateZone.Count; i++)
                        _edgeCandidates.Add(candidateZone[i]);
                }

                int apex = _edgeCandidates[rng.Next(_edgeCandidates.Count)];
                int apexX = apex % gridW;
                int apexY = apex / gridW;

                TryPlace(apex, output, maxCount);

                WalkArm(apexX, apexY, -1, dy, gridW, gridH, rng, output, maxCount);
                WalkArm(apexX, apexY, +1, dy, gridW, gridH, rng, output, maxCount);
            }
        }

        void WalkArm(int startX, int startY, int baseDx, int dy,
            int gridW, int gridH, System.Random rng, List<int> output, int maxCount)
        {
            int cx = startX;
            int y = startY + dy;
            while (y >= 0 && y < gridH && output.Count < maxCount)
            {
                int roll = rng.Next(100);
                int dx;
                if (roll < 70)
                    dx = baseDx;
                else if (roll < 90)
                    dx = 0;
                else
                    dx = baseDx * 2;

                cx += dx;
                if (cx < 0) cx = 0;
                if (cx >= gridW) cx = gridW - 1;

                TryPlace(y * gridW + cx, output, maxCount);
                y += dy;
            }
        }

        void TryPlace(int idx, List<int> output, int maxCount)
        {
            if (output.Count >= maxCount) return;
            if (!_candidateSet.Contains(idx)) return;
            if (!_placed.Add(idx)) return;
            output.Add(idx);
        }
    }

    // ════════════════════════════════════════════════════════
    //  Symmetries (mirror)
    // ════════════════════════════════════════════════════════

    /// <summary>No symmetry — full grid, no mirroring.</summary>
    internal sealed class NoneSymmetry : IObstacleSymmetry
    {
        public void BuildCandidateZone(int gridW, int gridH, List<int> output)
        {
            output.Clear();
            for (int y = 0; y < gridH; y++)
                for (int x = 0; x < gridW; x++)
                    output.Add(y * gridW + x);
        }

        public int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH)
        {
            cells[cellIndex].isObstacle = true;
            return 1;
        }
    }

    /// <summary>Horizontal symmetry — left half zone, mirror left↔right.</summary>
    internal sealed class HorizontalSymmetry : IObstacleSymmetry
    {
        public void BuildCandidateZone(int gridW, int gridH, List<int> output)
        {
            output.Clear();
            int halfW = (gridW + 1) / 2;
            for (int y = 0; y < gridH; y++)
                for (int x = 0; x < halfW; x++)
                    output.Add(y * gridW + x);
        }

        public int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH)
        {
            int x = cellIndex % gridW;
            int y = cellIndex / gridW;
            cells[cellIndex].isObstacle = true;
            int placed = 1;

            int mx = gridW - 1 - x;
            if (mx != x)
            {
                cells[y * gridW + mx].isObstacle = true;
                placed++;
            }
            return placed;
        }
    }

    /// <summary>Vertical symmetry — top half zone, mirror top↔bottom.</summary>
    internal sealed class VerticalSymmetry : IObstacleSymmetry
    {
        public void BuildCandidateZone(int gridW, int gridH, List<int> output)
        {
            output.Clear();
            int halfH = (gridH + 1) / 2;
            for (int y = 0; y < halfH; y++)
                for (int x = 0; x < gridW; x++)
                    output.Add(y * gridW + x);
        }

        public int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH)
        {
            int x = cellIndex % gridW;
            int y = cellIndex / gridW;
            cells[cellIndex].isObstacle = true;
            int placed = 1;

            int my = gridH - 1 - y;
            if (my != y)
            {
                cells[my * gridW + x].isObstacle = true;
                placed++;
            }
            return placed;
        }
    }

    /// <summary>Full symmetry — top-left quadrant zone, mirror all 4 corners.</summary>
    internal sealed class FullSymmetry : IObstacleSymmetry
    {
        public void BuildCandidateZone(int gridW, int gridH, List<int> output)
        {
            output.Clear();
            int halfW = (gridW + 1) / 2;
            int halfH = (gridH + 1) / 2;
            for (int y = 0; y < halfH; y++)
                for (int x = 0; x < halfW; x++)
                    output.Add(y * gridW + x);
        }

        public int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH)
        {
            int cx = cellIndex % gridW;
            int cy = cellIndex / gridW;
            int mx = gridW - 1 - cx;
            int my = gridH - 1 - cy;

            cells[cy * gridW + cx].isObstacle = true;
            int placed = 1;

            if (mx != cx)
            {
                cells[cy * gridW + mx].isObstacle = true;
                placed++;
            }
            if (my != cy)
            {
                cells[my * gridW + cx].isObstacle = true;
                placed++;
            }
            if (mx != cx && my != cy)
            {
                cells[my * gridW + mx].isObstacle = true;
                placed++;
            }
            return placed;
        }
    }
}
