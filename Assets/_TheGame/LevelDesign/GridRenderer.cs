using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class GridRenderer
    {
        // ── Colors ──
        static readonly Color s_cellColorA       = new Color(0.23f, 0.23f, 0.26f, 1f);
        static readonly Color s_cellColorB       = new Color(0.26f, 0.26f, 0.29f, 1f);
        static readonly Color s_gridBgColor      = new Color(0.14f, 0.14f, 0.16f, 1f);
        static readonly Color s_gridLineColor    = new Color(0.12f, 0.12f, 0.14f, 1f);
        static readonly Color s_cellShadowBottom = new Color(0f, 0f, 0f, 0.25f);
        static readonly Color s_cellShadowRight  = new Color(0f, 0f, 0f, 0.15f);
        static readonly Color s_cellHighlightTop = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color s_labelColor       = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color s_hoverBorderColor = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color s_hoverFillColor   = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color s_previewTint      = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color s_obstacleLineColor = new Color(1f, 0.3f, 0.3f, 0.45f);
        static readonly Color s_obstaclePreview  = new Color(0.12f, 0.12f, 0.12f, 0.45f);
        static readonly Color s_hiddenOverlay    = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        static readonly Color s_hiddenQColor     = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color s_garageBg         = new Color(0.14f, 0.26f, 0.14f, 1f);
        static readonly Color s_garageArrowColor = new Color(1f, 1f, 1f, 0.8f);
        static readonly Color s_garageCountColor = new Color(1f, 1f, 0.6f, 0.9f);

        const float CellInnerShadow = 1f;
        const float GridLineThickness = 1f;

        // ── Cached content ──
        static readonly GUIContent s_obstacleCellX = new GUIContent("✕");
        static readonly GUIContent s_hiddenCellQ  = new GUIContent("?");
        static readonly GUIContent[] s_dirArrows  = {
            new GUIContent("↑"), new GUIContent("↓"),
            new GUIContent("←"), new GUIContent("→")
        };
        static readonly GUIContent s_emptyStateIcon = new GUIContent("▦");
        static readonly GUIContent s_emptyStateMsg  = new GUIContent("No level data\nSet Width and Height to begin");

        // ── Styles (lazy-init) ──
        static GUIStyle s_axisLabel;
        static GUIStyle s_cellIconStyle;
        static GUIStyle s_garageCount;
        static GUIStyle s_emptyStateLabel, s_emptyStateIconStyle;

        // ── Instance ──
        readonly LevelEditorContext _ctx;

        // ── Axis labels (pre-cached) ──
        readonly GUIContent[] _colLabels = new GUIContent[LevelEditorContext.MaxGridSize];
        readonly GUIContent[] _rowLabels = new GUIContent[LevelEditorContext.MaxGridSize];
        readonly Rect[] _colLabelRects = new Rect[LevelEditorContext.MaxGridSize];
        readonly Rect[] _rowLabelRects = new Rect[LevelEditorContext.MaxGridSize];

        // ── Font size cache ──
        float _prevCellSizeForFont = -1f;

        public GridRenderer(LevelEditorContext ctx)
        {
            _ctx = ctx;
            for (int i = 0; i < LevelEditorContext.MaxGridSize; i++)
            {
                _colLabels[i] = new GUIContent(i.ToString());
                _rowLabels[i] = new GUIContent(i.ToString());
            }
        }

        // ════════════════════════════════════════════════════════
        //  Style initialization
        // ════════════════════════════════════════════════════════

        public static void EnsureStyles()
        {
            if (s_axisLabel != null) return;

            s_axisLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = s_labelColor },
                hover = { textColor = s_labelColor }
            };
            s_cellIconStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            s_garageCount = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 9,
                normal = { textColor = s_garageCountColor }
            };
            s_emptyStateLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(1f, 1f, 1f, 0.25f) }
            };
            s_emptyStateIconStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 48,
                normal = { textColor = new Color(1f, 1f, 1f, 0.10f) }
            };
        }

        // ════════════════════════════════════════════════════════
        //  Layout
        // ════════════════════════════════════════════════════════

        public void UpdateAxisLabelRects(float axisLabelSize)
        {
            for (int x = 0; x < _ctx.GridWidth; x++)
                _colLabelRects[x] = new Rect(_ctx.GridOriginX + x * _ctx.CellSize, _ctx.GridOriginY - axisLabelSize, _ctx.CellSize, axisLabelSize);
            for (int y = 0; y < _ctx.GridHeight; y++)
                _rowLabelRects[y] = new Rect(_ctx.GridOriginX - axisLabelSize, _ctx.GridOriginY + y * _ctx.CellSize, axisLabelSize, _ctx.CellSize);
        }

        public void UpdateCachedFontSizes()
        {
            if (Mathf.Approximately(_prevCellSizeForFont, _ctx.CellSize))
                return;
            _prevCellSizeForFont = _ctx.CellSize;
            if (s_cellIconStyle != null) s_cellIconStyle.fontSize = Mathf.Max(8, (int)(_ctx.CellSize * 0.45f));
        }

        // ════════════════════════════════════════════════════════
        //  Drawing methods
        // ════════════════════════════════════════════════════════

        public void DrawGridBackground(Rect gridArea) => EditorGUI.DrawRect(gridArea, s_gridBgColor);

        public void DrawCells()
        {
            int gridW = _ctx.GridWidth;
            int gridH = _ctx.GridHeight;
            int cellCount = _ctx.CachedCellCount;
            var cells = _ctx.Cells;
            var cellRects = _ctx.CellRects;
            var garageMap = _ctx.GarageMap;
            var colorLookup = _ctx.ColorLookup;

            for (int y = 0; y < gridH; y++)
            for (int x = 0; x < gridW; x++)
            {
                int idx = y * gridW + x;
                if (idx >= cellCount) return;
                ref var cell = ref cells[idx];
                var rect = cellRects[idx];

                if (cell.garageId >= 0 && garageMap.TryGetValue(cell.garageId, out var g))
                    DrawGarageCell(rect, g);
                else if (cell.isObstacle)
                    DrawObstacleCell(rect);
                else if (cell.colorId >= 0 && colorLookup.TryGetValue(cell.colorId, out var pc))
                    DrawColorCell(rect, pc, cell.isHidden);
                else
                    DrawEmptyCell(rect, x, y);
            }
        }

        void DrawGarageCell(Rect rect, GarageInfo g)
        {
            EditorGUI.DrawRect(rect, s_garageBg);
            DrawCellInnerShadow(rect);

            var prevColor = GUI.contentColor;
            GUI.contentColor = s_garageArrowColor;
            GUI.Label(rect, s_dirArrows[Mathf.Clamp(g.directionType, 0, 3)], s_cellIconStyle);
            GUI.contentColor = prevColor;

            if (g.carColors.Count > 0)
            {
                var countRect = new Rect(rect.x, rect.y + rect.height * 0.55f, rect.width, rect.height * 0.4f);
                GUI.Label(countRect, LevelEditorDrawUtils.GetNumberContent(g.carColors.Count), s_garageCount);
            }
        }

        void DrawObstacleCell(Rect rect)
        {
            EditorGUI.DrawRect(rect, LevelEditorStyles.ObstacleBgColor);
            var prevColor = GUI.contentColor;
            GUI.contentColor = s_obstacleLineColor;
            GUI.Label(rect, s_obstacleCellX, s_cellIconStyle);
            GUI.contentColor = prevColor;
        }

        void DrawColorCell(Rect rect, Color color, bool isHidden)
        {
            EditorGUI.DrawRect(rect, color);
            DrawCellInnerShadow(rect);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), s_cellHighlightTop);

            if (isHidden)
            {
                EditorGUI.DrawRect(rect, s_hiddenOverlay);
                var prevColor = GUI.contentColor;
                GUI.contentColor = s_hiddenQColor;
                GUI.Label(rect, s_hiddenCellQ, s_cellIconStyle);
                GUI.contentColor = prevColor;
            }
        }

        static void DrawEmptyCell(Rect rect, int x, int y)
        {
            EditorGUI.DrawRect(rect, (x + y) % 2 == 0 ? s_cellColorA : s_cellColorB);
        }

        static void DrawCellInnerShadow(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - CellInnerShadow, rect.width, CellInnerShadow), s_cellShadowBottom);
            EditorGUI.DrawRect(new Rect(rect.xMax - CellInnerShadow, rect.y, CellInnerShadow, rect.height), s_cellShadowRight);
        }

        public void DrawGridLines()
        {
            if (_ctx.CachedCellCount == 0) return;
            for (int x = 0; x <= _ctx.GridWidth; x++)
                EditorGUI.DrawRect(new Rect(_ctx.GridOriginX + x * _ctx.CellSize, _ctx.GridOriginY, GridLineThickness, _ctx.TotalGridHeight), s_gridLineColor);
            for (int y = 0; y <= _ctx.GridHeight; y++)
                EditorGUI.DrawRect(new Rect(_ctx.GridOriginX, _ctx.GridOriginY + y * _ctx.CellSize, _ctx.TotalGridWidth, GridLineThickness), s_gridLineColor);
        }

        public void DrawAxisLabels()
        {
            if (_ctx.CachedCellCount == 0) return;
            for (int x = 0; x < _ctx.GridWidth; x++)
                GUI.Label(_colLabelRects[x], _colLabels[x], s_axisLabel);
            for (int y = 0; y < _ctx.GridHeight; y++)
                GUI.Label(_rowLabelRects[y], _rowLabels[y], s_axisLabel);
        }

        public void DrawConnections(ConnectionToolGroup connectionGroup)
        {
            if (_ctx.Connections.Count == 0 || _ctx.CachedCellCount == 0)
                return;
            foreach (long edge in _ctx.Connections)
            {
                LevelEditorDrawUtils.UnpackEdge(edge, out int a, out int b);
                if (a >= _ctx.CachedCellCount || b >= _ctx.CachedCellCount)
                    continue;
                connectionGroup.DrawConnectorBlock(_ctx.CellRects[a], _ctx.CellRects[b], false);
            }
        }

        public void DrawHoverHighlight()
        {
            if (_ctx.HoverX < 0 || _ctx.HoverY < 0)
                return;
            int idx = _ctx.HoverY * _ctx.GridWidth + _ctx.HoverX;
            if (idx >= _ctx.CachedCellCount)
                return;

            var rect = _ctx.CellRects[idx];
            EditorGUI.DrawRect(rect, s_hoverFillColor);
            LevelEditorDrawUtils.DrawWireRect(rect, s_hoverBorderColor, 1f);
        }

        public void DrawHoverPreview()
        {
            if (_ctx.HoverX < 0 || _ctx.HoverY < 0)
                return;
            int idx = _ctx.HoverY * _ctx.GridWidth + _ctx.HoverX;
            if (idx >= _ctx.CachedCellCount)
                return;

            if (_ctx.ActiveTool == ToolMode.PaintColor && _ctx.SelectedColorId >= 0 &&
                _ctx.ColorLookup.TryGetValue(_ctx.SelectedColorId, out var pc))
            {
                var t = pc;
                t.a = s_previewTint.a;
                EditorGUI.DrawRect(_ctx.CellRects[idx], t);
            }
            else if (_ctx.ActiveTool == ToolMode.PaintObstacle)
            {
                EditorGUI.DrawRect(_ctx.CellRects[idx], s_obstaclePreview);
            }
        }

        public void DrawEmptyState(Rect gridArea)
        {
            EditorGUI.DrawRect(gridArea, s_gridBgColor);
            var iconRect = new Rect(gridArea.x, gridArea.center.y - 50f, gridArea.width, 60f);
            GUI.Label(iconRect, s_emptyStateIcon, s_emptyStateIconStyle);
            var textRect = new Rect(gridArea.x, gridArea.center.y + 10f, gridArea.width, 40f);
            GUI.Label(textRect, s_emptyStateMsg, s_emptyStateLabel);
        }
    }
}
