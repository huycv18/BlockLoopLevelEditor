using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    /// <summary>
    /// Rectangle drag-select, then drag inside the selection to move its contents (colors,
    /// obstacles, hidden, garages) elsewhere on the grid, overwriting the destination.
    /// </summary>
    internal sealed class SelectToolGroup : ToolGroup
    {
        // ── Colors ──
        static readonly Color s_selBorderColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color s_selFillColor = new Color(1f, 0.85f, 0.2f, 0.12f);
        static readonly Color s_ghostObstacle = new Color(0.9f, 0.2f, 0.2f, 0.45f);
        static readonly Color s_ghostGarage = new Color(0.2f, 0.8f, 0.2f, 0.45f);
        static readonly Color s_ghostEmpty = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color s_selectBtnBg = new Color(0.35f, 0.32f, 0.10f, 1f);

        static readonly GUIContent s_selectBtnContent = new GUIContent("⛶", "Select / Move (V)");

        readonly System.Action _onBeforeMutate;

        bool _hasSelection;
        int _selMinX, _selMinY, _selMaxX, _selMaxY;

        bool _isDragging;
        bool _isMoving;
        int _dragStartX, _dragStartY;
        int _dragCurX, _dragCurY;

        // Move-drag snapshot — captured once at move-start, grid isn't mutated until commit.
        CellData[] _moveBuffer;
        int _moveW, _moveH;

        public SelectToolGroup(LevelEditorContext ctx, System.Action onBeforeMutate)
            : base(ctx, "Select", new Color(1f, 0.85f, 0.2f, 1f), ToolMode.Select)
        {
            _onBeforeMutate = onBeforeMutate;
        }

        public override bool IsToggleTool => true;
        public override Color ToggleSwatchColor => s_selectBtnBg;
        public override GUIContent ToggleSwatchContent => s_selectBtnContent;
        public override string ShortcutKeyLabel => "V";

        public bool HasSelection => _hasSelection;

        public void ClearSelection()
        {
            _hasSelection = false;
            _isDragging = false;
            _isMoving = false;
            _moveBuffer = null;
        }

        public override void OnToolChanged(ToolMode newMode, int colorId) => ClearSelection();

        public override void OnGridResized(int oldWidth, int newWidth, int newHeight) => ClearSelection();

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (isClick)
            {
                if (_hasSelection && IsInsideSelection(cx, cy))
                {
                    _isMoving = true;
                    _isDragging = true;
                    _dragStartX = cx; _dragStartY = cy;
                    _dragCurX = cx; _dragCurY = cy;
                    CaptureMoveBuffer();
                }
                else
                {
                    _hasSelection = false;
                    _isMoving = false;
                    _isDragging = true;
                    _dragStartX = cx; _dragStartY = cy;
                    _dragCurX = cx; _dragCurY = cy;
                }
                Event.current.Use();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }

            if (isDrag && _isDragging)
            {
                _dragCurX = cx; _dragCurY = cy;
                Event.current.Use();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }

            return false;
        }

        public override void OnMouseUp()
        {
            if (!_isDragging) return;

            if (_isMoving)
                CommitMove();
            else
            {
                _selMinX = Mathf.Min(_dragStartX, _dragCurX);
                _selMaxX = Mathf.Max(_dragStartX, _dragCurX);
                _selMinY = Mathf.Min(_dragStartY, _dragCurY);
                _selMaxY = Mathf.Max(_dragStartY, _dragCurY);
                _hasSelection = true;
            }

            _isDragging = false;
            _isMoving = false;
            _moveBuffer = null;
            Ctx.RequestRepaint?.Invoke();
        }

        bool IsInsideSelection(int cx, int cy)
        {
            return cx >= _selMinX && cx <= _selMaxX && cy >= _selMinY && cy <= _selMaxY;
        }

        void CaptureMoveBuffer()
        {
            _moveW = _selMaxX - _selMinX + 1;
            _moveH = _selMaxY - _selMinY + 1;
            _moveBuffer = new CellData[_moveW * _moveH];
            for (int ry = 0; ry < _moveH; ry++)
            for (int rx = 0; rx < _moveW; rx++)
                _moveBuffer[ry * _moveW + rx] = Ctx.Cells[(_selMinY + ry) * Ctx.GridWidth + (_selMinX + rx)];
        }

        void CommitMove()
        {
            int rawDeltaX = _dragCurX - _dragStartX;
            int rawDeltaY = _dragCurY - _dragStartY;
            ClampDelta(rawDeltaX, rawDeltaY, out int deltaX, out int deltaY);

            if (deltaX != 0 || deltaY != 0)
            {
                _onBeforeMutate?.Invoke();
                Ctx.MoveBlock(_selMinX, _selMinY, _moveW, _moveH, deltaX, deltaY);
                _selMinX += deltaX; _selMaxX += deltaX;
                _selMinY += deltaY; _selMaxY += deltaY;
                Ctx.ShowToast?.Invoke("Selection moved");
            }
        }

        void ClampDelta(int deltaX, int deltaY, out int outDx, out int outDy)
        {
            int minDx = -_selMinX;
            int maxDx = Ctx.GridWidth - 1 - _selMaxX;
            int minDy = -_selMinY;
            int maxDy = Ctx.GridHeight - 1 - _selMaxY;
            outDx = Mathf.Clamp(deltaX, minDx, maxDx);
            outDy = Mathf.Clamp(deltaY, minDy, maxDy);
        }

        // ════════════════════════════════════════════════════════
        //  Overlay drawing
        // ════════════════════════════════════════════════════════

        public override void DrawGridOverlayPreHover()
        {
            if (_isDragging && _isMoving)
                DrawMoveGhost();
            else if (_isDragging)
                DrawLiveSelectionRect();
            else if (_hasSelection)
                DrawSelectionBorder(_selMinX, _selMinY, _selMaxX, _selMaxY);
        }

        void DrawLiveSelectionRect()
        {
            int minX = Mathf.Min(_dragStartX, _dragCurX);
            int maxX = Mathf.Max(_dragStartX, _dragCurX);
            int minY = Mathf.Min(_dragStartY, _dragCurY);
            int maxY = Mathf.Max(_dragStartY, _dragCurY);
            DrawSelectionBorder(minX, minY, maxX, maxY);
        }

        void DrawSelectionBorder(int minX, int minY, int maxX, int maxY)
        {
            if (Ctx.CachedCellCount == 0) return;
            int minIdx = minY * Ctx.GridWidth + minX;
            int maxIdx = maxY * Ctx.GridWidth + maxX;
            if (minIdx < 0 || maxIdx >= Ctx.CachedCellCount) return;

            var minRect = Ctx.CellRects[minIdx];
            var maxRect = Ctx.CellRects[maxIdx];
            var full = new Rect(minRect.x, minRect.y, maxRect.xMax - minRect.x, maxRect.yMax - minRect.y);
            EditorGUI.DrawRect(full, s_selFillColor);
            LevelEditorDrawUtils.DrawWireRect(full, s_selBorderColor, 2f);
        }

        void DrawMoveGhost()
        {
            int rawDeltaX = _dragCurX - _dragStartX;
            int rawDeltaY = _dragCurY - _dragStartY;
            ClampDelta(rawDeltaX, rawDeltaY, out int deltaX, out int deltaY);

            int newMinX = _selMinX + deltaX;
            int newMinY = _selMinY + deltaY;

            for (int ry = 0; ry < _moveH; ry++)
            for (int rx = 0; rx < _moveW; rx++)
            {
                int destIdx = (newMinY + ry) * Ctx.GridWidth + (newMinX + rx);
                if (destIdx < 0 || destIdx >= Ctx.CachedCellCount) continue;
                var rect = Ctx.CellRects[destIdx];
                var cell = _moveBuffer[ry * _moveW + rx];

                Color fill;
                if (cell.garageId >= 0) fill = s_ghostGarage;
                else if (cell.isObstacle) fill = s_ghostObstacle;
                else if (cell.colorId >= 0 && Ctx.ColorLookup.TryGetValue(cell.colorId, out var pc))
                {
                    fill = pc;
                    fill.a = 0.55f;
                }
                else fill = s_ghostEmpty;

                EditorGUI.DrawRect(rect, fill);
            }

            DrawSelectionBorder(newMinX, newMinY, newMinX + _moveW - 1, newMinY + _moveH - 1);
        }
    }
}
