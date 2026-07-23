using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Measures and draws the generated receiver queues above the grid canvas.
    /// Each queue is a vertical column of colored cells; the head (first out)
    /// is at the bottom (closest to the grid), the tail at the top.
    /// </summary>
    internal sealed class ReceiverQueueRenderer
    {
        // ── Layout constants ──
        const float DividerGap = 200f;
        const float QueueLabelHeight = 16f;
        const float MinQueueCellSize = 14f;
        const float MaxQueueCellSize = 36f;
        const float CellInnerShadow = 1f;
        const float CellLineThickness = 1f;
        const int MaxQueues = 99;

        // ── Colors ──
        static readonly Color s_labelColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color s_shadowBottom = new Color(0f, 0f, 0f, 0.25f);
        static readonly Color s_shadowRight = new Color(0f, 0f, 0f, 0.15f);
        static readonly Color s_highlightTop = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color s_gridLineColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        // ── Cached GUIContent for labels ──
        static readonly GUIContent[] s_queueLabels = new GUIContent[MaxQueues];
        static GUIStyle s_labelStyle;

        static ReceiverQueueRenderer()
        {
            for (int i = 0; i < MaxQueues; i++)
                s_queueLabels[i] = new GUIContent("Q" + i);
        }

        static void EnsureStyles()
        {
            if (s_labelStyle != null) return;
            s_labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = s_labelColor },
            };
        }

        // ── Instance ──
        readonly LevelEditorContext _ctx;

        // ── Layout cache ──
        float _queueCellSize;
        float _queueSpacing;
        float _queuesOriginX;
        float _queuesOriginY;  // top of label row
        int _cachedQueueCount;
        int _cachedMaxLength;

        // Pre-allocated rects: flat [queueIndex * maxLength + row]
        Rect[] _cellRects;
        Rect[] _labelRects;

        public ReceiverQueueRenderer(LevelEditorContext ctx)
        {
            _ctx = ctx;
            _labelRects = new Rect[MaxQueues];
        }

        public bool HasQueues =>
            _ctx.GeneratedReceiverQueues != null && _ctx.GeneratedReceiverQueues.Length > 0;

        // ════════════════════════════════════════════════════════
        //  Measure — called before CellSize calculation
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the total height needed for the queues area
        /// (labels + cells + divider gap). Does NOT include AxisLabelSize.
        /// </summary>
        public float MeasureQueuesHeight(float availableWidth)
        {
            if (!HasQueues) return 0f;

            var queues = _ctx.GeneratedReceiverQueues;
            int queueCount = queues.Length;
            int maxLen = 0;
            for (int i = 0; i < queueCount; i++)
            {
                int len = queues[i].colorTypesQueue.Length;
                if (len > maxLen) maxLen = len;
            }
            if (maxLen == 0) return 0f;

            float cellSize = ComputeCellSize(availableWidth, queueCount, maxLen);

            return QueueLabelHeight + cellSize * maxLen + DividerGap;
        }

        // ════════════════════════════════════════════════════════
        //  Layout — called after grid origin is determined
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuild layout cache. <paramref name="queuesBottomY"/> is the Y
        /// coordinate of the bottom edge of the queues area (just above the
        /// divider gap, which sits between queues and grid axis labels).
        /// <paramref name="availableWidth"/> must match the value passed to
        /// <see cref="MeasureQueuesHeight"/> so cell sizing is consistent.
        /// </summary>
        public void RebuildLayout(float gridOriginX, float queuesBottomY,
            float totalGridWidth, float availableWidth)
        {
            if (!HasQueues) return;

            var queues = _ctx.GeneratedReceiverQueues;
            _cachedQueueCount = queues.Length;
            _cachedMaxLength = 0;
            for (int i = 0; i < _cachedQueueCount; i++)
            {
                int len = queues[i].colorTypesQueue.Length;
                if (len > _cachedMaxLength) _cachedMaxLength = len;
            }
            if (_cachedMaxLength == 0) return;

            _queueCellSize = ComputeCellSize(availableWidth, _cachedQueueCount, _cachedMaxLength);
            _queueSpacing = Mathf.Max(2f, _queueCellSize * 0.15f);

            // Center queues horizontally under/above the grid
            float totalQueuesWidth = _cachedQueueCount * _queueCellSize
                + (_cachedQueueCount - 1) * _queueSpacing;
            _queuesOriginX = gridOriginX + (totalGridWidth - totalQueuesWidth) * 0.5f;

            // Vertical: labels at top, cells below labels, bottom edge = queuesBottomY
            float cellsHeight = _cachedMaxLength * _queueCellSize;
            _queuesOriginY = queuesBottomY - cellsHeight - QueueLabelHeight;

            // Allocate cell rects
            int totalCells = _cachedQueueCount * _cachedMaxLength;
            if (_cellRects == null || _cellRects.Length < totalCells)
                _cellRects = new Rect[totalCells];

            for (int q = 0; q < _cachedQueueCount; q++)
            {
                float colX = _queuesOriginX + q * (_queueCellSize + _queueSpacing);

                // Label rect
                if (q < _labelRects.Length)
                    _labelRects[q] = new Rect(colX, _queuesOriginY, _queueCellSize, QueueLabelHeight);

                // Cell rects: row 0 = top (tail/last), row maxLen-1 = bottom (head/first)
                float cellsTopY = _queuesOriginY + QueueLabelHeight;
                for (int row = 0; row < _cachedMaxLength; row++)
                {
                    _cellRects[q * _cachedMaxLength + row] =
                        new Rect(colX, cellsTopY + row * _queueCellSize,
                            _queueCellSize, _queueCellSize);
                }
            }
        }

        // ════════════════════════════════════════════════════════
        //  Draw
        // ════════════════════════════════════════════════════════

        public void Draw()
        {
            if (!HasQueues || _cachedMaxLength == 0) return;
            EnsureStyles();

            var queues = _ctx.GeneratedReceiverQueues;
            var colorLookup = _ctx.ColorLookup;

            // Draw labels
            for (int q = 0; q < _cachedQueueCount && q < MaxQueues; q++)
                GUI.Label(_labelRects[q], s_queueLabels[q], s_labelStyle);

            // Draw cells
            for (int q = 0; q < _cachedQueueCount; q++)
            {
                var queue = queues[q];
                int len = queue.colorTypesQueue.Length;

                // Shorter queues leave empty rows at the top, filled at the bottom
                int emptyRows = _cachedMaxLength - len;

                for (int row = emptyRows; row < _cachedMaxLength; row++)
                {
                    int rectIdx = q * _cachedMaxLength + row;
                    var rect = _cellRects[rectIdx];

                    // Map visual row to data index:
                    // row=emptyRows (first filled, top) → colorTypesQueue[len-1] (tail)
                    // row=maxLen-1 (bottom) → colorTypesQueue[0] (head, first out)
                    int dataIdx = len - 1 - (row - emptyRows);

                    int colorId = queue.colorTypesQueue[dataIdx];
                    if (colorLookup.TryGetValue(colorId, out var color))
                    {
                        EditorGUI.DrawRect(rect, color);
                        DrawCellInnerShadow(rect);
                        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), s_highlightTop);
                    }
                }

                // Grid lines between cells in this column
                float colX = _queuesOriginX + q * (_queueCellSize + _queueSpacing);
                float cellsTopY = _queuesOriginY + QueueLabelHeight;
                float cellsH = _cachedMaxLength * _queueCellSize;

                // Vertical borders (left + right)
                EditorGUI.DrawRect(new Rect(colX, cellsTopY, CellLineThickness, cellsH), s_gridLineColor);
                EditorGUI.DrawRect(new Rect(colX + _queueCellSize, cellsTopY, CellLineThickness, cellsH), s_gridLineColor);

                // Horizontal lines
                for (int row = 0; row <= _cachedMaxLength; row++)
                {
                    float ly = cellsTopY + row * _queueCellSize;
                    EditorGUI.DrawRect(new Rect(colX, ly, _queueCellSize, CellLineThickness), s_gridLineColor);
                }
            }

        }

        // ════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════

        static float ComputeCellSize(float availableWidth, int queueCount, int maxLength)
        {
            // Estimate spacing as fraction of cell size — solve:
            //   queueCount * cellSize + (queueCount - 1) * max(2, cellSize * 0.15) <= availableWidth
            // Approximate: cellSize * (queueCount + (queueCount-1)*0.15) <= availableWidth
            float factor = queueCount + (queueCount - 1) * 0.15f;
            float cellW = factor > 0f ? availableWidth / factor : MaxQueueCellSize;
            return Mathf.Clamp(Mathf.Floor(cellW), MinQueueCellSize, MaxQueueCellSize);
        }

        static void DrawCellInnerShadow(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - CellInnerShadow, rect.width, CellInnerShadow),
                s_shadowBottom);
            EditorGUI.DrawRect(new Rect(rect.xMax - CellInnerShadow, rect.y, CellInnerShadow, rect.height),
                s_shadowRight);
        }
    }
}
