using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    /// <summary>
    /// Zone "Tools" — compact matrix of single-action toggle tools (Obstacle, Hidden, Garage, Connection).
    /// Mỗi tool hiển thị 1 swatch 66×66 trong grid layout, kèm title label phía trên.
    /// </summary>
    internal sealed class ToolsZone : ILeftPanelZone
    {
        readonly LevelEditorContext _ctx;
        readonly ToolGroup[] _toggleTools;

        // ── Pre-allocated per-tool arrays (allocated once in ctor) ──
        readonly GUIContent[] _titles;
        readonly float[] _labelHeights;

        // ── Layout cache (rebuilt when panel width changes) ──
        float _cachedPanelWidth = -1f;
        int _cols;
        float _contentH;
        float _totalH;
        float[] _rowHeights;

        public ToolsZone(LevelEditorContext ctx, ToolGroup[] toggleTools)
        {
            _ctx = ctx;
            _toggleTools = toggleTools;

            _titles = new GUIContent[toggleTools.Length];
            _labelHeights = new float[toggleTools.Length];
            for (int i = 0; i < toggleTools.Length; i++)
                _titles[i] = new GUIContent(toggleTools[i].Title);
        }

        // ════════════════════════════════════════════════════════
        //  ILeftPanelZone
        // ════════════════════════════════════════════════════════

        public float MeasureHeight(float panelWidth)
        {
            EnsureLayout(panelWidth);
            return _totalH;
        }

        public float Draw(float startY, float panelWidth)
        {
            LeftPanelHelpers.EnsureStyles();
            EnsureLayout(panelWidth);

            float pad = LevelEditorStyles.PanelPadding;
            float innerPad = LevelEditorStyles.GroupInnerPadding;
            float w = panelWidth - pad * 2f;

            // Header
            float contentStartY = LeftPanelHelpers.BeginLeftZoneHeader(startY, panelWidth,
                LeftPanelHelpers.ToolsZoneTitle, LeftPanelHelpers.AccentToolsZone);

            // Content area
            float contentH = _contentH + innerPad * 2f;
            var contentRect = new Rect(pad, contentStartY, w, contentH);
            EditorGUI.DrawRect(contentRect, LevelEditorStyles.GroupContentBgColor);

            // Swatch matrix — label on top, swatch below (1.5× size)
            float swatchSize = LeftPanelHelpers.ToggleSwatchSize;
            float swatchSpacing = LeftPanelHelpers.ToggleSwatchSpacing;
            float stepX = swatchSize + swatchSpacing;
            float ox = contentRect.x + innerPad;
            float oy = contentRect.y + innerPad;

            int rowCount = (_toggleTools.Length + _cols - 1) / _cols;
            float rowY = oy;

            for (int r = 0; r < rowCount; r++)
            {
                float rowH = _rowHeights[r];
                float maxLabelH = rowH - swatchSize;

                int start = r * _cols;
                int end = Mathf.Min(start + _cols, _toggleTools.Length);

                for (int i = start; i < end; i++)
                {
                    var tool = _toggleTools[i];
                    int col = i % _cols;
                    float cellX = ox + col * stepX;

                    float labelH = maxLabelH;
                    float swatchY = rowY + labelH;
                    float cellTotalH = labelH + swatchSize;
                    bool isSel = tool.IsToggleSelected;

                    // ── Title label with accent bar ──
                    var labelBgRect = new Rect(cellX, rowY, swatchSize, labelH);
                    EditorGUI.DrawRect(labelBgRect, LevelEditorStyles.SubHeaderBgColor);
                    EditorGUI.DrawRect(new Rect(cellX, rowY,
                        LevelEditorStyles.SubHeaderAccentWidth, labelH), tool.AccentColor);
                    GUI.Label(labelBgRect, _titles[i], LevelEditorStyles.SubHeaderStyle);

                    // ── Swatch ──
                    var swatchRect = new Rect(cellX, swatchY, swatchSize, swatchSize);

                    if (isSel)
                        EditorGUI.DrawRect(
                            LevelEditorDrawUtils.ExpandRect(swatchRect, LeftPanelHelpers.SwatchSelectionBorder),
                            LevelEditorStyles.SelectionBorderColor);

                    EditorGUI.DrawRect(
                        LevelEditorDrawUtils.ExpandRect(swatchRect, 1f),
                        LevelEditorStyles.SwatchBorderColor);
                    EditorGUI.DrawRect(swatchRect, tool.ToggleSwatchColor);
                    EditorGUI.DrawRect(
                        new Rect(swatchRect.x, swatchRect.y, swatchRect.width, 1f),
                        LevelEditorStyles.SwatchHighlightColor);

                    if (tool.ToggleSwatchContent != null)
                        GUI.Label(swatchRect, tool.ToggleSwatchContent,
                            LeftPanelHelpers.ToggleIconStyle);

                    // ── Click handling (covers label + swatch) ──
                    var hitRect = new Rect(cellX, rowY, swatchSize, cellTotalH);
                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.button == 0 &&
                        hitRect.Contains(Event.current.mousePosition))
                    {
                        tool.OnToggleClicked();
                        Event.current.Use();
                        _ctx.RequestRepaint?.Invoke();
                    }
                }

                rowY += rowH + swatchSpacing;
            }

            return contentStartY + contentH + LevelEditorStyles.GroupSpacing;
        }

        // ════════════════════════════════════════════════════════
        //  Layout cache (zero-GC after first call per panel width)
        // ════════════════════════════════════════════════════════

        void EnsureLayout(float panelWidth)
        {
            if (panelWidth == _cachedPanelWidth)
                return;
            _cachedPanelWidth = panelWidth;

            float pad = LevelEditorStyles.PanelPadding;
            float innerPad = LevelEditorStyles.GroupInnerPadding;
            float swatchSize = LeftPanelHelpers.ToggleSwatchSize;
            float swatchSpacing = LeftPanelHelpers.ToggleSwatchSpacing;

            float availW = panelWidth - pad * 2f - innerPad * 2f;
            _cols = Mathf.Max(1, (int)((availW + swatchSpacing) / (swatchSize + swatchSpacing)));

            // Per-tool label height (wordWrap: text may need 2+ lines)
            float subH = LevelEditorStyles.SubHeaderHeight;
            float labelTextW = swatchSize - LevelEditorStyles.SubHeaderAccentWidth
                - LevelEditorStyles.SubHeaderPadLeft;
            var subStyle = LevelEditorStyles.SubHeaderStyle;
            if (subStyle != null)
            {
                for (int i = 0; i < _toggleTools.Length; i++)
                    _labelHeights[i] = Mathf.Max(subH,
                        subStyle.CalcHeight(_titles[i], labelTextW));
            }
            else
            {
                for (int i = 0; i < _toggleTools.Length; i++)
                    _labelHeights[i] = subH;
            }

            // Per-row height = swatch + max label height in that row
            int count = _toggleTools.Length;
            int rows = (count + _cols - 1) / _cols;
            if (_rowHeights == null || _rowHeights.Length < rows)
                _rowHeights = new float[rows];

            _contentH = 0f;
            for (int r = 0; r < rows; r++)
            {
                float maxLabelH = subH;
                int start = r * _cols;
                int end = Mathf.Min(start + _cols, count);
                for (int i = start; i < end; i++)
                    maxLabelH = Mathf.Max(maxLabelH, _labelHeights[i]);

                _rowHeights[r] = maxLabelH + swatchSize;
                _contentH += _rowHeights[r];
            }
            _contentH += Mathf.Max(0, rows - 1) * swatchSpacing;

            _totalH = LevelEditorStyles.GroupTitleHeight + _contentH
                + innerPad * 2f + LevelEditorStyles.GroupSpacing;
        }
    }
}
