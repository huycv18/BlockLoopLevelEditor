using System.Collections.Generic;

namespace BlockLoop.LevelDesign
{
    /// <summary>
    /// Selects obstacle positions from a candidate zone (density strategy).
    /// Implementations control whether obstacles are scattered or clustered.
    /// </summary>
    internal interface IObstaclePlacer
    {
        /// <summary>
        /// Select up to maxCount cell indices from candidateZone.
        /// Results are written into output (cleared first).
        /// candidateZone contains valid cell indices already filtered by symmetry zone.
        /// </summary>
        void SelectCells(List<int> candidateZone, int maxCount, int gridW, int gridH,
            System.Random rng, List<int> output);
    }
}
