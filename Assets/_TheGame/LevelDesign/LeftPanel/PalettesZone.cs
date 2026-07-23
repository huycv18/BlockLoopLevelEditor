using UnityEngine;

namespace BlockLoop.LevelDesign
{
    /// <summary>
    /// Zone "Palettes" — delegation container cho các palette ToolGroups (ColorToolGroup, v.v.).
    /// Mỗi tool tự vẽ qua DrawPanel/MeasureHeight, zone chỉ vẽ header và indent.
    /// </summary>
    internal sealed class PalettesZone : ILeftPanelZone
    {
        readonly ToolGroup[] _paletteTools;

        public PalettesZone(ToolGroup[] paletteTools)
        {
            _paletteTools = paletteTools;
        }

        // ════════════════════════════════════════════════════════
        //  ILeftPanelZone
        // ════════════════════════════════════════════════════════

        public float MeasureHeight(float panelWidth)
        {
            float childW = panelWidth - LeftPanelHelpers.ZoneChildIndent;
            float h = LevelEditorStyles.GroupTitleHeight;
            for (int i = 0; i < _paletteTools.Length; i++)
                h += _paletteTools[i].MeasureHeight(childW);
            return h;
        }

        public float Draw(float startY, float panelWidth)
        {
            LevelEditorStyles.EnsureStyles();

            // Zone header
            float contentStartY = LeftPanelHelpers.BeginLeftZoneHeader(startY, panelWidth,
                LeftPanelHelpers.PalettesZoneTitle, LeftPanelHelpers.AccentPalettesZone);

            // Sub-groups indented
            float indent = LeftPanelHelpers.ZoneChildIndent;
            float childW = panelWidth - indent;
            float titleH = contentStartY - startY;
            float y = titleH;

            GUI.BeginGroup(new Rect(indent, startY, childW, 2000f));
            for (int i = 0; i < _paletteTools.Length; i++)
                y = _paletteTools[i].DrawPanel(y, childW);
            GUI.EndGroup();

            return startY + y;
        }
    }
}
