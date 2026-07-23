using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class SymmetrySection : IGenerateSection
    {
        static readonly string[] s_labels = { "None", "Horizontal ↔", "Vertical ↕", "Both ✦" };

        readonly LevelEditorContext _ctx;

        public SymmetrySection(LevelEditorContext ctx) => _ctx = ctx;

        public string Title => "Symmetry";

        public float MeasureHeight(float cw)
        {
            return RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;
        }

        public float Draw(float x, float y, float cw)
        {
            var cfg = _ctx.GenerateConfig;
            cfg.SymmetryMode = EditorGUI.Popup(
                new Rect(x, y, cw, RightPanelHelpers.RowHeight),
                cfg.SymmetryMode, s_labels, RightPanelHelpers.PopupStyle);

            return y + RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;
        }
    }
}
