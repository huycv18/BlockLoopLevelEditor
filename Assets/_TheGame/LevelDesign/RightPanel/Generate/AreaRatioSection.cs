using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class AreaRatioSection : IGenerateSection
    {
        static readonly GUIContent s_hint = new GUIContent("0–100");
        static readonly GUIContent s_minLabel = new GUIContent("Min");
        static readonly GUIContent s_maxLabel = new GUIContent("Max");

        readonly LevelEditorContext _ctx;
        string _fieldMin = "";
        string _fieldMax = "";

        public AreaRatioSection(LevelEditorContext ctx) => _ctx = ctx;

        public string Title => "Area Ratio";

        public float MeasureHeight(float cw)
        {
            return RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;
        }

        public float Draw(float x, float y, float cw)
        {
            var cfg = _ctx.GenerateConfig;
            float halfW = (cw - RightPanelHelpers.RowSpacing) * 0.5f;
            float labelW = 30f;
            float fieldW = halfW - labelW - 4f;

            GUI.Label(new Rect(x, y, labelW, RightPanelHelpers.RowHeight), s_minLabel, RightPanelHelpers.LabelStyle);
            var minRect = new Rect(x + labelW + 4f, y, fieldW, RightPanelHelpers.RowHeight);
            cfg.ObstacleMinPercent = RightPanelHelpers.IntFieldWithHint(minRect, ref _fieldMin,
                cfg.ObstacleMinPercent, 0, 100, s_hint);

            float maxX = x + halfW + RightPanelHelpers.RowSpacing;
            GUI.Label(new Rect(maxX, y, labelW, RightPanelHelpers.RowHeight), s_maxLabel, RightPanelHelpers.LabelStyle);
            var maxRect = new Rect(maxX + labelW + 4f, y, fieldW, RightPanelHelpers.RowHeight);
            cfg.ObstacleMaxPercent = RightPanelHelpers.IntFieldWithHint(maxRect, ref _fieldMax,
                cfg.ObstacleMaxPercent, cfg.ObstacleMinPercent, 100, s_hint);

            return y + RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;
        }
    }
}
