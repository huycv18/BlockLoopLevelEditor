using System;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class GridSizeGroup : IRightPanelGroup
    {
        static readonly Color s_accent = new Color(0.45f, 0.75f, 0.95f, 1f);
        static readonly GUIContent s_hintGridSize = new GUIContent("2–25");

        readonly LevelEditorContext _ctx;
        readonly Action<int, int> _onGenerateGrid;

        string _fieldWidth = "";
        string _fieldHeight = "";

        public GridSizeGroup(LevelEditorContext ctx, Action<int, int> onGenerateGrid)
        {
            _ctx = ctx;
            _onGenerateGrid = onGenerateGrid;
        }

        public string FieldWidth
        {
            get => _fieldWidth;
            set => _fieldWidth = value;
        }

        public string FieldHeight
        {
            get => _fieldHeight;
            set => _fieldHeight = value;
        }

        public float MeasureHeight(float panelWidth)
        {
            float contentH = RightPanelHelpers.RowHeight * 2
                + RightPanelHelpers.RowSpacing
                + RightPanelHelpers.ButtonSpacing
                + RightPanelHelpers.ButtonHeight;
            return RightPanelHelpers.MeasureRightGroup(contentH);
        }

        public float Draw(float startY, float panelWidth)
        {
            RightPanelHelpers.EnsureStyles();

            float contentH = RightPanelHelpers.RowHeight * 2
                + RightPanelHelpers.RowSpacing
                + RightPanelHelpers.ButtonSpacing
                + RightPanelHelpers.ButtonHeight;
            float nextY = RightPanelHelpers.BeginRightGroup(startY, panelWidth,
                "Grid Size", s_accent, contentH, out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cw = content.width - LevelEditorStyles.GroupInnerPadding * 2f;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;

            GUI.Label(new Rect(cx, cy, RightPanelHelpers.LabelWidth, RightPanelHelpers.RowHeight),
                "Width", LevelEditorStyles.PanelLabelStyle);
            var widthRect = new Rect(cx + RightPanelHelpers.LabelWidth + 4f, cy,
                cw - RightPanelHelpers.LabelWidth - 4f, RightPanelHelpers.RowHeight);
            RightPanelHelpers.IntFieldWithHint(widthRect, ref _fieldWidth, LevelEditorContext.DefaultGridSize,
                LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize, s_hintGridSize);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;

            GUI.Label(new Rect(cx, cy, RightPanelHelpers.LabelWidth, RightPanelHelpers.RowHeight),
                "Height", LevelEditorStyles.PanelLabelStyle);
            var heightRect = new Rect(cx + RightPanelHelpers.LabelWidth + 4f, cy,
                cw - RightPanelHelpers.LabelWidth - 4f, RightPanelHelpers.RowHeight);
            RightPanelHelpers.IntFieldWithHint(heightRect, ref _fieldHeight, LevelEditorContext.DefaultGridSize,
                LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize, s_hintGridSize);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.ButtonSpacing;

            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                "Generate Grid", RightPanelHelpers.ButtonStyle))
            {
                int nw = RightPanelHelpers.ParseFieldInt(_fieldWidth, LevelEditorContext.DefaultGridSize,
                    LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize);
                int nh = RightPanelHelpers.ParseFieldInt(_fieldHeight, LevelEditorContext.DefaultGridSize,
                    LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize);
                _onGenerateGrid?.Invoke(nw, nh);
            }

            return nextY;
        }
    }
}
