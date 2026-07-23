using System.Collections.Generic;

namespace BlockLoop.LevelDesign
{
    /// <summary>
    /// Defines the symmetry zone and mirror logic for obstacle placement.
    /// Implementations control which quadrant/half is the "source" zone and how positions are mirrored.
    /// </summary>
    internal interface IObstacleSymmetry
    {
        /// <summary>
        /// Build the candidate zone — the set of cell indices where obstacles can be placed
        /// before mirroring. Output is cleared then filled.
        /// </summary>
        void BuildCandidateZone(int gridW, int gridH, List<int> output);

        /// <summary>
        /// Place an obstacle at cellIndex and all its mirror positions.
        /// Returns the number of obstacles actually placed (1–4 depending on symmetry and axis overlap).
        /// </summary>
        int ApplyMirror(int cellIndex, CellData[] cells, int gridW, int gridH);
    }
}
