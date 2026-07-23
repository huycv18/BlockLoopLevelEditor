using System;
using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class DensitySection : IGenerateSection
    {
        const int DensityCount = 4;
        const float RowHeight = 28f;
        const float LabelWidth = 68f;

        static readonly GUIContent[] s_names =
        {
            new GUIContent("Scattered"),
            new GUIContent("Clustered"),
            new GUIContent("Line"),
            new GUIContent("Funnel"),
        };

        readonly LevelEditorContext _ctx;
        readonly Action _requestRepaint;
        readonly string[] _fieldWeights = { "", "", "", "" };
        readonly GUIContent _statusContent = new GUIContent();

        public DensitySection(LevelEditorContext ctx, Action requestRepaint)
        {
            _ctx = ctx;
            _requestRepaint = requestRepaint;
        }

        public string Title => "Density";

        public float MeasureHeight(float cw)
        {
            return DensityCount * (RowHeight + RightPanelHelpers.RowSpacing)
                + RightPanelHelpers.WeightStatusHeight
                + RightPanelHelpers.RowSpacing;
        }

        public float Draw(float x, float y, float cw)
        {
            EnsureStyles();
            var cfg = _ctx.GenerateConfig;

            for (int i = 0; i < DensityCount; i++)
            {
                GUI.Label(new Rect(x, y, LabelWidth, RowHeight), s_names[i], RightPanelHelpers.LabelStyle);

                float sliderX = x + LabelWidth + 4f;
                float sliderW = cw - LabelWidth - 4f;
                string fStr = _fieldWeights[i];
                cfg.DensityWeights[i] = RightPanelHelpers.SliderWithField(
                    sliderX, y, sliderW, RowHeight,
                    cfg.DensityWeights[i], 0f, 1f, ref fStr, _requestRepaint);
                _fieldWeights[i] = fStr;

                y += RowHeight + RightPanelHelpers.RowSpacing;
            }

            float totalW = 0f;
            for (int i = 0; i < cfg.DensityWeights.Length; i++)
                totalW += cfg.DensityWeights[i];

            var barRect = new Rect(x, y, cw, RightPanelHelpers.WeightStatusHeight);
            RightPanelHelpers.DrawWeightStatusBar(barRect, totalW, _statusContent);
            y += RightPanelHelpers.WeightStatusHeight + RightPanelHelpers.RowSpacing;

            return y;
        }

        static void EnsureStyles() => RightPanelHelpers.EnsureStyles();
    }
}
