using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class ColorToolGroup : ToolGroup
    {
        const int MinCols = 4;

        static readonly Color s_eraserBg = new Color(0.35f, 0.35f, 0.35f, 1f);

        static readonly GUIContent s_eraserContent = new GUIContent("✕", "Eraser (D)");

        // ── Layout cache (recalc only when panelWidth changes) ──
        float _cachedPanelWidth = -1f;
        int _cachedCols;
        float _cachedContentH;
        float _cachedTotalH;

        public ColorToolGroup(LevelEditorContext ctx)
            : base(ctx, "Colors", new Color(0.35f, 0.60f, 1.00f, 1f), ToolMode.PaintColor) { }

        public override bool CanHandleTool(ToolMode mode) => mode == ToolMode.PaintColor || mode == ToolMode.Eraser;

        void EnsureLayoutCache(float panelWidth)
        {
            if (panelWidth == _cachedPanelWidth) return;
            _cachedPanelWidth = panelWidth;

            float availW = panelWidth - PanelPadding * 2f - GroupInnerPadding * 2f;
            _cachedCols = Mathf.Max(MinCols, (int)((availW + SwatchSpacing) / (SwatchSize + SwatchSpacing)));

            int total = Ctx.PaletteCount + 1;
            int rows = (total + _cachedCols - 1) / _cachedCols;
            _cachedContentH = rows * SwatchSize + (rows - 1) * SwatchSpacing;
            _cachedTotalH = LevelEditorStyles.SubHeaderHeight + _cachedContentH
                + GroupInnerPadding * 2f + LevelEditorStyles.GroupSpacing;
        }

        public override float MeasureHeight(float panelWidth)
        {
            EnsureLayoutCache(panelWidth);
            return _cachedTotalH;
        }

        public override float DrawPanel(float startY, float panelWidth)
        {
            LevelEditorStyles.EnsureStyles();
            EnsureLayoutCache(panelWidth);

            float pad = PanelPadding;
            float w = panelWidth - pad * 2f;
            float subH = LevelEditorStyles.SubHeaderHeight;

            // Sub-header
            var headerRect = new Rect(pad, startY, w, subH);
            EditorGUI.DrawRect(headerRect, LevelEditorStyles.SubHeaderBgColor);
            EditorGUI.DrawRect(new Rect(pad, startY, LevelEditorStyles.SubHeaderAccentWidth, subH), AccentColor);
            GUI.Label(headerRect, Title, LevelEditorStyles.SubHeaderStyle);

            // Content area
            float ch = _cachedContentH + GroupInnerPadding * 2f;
            var content = new Rect(pad, startY + subH, w, ch);
            EditorGUI.DrawRect(content, LevelEditorStyles.GroupContentBgColor);

            // Swatch grid
            float ox = content.x + GroupInnerPadding;
            float oy = content.y + GroupInnerPadding;
            int total = Ctx.PaletteCount + 1;
            int cols = _cachedCols;
            float step = SwatchSize + SwatchSpacing;

            for (int i = 0; i < total; i++)
            {
                var rect = new Rect(
                    ox + (i % cols) * step,
                    oy + (i / cols) * step,
                    SwatchSize, SwatchSize);

                if (i < Ctx.PaletteCount)
                {
                    var e = Ctx.PaletteEntries[i];
                    bool sel = Ctx.ActiveTool == ToolMode.PaintColor && Ctx.SelectedColorId == e.materialId;
                    if (DrawSwatch(rect, e.color, sel, Ctx.PaletteTooltips[i], GUIStyle.none))
                        Ctx.SelectTool(ToolMode.PaintColor, e.materialId);
                }
                else
                {
                    if (DrawSwatch(rect, s_eraserBg, Ctx.ActiveTool == ToolMode.Eraser, s_eraserContent, SwatchIconStyle, "D"))
                        Ctx.SelectTool(ToolMode.Eraser);
                }
            }

            return startY + subH + ch + LevelEditorStyles.GroupSpacing;
        }

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (Ctx.ActiveTool == ToolMode.PaintColor)
                return HandlePaintColor(idx, ref cell, isClick, hasGarage);
            if (Ctx.ActiveTool == ToolMode.Eraser)
                return HandleEraser(idx, ref cell, isClick, hasGarage);
            return false;
        }

        bool HandlePaintColor(int idx, ref CellData cell, bool isClick, bool hasGarage)
        {
            if (Ctx.SelectedColorId < 0 || hasGarage)
                return false;
            if (cell.colorId != Ctx.SelectedColorId || cell.isObstacle)
            {
                bool wasCube = cell.colorId >= 0 && !cell.isObstacle;
                if (cell.isObstacle)
                    Ctx.RemoveConnectionsForCell(idx);
                cell.colorId = Ctx.SelectedColorId;
                cell.isObstacle = false;
                if (!wasCube)
                {
                    cell.isHidden = false;
                    Ctx.VehicleImportData[idx] = default;
                }
                Event.current.Use();
                Ctx.MarkStatusDirty();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }
            if (isClick)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        bool HandleEraser(int idx, ref CellData cell, bool isClick, bool hasGarage)
        {
            if (hasGarage || cell.isObstacle)
                return false;

            if (cell.colorId >= 0)
            {
                Ctx.RemoveConnectionsForCell(idx);
                cell.colorId = -1;
                cell.isHidden = false;
                Ctx.VehicleImportData[idx] = default;
                Event.current.Use();
                Ctx.MarkStatusDirty();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }
            if (isClick)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
