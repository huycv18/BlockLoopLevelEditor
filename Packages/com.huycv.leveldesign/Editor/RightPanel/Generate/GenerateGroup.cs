using System;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class GenerateGroup : IRightPanelGroup
    {
        static readonly Color s_accent = new Color(0.65f, 0.45f, 0.85f, 1f);
        const float CategorySpacing = 6f;

        static readonly GUIContent s_genObstaclesBtn = new GUIContent("Generate Obstacles");
        static readonly GUIContent s_genColorsBtn = new GUIContent("Generate Colors");
        static readonly GUIContent s_genAllBtn = new GUIContent("Generate All");

        readonly GenerateCategory _obstacleCategory;
        readonly GenerateCategory _colorCategory;
        readonly Action<GenerateMode> _onGenerate;

        public GenerateGroup(LevelEditorContext ctx, Action<GenerateMode> onGenerate, Action requestRepaint)
        {
            _onGenerate = onGenerate;

            _obstacleCategory = new GenerateCategory("Obstacle", s_accent, new IGenerateSection[]
            {
                new AreaRatioSection(ctx),
                new SymmetrySection(ctx),
                new DensitySection(ctx, requestRepaint),
            });

            _colorCategory = new GenerateCategory("Color", s_accent, new IGenerateSection[]
            {
                new ColorWeightSection(ctx, requestRepaint),
            });
        }

        float ComputeContentHeight(float cw)
        {
            float h = 0f;
            h += _obstacleCategory.MeasureHeight(cw);
            h += CategorySpacing;
            h += _colorCategory.MeasureHeight(cw);
            h += CategorySpacing;
            h += RightPanelHelpers.ButtonHeight * 3 + RightPanelHelpers.ButtonSpacing * 2;
            return h;
        }

        public float MeasureHeight(float panelWidth)
        {
            float cw = panelWidth - LevelEditorStyles.PanelPadding * 2f
                - LevelEditorStyles.GroupInnerPadding * 2f;
            return RightPanelHelpers.MeasureRightGroup(ComputeContentHeight(cw));
        }

        public float Draw(float startY, float panelWidth)
        {
            RightPanelHelpers.EnsureStyles();

            float cw = panelWidth - LevelEditorStyles.PanelPadding * 2f
                - LevelEditorStyles.GroupInnerPadding * 2f;
            float contentH = ComputeContentHeight(cw);
            float nextY = RightPanelHelpers.BeginRightGroup(startY, panelWidth,
                "Generate Random", s_accent, contentH, out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;

            cy = _obstacleCategory.Draw(cx, cy, cw);
            cy += CategorySpacing;

            cy = _colorCategory.Draw(cx, cy, cw);
            cy += CategorySpacing;

            // Generate buttons
            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                s_genObstaclesBtn, RightPanelHelpers.ButtonStyle))
                _onGenerate?.Invoke(GenerateMode.ObstaclesOnly);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                s_genColorsBtn, RightPanelHelpers.ButtonStyle))
                _onGenerate?.Invoke(GenerateMode.ColorsOnly);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                s_genAllBtn, RightPanelHelpers.ButtonStyle))
                _onGenerate?.Invoke(GenerateMode.All);

            return nextY;
        }
    }
}
