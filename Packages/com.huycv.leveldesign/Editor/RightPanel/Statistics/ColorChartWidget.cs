using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class ColorChartWidget : IStatisticsWidget
    {
        // ── Chart layout constants ──
        const float ChartHeight = 100f;
        const float ChartBarMaxWidth = 48f;
        const float ChartSwatchSize = 14f;
        const float ChartCountLabelH = 16f;
        const float ChartAxisWidth = 24f;
        const float ChartPadding = 6f;

        // ── Colors ──
        static readonly Color s_chartBg = new Color(0.13f, 0.13f, 0.15f, 1f);
        static readonly Color s_chartAxisColor = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color s_barBorder = new Color(1f, 1f, 1f, 0.25f);

        // ── GUIContent ──
        static readonly GUIContent s_title = new GUIContent("Color Distribution");
        static readonly GUIContent s_empty = new GUIContent("No colored cells");

        // ── Styles (lazy-init) ──
        static GUIStyle s_countStyle;
        static GUIStyle s_axisStyle;

        // ── Color counting (merged from ColorStatistics) ──
        const int MaxPaletteSize = 128; // must cover the full color palette or colors past this cap are silently dropped from the chart
        readonly int[] _counts = new int[MaxPaletteSize];
        readonly int[] _prevCounts = new int[MaxPaletteSize];
        readonly GUIContent[] _countLabels = new GUIContent[MaxPaletteSize];
        readonly Dictionary<int, int> _materialToIndex = new Dictionary<int, int>();
        readonly int[] _visibleIndices = new int[MaxPaletteSize];
        int _visibleCount;
        int _paletteCount;
        int _maxCount;

        // ── Bar layout cache ──
        float _cachedWidth = -1f;
        int _cachedVisCount = -1;
        float _barW;
        float _barSpacing;
        float _barsStartX;
        float _totalBarsW;

        // ── Y-axis tick cache ──
        readonly GUIContent _yTickZero = new GUIContent("0");
        readonly GUIContent _yTickMid = new GUIContent("");
        readonly GUIContent _yTickMax = new GUIContent("");
        int _cachedMaxForTicks = -1;

        // ── Palette ref (set by BuildIndex) ──
        PaletteEntry[] _paletteEntries;

        public ColorChartWidget()
        {
            for (int i = 0; i < MaxPaletteSize; i++)
            {
                _countLabels[i] = new GUIContent("0");
                _prevCounts[i] = -1;
            }
        }

        public void BuildIndex(PaletteEntry[] entries, int count)
        {
            _paletteEntries = entries;
            _materialToIndex.Clear();
            int n = Mathf.Min(count, MaxPaletteSize);
            for (int i = 0; i < n; i++)
                _materialToIndex[entries[i].materialId] = i;
        }

        public float MeasureHeight(float width)
        {
            return LevelEditorStyles.SubHeaderHeight
                + ChartPadding * 2f + ChartCountLabelH + ChartHeight + ChartSwatchSize + ChartPadding;
        }

        public void Rebuild(LevelEditorContext ctx)
        {
            _paletteCount = Mathf.Min(ctx.PaletteCount, MaxPaletteSize);

            for (int i = 0; i < _paletteCount; i++)
                _counts[i] = 0;

            int total = ctx.GridWidth * ctx.GridHeight;
            for (int i = 0; i < total; i++)
            {
                ref var cell = ref ctx.Cells[i];
                if (cell.isObstacle || cell.colorId < 0 || cell.garageId >= 0)
                    continue;
                if (_materialToIndex.TryGetValue(cell.colorId, out int idx) && idx < _paletteCount)
                    _counts[idx]++;
            }

            foreach (var kv in ctx.GarageMap)
            {
                var cars = kv.Value.carColors;
                for (int i = 0; i < cars.Count; i++)
                {
                    if (_materialToIndex.TryGetValue(cars[i], out int idx) && idx < _paletteCount)
                        _counts[idx]++;
                }
            }

            _maxCount = 0;
            _visibleCount = 0;
            for (int i = 0; i < _paletteCount; i++)
            {
                if (_counts[i] != _prevCounts[i])
                {
                    _prevCounts[i] = _counts[i];
                    _countLabels[i].text = _counts[i].ToString();
                }
                if (_counts[i] > 0)
                {
                    if (_counts[i] > _maxCount)
                        _maxCount = _counts[i];
                    _visibleIndices[_visibleCount++] = i;
                }
            }
        }

        public void Draw(float x, float y, float width)
        {
            EnsureStyles();

            // Sub-header
            var subRect = new Rect(x, y, width, LevelEditorStyles.SubHeaderHeight);
            EditorGUI.DrawRect(subRect, LevelEditorStyles.SubHeaderBgColor);
            EditorGUI.DrawRect(new Rect(x, y, LevelEditorStyles.SubHeaderAccentWidth, LevelEditorStyles.SubHeaderHeight), StatisticsGroup.Accent);
            GUI.Label(subRect, s_title, LevelEditorStyles.SubHeaderStyle);
            y += LevelEditorStyles.SubHeaderHeight;

            float totalH = ChartPadding * 2f + ChartCountLabelH + ChartHeight + ChartSwatchSize + ChartPadding;
            var bgRect = new Rect(x, y, width, totalH);
            EditorGUI.DrawRect(bgRect, s_chartBg);

            if (_visibleCount == 0)
            {
                GUI.Label(bgRect, s_empty, RightPanelHelpers.PlaceholderStyle);
                return;
            }

            // Recalc bar layout only when width or visible count changes
            if (width != _cachedWidth || _visibleCount != _cachedVisCount)
            {
                _cachedWidth = width;
                _cachedVisCount = _visibleCount;
                float chartLeft = x + ChartAxisWidth + ChartPadding;
                float availW = x + width - ChartPadding - chartLeft;
                int slots = _visibleCount + 1;
                // Never clamp to a hard minimum: with many visible colors, a fixed minimum bar
                // width can force the total row wider than availW, silently clipping the
                // rightmost bars out of the scroll view. Shrink to fit instead; only cap the max.
                float rawBarW = availW / (_visibleCount + slots);
                _barW = Mathf.Max(1f, Mathf.Min(rawBarW, ChartBarMaxWidth));
                _barSpacing = Mathf.Max(0f, (availW - _barW * _visibleCount) / slots);
                _totalBarsW = availW;
                _barsStartX = chartLeft + _barSpacing;
            }

            float countLabelY = y + ChartPadding;
            float barTopY = countLabelY + ChartCountLabelH;
            float barBottomY = barTopY + ChartHeight;
            float swatchY = barBottomY + 2f;

            // Y-axis tick labels
            if (_cachedMaxForTicks != _maxCount)
            {
                _cachedMaxForTicks = _maxCount;
                _yTickMax.text = _maxCount.ToString();
                _yTickMid.text = (_maxCount / 2).ToString();
            }

            float axisX = x + ChartPadding;
            float axisW = ChartAxisWidth - ChartPadding;
            GUI.Label(new Rect(axisX, barTopY - 2f, axisW, 14f), _yTickMax, s_axisStyle);
            GUI.Label(new Rect(axisX, barTopY + ChartHeight * 0.5f - 7f, axisW, 14f), _yTickMid, s_axisStyle);
            GUI.Label(new Rect(axisX, barBottomY - 12f, axisW, 14f), _yTickZero, s_axisStyle);

            // Axis lines
            float lineLeft = x + ChartAxisWidth + ChartPadding - 1f;
            EditorGUI.DrawRect(new Rect(lineLeft, barTopY, 1f, ChartHeight), s_chartAxisColor);
            EditorGUI.DrawRect(new Rect(lineLeft, barBottomY, _totalBarsW + 2f, 1f), s_chartAxisColor);

            // Bars
            float barStep = _barW + _barSpacing;
            for (int v = 0; v < _visibleCount; v++)
            {
                int pi = _visibleIndices[v];
                float barX = _barsStartX + v * barStep;
                float barH = (float)_counts[pi] / _maxCount * ChartHeight;

                var barRect = new Rect(barX, barBottomY - barH, _barW, barH);
                EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(barRect, 2f), s_barBorder);
                var barColor = _paletteEntries[pi].color;
                barColor.a = 0.85f;
                EditorGUI.DrawRect(barRect, barColor);

                GUI.Label(new Rect(barX, countLabelY, _barW, ChartCountLabelH), _countLabels[pi], s_countStyle);

                float swatchX = barX + (_barW - ChartSwatchSize) * 0.5f;
                EditorGUI.DrawRect(new Rect(swatchX, swatchY, ChartSwatchSize, ChartSwatchSize), _paletteEntries[pi].color);
            }
        }

        static void EnsureStyles()
        {
            if (s_countStyle != null) return;
            RightPanelHelpers.EnsureStyles();
            s_countStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.70f) }
            };
            s_axisStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 1f, 1f, 0.50f) }
            };
        }
    }
}
