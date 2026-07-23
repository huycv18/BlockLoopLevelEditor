using System.Collections.Generic;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    // ════════════════════════════════════════════════════════
    //  Enums
    // ════════════════════════════════════════════════════════

    internal enum ToolMode { None, PaintColor, Eraser, PaintObstacle, ToggleHidden, PlaceGarage, LinkCube, Select }

    internal enum GenerateMode
    {
        All,
        ObstaclesOnly,
        ColorsOnly,
    }

    // ════════════════════════════════════════════════════════
    //  Data types
    // ════════════════════════════════════════════════════════

    internal struct CellData
    {
        public int colorId;
        public bool isObstacle;
        public bool isHidden;
        public int garageId;
    }

    internal struct PaletteEntry
    {
        public int materialId;
        public Color color;
    }

    internal class GarageInfo
    {
        public int cellX, cellY;
        public int directionType;
        public readonly List<int> carColors = new List<int>();
        public string cachedCountStr = "0";
    }

    internal struct VehicleImportData
    {
        public bool hasData;
        public bool hasIce;
        public int iceCount;
        public int directionMode;
    }

    // ════════════════════════════════════════════════════════
    //  Generate Random config
    // ════════════════════════════════════════════════════════

    internal sealed class LevelGenerateConfig
    {
        public int ObstacleMinPercent = 10;
        public int ObstacleMaxPercent = 25;

        /// <summary>0=None, 1=Horizontal(left↔right), 2=Vertical(top↔bottom), 3=Both</summary>
        public int SymmetryMode;

        /// <summary>Weight per density mode: [0]=Scattered, [1]=Clustered, [2]=Line, [3]=Funnel</summary>
        public readonly float[] DensityWeights = { 1f, 0f, 0f, 0f };

        public struct ColorWeightEntry
        {
            public int materialId;
            public int weight;
        }

        public readonly List<ColorWeightEntry> ColorWeights = new List<ColorWeightEntry>();

        /// <summary>Reusable buffer for generation — avoids allocation per generate call.</summary>
        public readonly List<int> CellIndexBuffer = new List<int>();

        /// <summary>Whether the inline add-color palette is shown.</summary>
        public bool ShowAddColorPalette;
    }

    // ════════════════════════════════════════════════════════
    //  Receiver queue generation result
    // ════════════════════════════════════════════════════════

    internal struct ReceiverQueueResult
    {
        public int queueIndex;
        public int[] colorTypesQueue;
    }
}
