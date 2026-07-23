using System;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class ReceiverQueuesGroup : IRightPanelGroup
    {
        static readonly Color s_accent = new Color(0.40f, 0.80f, 0.75f, 1f);

        const float LocalLabelWidth = 100f;

        readonly LevelEditorContext _ctx;
        readonly Action<int, int> _onGenerate;

        string _fieldQueuesAmount = "";
        string _fieldClearRatio = "3";

        public ReceiverQueuesGroup(LevelEditorContext ctx, Action<int, int> onGenerate)
        {
            _ctx = ctx;
            _onGenerate = onGenerate;
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
                "Generate Receiver Queues", s_accent, contentH, out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cw = content.width - LevelEditorStyles.GroupInnerPadding * 2f;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;

            // Row 1: Queues Amount
            GUI.Label(new Rect(cx, cy, LocalLabelWidth, RightPanelHelpers.RowHeight),
                "Queues Amount", LevelEditorStyles.PanelLabelStyle);
            var queuesRect = new Rect(cx + LocalLabelWidth + 4f, cy,
                cw - LocalLabelWidth - 4f, RightPanelHelpers.RowHeight);
            RightPanelHelpers.IntFieldWithHint(queuesRect, ref _fieldQueuesAmount, 1,
                1, 99, GUIContent.none);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;

            // Row 2: Clear Ratio
            GUI.Label(new Rect(cx, cy, LocalLabelWidth, RightPanelHelpers.RowHeight),
                "Clear Ratio", LevelEditorStyles.PanelLabelStyle);
            var ratioRect = new Rect(cx + LocalLabelWidth + 4f, cy,
                cw - LocalLabelWidth - 4f, RightPanelHelpers.RowHeight);
            RightPanelHelpers.IntFieldWithHint(ratioRect, ref _fieldClearRatio, 3,
                1, 99, GUIContent.none);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.ButtonSpacing;

            // Button
            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                "Generate Receiver Queues", RightPanelHelpers.ButtonStyle))
            {
                int queues = RightPanelHelpers.ParseFieldInt(_fieldQueuesAmount, 1, 1, 99);
                int ratio = RightPanelHelpers.ParseFieldInt(_fieldClearRatio, 3, 1, 99);
                _onGenerate?.Invoke(queues, ratio);
            }

            return nextY;
        }
    }
}
