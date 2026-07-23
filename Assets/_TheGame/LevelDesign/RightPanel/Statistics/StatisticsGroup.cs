using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class StatisticsGroup : IRightPanelGroup
    {
        internal static readonly Color s_accent = new Color(0.80f, 0.70f, 0.40f, 1f);
        internal static Color Accent => s_accent;
        const float WidgetSpacing = 8f;

        readonly IStatisticsWidget[] _widgets;

        public StatisticsGroup(IStatisticsWidget[] widgets)
        {
            _widgets = widgets;
        }

        float ComputeInnerHeight(float panelWidth)
        {
            float h = 0f;
            for (int i = 0; i < _widgets.Length; i++)
            {
                if (i > 0) h += WidgetSpacing;
                h += _widgets[i].MeasureHeight(panelWidth);
            }
            return h;
        }

        public float MeasureHeight(float panelWidth)
        {
            return RightPanelHelpers.MeasureRightGroup(ComputeInnerHeight(panelWidth));
        }

        public void Rebuild(LevelEditorContext ctx)
        {
            for (int i = 0; i < _widgets.Length; i++)
                _widgets[i].Rebuild(ctx);
        }

        public float Draw(float startY, float pw)
        {
            float inner = ComputeInnerHeight(pw);
            float nextY = RightPanelHelpers.BeginRightGroup(startY, pw, "Statistics", s_accent, inner, out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cw = content.width - LevelEditorStyles.GroupInnerPadding * 2f;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;

            for (int i = 0; i < _widgets.Length; i++)
            {
                if (i > 0) cy += WidgetSpacing;
                _widgets[i].Draw(cx, cy, cw);
                cy += _widgets[i].MeasureHeight(cw);
            }

            return nextY;
        }
    }
}
