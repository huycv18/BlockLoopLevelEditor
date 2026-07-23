using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class GaragePopupController
    {
        // ── Constants ──
        const float DefaultWidth = 440f;
        const float MinWidth = 280f;
        const float MaxWidth = 700f;
        const float MinHeight = 140f;
        const float MaxHeight = 700f;
        const float TitleBarHeight = 40f;
        const float ResizeGripSize = 18f;
        const float ScrollBarReserve = 14f;
        const float PopupPadding = 16f;
        const float PopupDirBtnSize = 64f;
        const float PopupDirBtnSpacing = 8f;
        const float PopupSwatchSize = 44f;
        const float PopupSwatchSpacing = 6f;
        const float PopupLabelHeight = 36f;
        const float PopupSectionSpacing = 12f;
        const float PopupCloseBtnSize = 36f;

        // ── Colors ──
        static readonly Color s_popupBg         = new Color(0.14f, 0.14f, 0.16f, 0.97f);
        static readonly Color s_popupBorder      = new Color(0.45f, 0.50f, 0.60f, 0.7f);
        static readonly Color s_titleBarBg      = new Color(0.19f, 0.19f, 0.22f, 1f);
        static readonly Color s_closeBtnColor    = new Color(1f, 1f, 1f, 0.40f);
        static readonly Color s_closeBtnHover    = new Color(1f, 1f, 1f, 0.80f);
        static readonly Color s_popupAddBtnBg    = new Color(0.28f, 0.28f, 0.30f, 1f);
        static readonly Color s_resizeGripColor  = new Color(1f, 1f, 1f, 0.35f);
        internal static readonly Color s_garageBtnBg = new Color(0.18f, 0.33f, 0.18f, 1f);

        // ── Cached content ──
        static readonly GUIContent[] s_dirArrows = {
            new GUIContent("↑"), new GUIContent("↓"),
            new GUIContent("←"), new GUIContent("→")
        };
        static readonly GUIContent s_addContent    = new GUIContent("+");
        static readonly GUIContent s_closeContent  = new GUIContent("✕");
        static readonly GUIContent s_dirLabel      = new GUIContent("Direction:");
        static readonly GUIContent s_carsLabel     = new GUIContent("Cars queue:");
        static readonly GUIContent s_titleContent  = new GUIContent("⠿⠿  Garage");
        static readonly GUIContent s_gripContent   = new GUIContent("⋱");

        // ── Styles (lazy-init) ──
        static GUIStyle s_popupLabel, s_popupDirBtn, s_popupDirBtnActive;
        static GUIStyle s_popupAddBtn, s_popupCloseBtn, s_titleBarStyle, s_gripStyle;

        // ── State ──
        readonly LevelEditorContext _ctx;
        readonly System.Action _onBeforeMutate;
        int _editingGarageId = -1;
        Rect _popupRect;
        float _cellRectX; // cached for overflow repositioning
        bool _popupAddColorMode;

        // ── User size (persists across garages this session) ──
        float _width = DefaultWidth;
        float _height = -1f; // -1 = auto (matches natural content height)

        // ── Drag / resize state ──
        bool _isDraggingWindow;
        Vector2 _dragMouseOffset;
        bool _isResizing;
        Vector2 _resizeStartMouse;
        float _resizeStartWidth, _resizeStartHeight;
        Vector2 _contentScroll;

        public bool IsOpen => _editingGarageId >= 0;

        public GaragePopupController(LevelEditorContext ctx, System.Action onBeforeMutate)
        {
            _ctx = ctx;
            _onBeforeMutate = onBeforeMutate;
        }

        public void Open(int garageId, Rect cellRect, float windowWidth)
        {
            _editingGarageId = garageId;
            _popupAddColorMode = false;
            _cellRectX = cellRect.x;
            float px = cellRect.xMax + 8f;
            if (px + _width > windowWidth)
                px = cellRect.x - _width - 8f;
            _popupRect = new Rect(px, cellRect.y, _width, 0f);
            _isDraggingWindow = false;
            _isResizing = false;
        }

        public void Close()
        {
            _editingGarageId = -1;
            _popupAddColorMode = false;
            _isDraggingWindow = false;
            _isResizing = false;
        }

        public bool ContainsMouse(Vector2 mousePos)
        {
            return IsOpen && _popupRect.Contains(mousePos);
        }

        public void OnToolChanged(ToolMode newMode, int colorId)
        {
            Close();
        }

        public void OnGarageRemoved(int garageId)
        {
            if (_editingGarageId == garageId)
                Close();
        }

        float ComputeContentHeight()
        {
            float h = PopupPadding;
            h += PopupLabelHeight;
            h += PopupDirBtnSize + PopupSectionSpacing;
            h += PopupLabelHeight;
            h += PopupSwatchSize + PopupSectionSpacing;
            if (_popupAddColorMode)
            {
                float innerW = _width - PopupPadding * 2f;
                int cols = Mathf.Max(1, Mathf.FloorToInt((innerW + PopupSwatchSpacing) / (PopupSwatchSize + PopupSwatchSpacing)));
                int rows = (_ctx.PaletteCount + cols - 1) / cols;
                h += rows * PopupSwatchSize + (rows - 1) * PopupSwatchSpacing + PopupSectionSpacing;
            }
            h += PopupPadding;
            return h;
        }

        public void DrawIfOpen(float windowWidth, float windowHeight)
        {
            if (!IsOpen) return;
            if (!_ctx.GarageMap.TryGetValue(_editingGarageId, out var g))
            {
                Close();
                return;
            }

            EnsureStyles();

            // Apply any in-progress drag/resize BEFORE laying out this frame.
            if (_isDraggingWindow)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var newPos = Event.current.mousePosition - _dragMouseOffset;
                    _popupRect.x = newPos.x;
                    _popupRect.y = newPos.y;
                    Event.current.Use();
                    _ctx.RequestRepaint?.Invoke();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isDraggingWindow = false;
                    Event.current.Use();
                }
            }
            else if (_isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var delta = Event.current.mousePosition - _resizeStartMouse;
                    _width = Mathf.Clamp(_resizeStartWidth + delta.x, MinWidth, MaxWidth);
                    _height = Mathf.Clamp(_resizeStartHeight + delta.y, MinHeight, MaxHeight);
                    Event.current.Use();
                    _ctx.RequestRepaint?.Invoke();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                    Event.current.Use();
                }
            }

            float contentH = ComputeContentHeight();
            float viewH = _height < 0f ? contentH : Mathf.Clamp(_height, MinHeight, MaxHeight);
            float popupH = TitleBarHeight + viewH;

            // Reposition to stay on-screen (skip the horizontal clamp while actively dragging so
            // the popup doesn't fight the user's cursor mid-drag).
            float px = _popupRect.x;
            if (!_isDraggingWindow)
            {
                if (px + _width > windowWidth) px = windowWidth - _width - 8f;
                if (px < 0f) px = 0f;
            }
            float py = _popupRect.y;
            if (!_isDraggingWindow)
            {
                if (py + popupH > windowHeight) py = windowHeight - popupH - 8f;
                if (py < 0f) py = 0f;
            }

            _popupRect = new Rect(px, py, _width, popupH);

            // Background + border
            EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(_popupRect, 1f), s_popupBorder);
            EditorGUI.DrawRect(_popupRect, s_popupBg);

            // ── Title bar (drag handle) ──
            var titleBarRect = new Rect(_popupRect.x, _popupRect.y, _popupRect.width, TitleBarHeight);
            EditorGUI.DrawRect(titleBarRect, s_titleBarBg);
            GUI.Label(new Rect(titleBarRect.x + 10f, titleBarRect.y, titleBarRect.width - 50f, titleBarRect.height),
                s_titleContent, s_titleBarStyle);
            EditorGUIUtility.AddCursorRect(titleBarRect, MouseCursor.MoveArrow);

            // Close button (top-right, inside title bar)
            var closeRect = new Rect(
                _popupRect.xMax - PopupCloseBtnSize - 6f,
                titleBarRect.y + (TitleBarHeight - PopupCloseBtnSize) * 0.5f,
                PopupCloseBtnSize, PopupCloseBtnSize);
            bool closeHover = closeRect.Contains(Event.current.mousePosition);
            GUI.Label(closeRect, s_closeContent,
                closeHover ? s_popupCloseBtn : s_popupLabel);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && closeHover)
            {
                Close();
                Event.current.Use();
                _ctx.RequestRepaint?.Invoke();
                return;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                titleBarRect.Contains(Event.current.mousePosition) && !closeHover)
            {
                _isDraggingWindow = true;
                _dragMouseOffset = Event.current.mousePosition - new Vector2(_popupRect.x, _popupRect.y);
                Event.current.Use();
            }

            // ── Scrollable content body ──
            bool scrolling = contentH > viewH + 0.5f;
            float scrollbarReserve = scrolling ? ScrollBarReserve : 0f;
            var viewRect = new Rect(_popupRect.x, _popupRect.y + TitleBarHeight, _popupRect.width, viewH);
            var contentRect = new Rect(0f, 0f, _popupRect.width - scrollbarReserve, contentH);
            _contentScroll = GUI.BeginScrollView(viewRect, _contentScroll, contentRect, false, false);

            float cx = PopupPadding;
            float cy = PopupPadding;
            float cw = contentRect.width - PopupPadding * 2f;

            // Direction label
            GUI.Label(new Rect(cx, cy, cw, PopupLabelHeight), s_dirLabel, s_popupLabel);
            cy += PopupLabelHeight;

            // Direction buttons
            for (int d = 0; d < 4; d++)
            {
                var dr = new Rect(cx + d * (PopupDirBtnSize + PopupDirBtnSpacing), cy, PopupDirBtnSize, PopupDirBtnSize);
                bool active = g.directionType == d;
                if (active)
                    EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(dr, 2f), LevelEditorStyles.SelectionBorderColor);
                EditorGUI.DrawRect(dr, active ? s_garageBtnBg : s_popupAddBtnBg);
                GUI.Label(dr, s_dirArrows[d], active ? s_popupDirBtnActive : s_popupDirBtn);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    dr.Contains(Event.current.mousePosition))
                {
                    _onBeforeMutate?.Invoke();
                    g.directionType = d;
                    Event.current.Use();
                    _ctx.RequestRepaint?.Invoke();
                }
            }
            cy += PopupDirBtnSize + PopupSectionSpacing;

            // Cars queue label
            GUI.Label(new Rect(cx, cy, cw, PopupLabelHeight), s_carsLabel, s_popupLabel);
            cy += PopupLabelHeight;

            // Cars queue swatches
            float sx = cx;
            for (int i = 0; i < g.carColors.Count; i++)
            {
                int cid = g.carColors[i];
                var sr = new Rect(sx, cy, PopupSwatchSize, PopupSwatchSize);
                Color col = _ctx.ColorLookup.TryGetValue(cid, out var pc) ? pc : Color.gray;
                EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(sr, 1f), LevelEditorStyles.SwatchBorderColor);
                EditorGUI.DrawRect(sr, col);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                    sr.Contains(Event.current.mousePosition))
                {
                    _onBeforeMutate?.Invoke();
                    g.carColors.RemoveAt(i);
                    LevelEditorContext.UpdateGarageCountCache(g);
                    _ctx.MarkStatusDirty();
                    Event.current.Use();
                    _ctx.RequestRepaint?.Invoke();
                    GUI.EndScrollView();
                    return;
                }
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    sr.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                }
                sx += PopupSwatchSize + PopupSwatchSpacing;
            }

            // Add button
            var addR = new Rect(sx, cy, PopupSwatchSize, PopupSwatchSize);
            EditorGUI.DrawRect(addR, s_popupAddBtnBg);
            GUI.Label(addR, s_addContent, s_popupAddBtn);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                addR.Contains(Event.current.mousePosition))
            {
                _popupAddColorMode = !_popupAddColorMode;
                Event.current.Use();
                _ctx.RequestRepaint?.Invoke();
            }
            cy += PopupSwatchSize + PopupSectionSpacing;

            // Mini palette
            if (_popupAddColorMode)
            {
                float paletteX = cx;
                for (int i = 0; i < _ctx.PaletteCount; i++)
                {
                    var pe = _ctx.PaletteEntries[i];
                    var pr = new Rect(paletteX, cy, PopupSwatchSize, PopupSwatchSize);
                    if (paletteX + PopupSwatchSize > contentRect.width - PopupPadding)
                    {
                        paletteX = cx;
                        cy += PopupSwatchSize + PopupSwatchSpacing;
                        pr = new Rect(paletteX, cy, PopupSwatchSize, PopupSwatchSize);
                    }
                    EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(pr, 1f), LevelEditorStyles.SwatchBorderColor);
                    EditorGUI.DrawRect(pr, pe.color);
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                        pr.Contains(Event.current.mousePosition))
                    {
                        _onBeforeMutate?.Invoke();
                        g.carColors.Add(pe.materialId);
                        LevelEditorContext.UpdateGarageCountCache(g);
                        _ctx.MarkStatusDirty();
                        Event.current.Use();
                        _ctx.RequestRepaint?.Invoke();
                        GUI.EndScrollView();
                        return;
                    }
                    paletteX += PopupSwatchSize + PopupSwatchSpacing;
                }
                cy += PopupSwatchSize + PopupSectionSpacing;
            }

            GUI.EndScrollView();

            // ── Resize grip (bottom-right corner) ──
            var gripRect = new Rect(_popupRect.xMax - ResizeGripSize - 2f, _popupRect.yMax - ResizeGripSize - 2f,
                ResizeGripSize, ResizeGripSize);
            GUI.Label(gripRect, s_gripContent, s_gripStyle);
            EditorGUIUtility.AddCursorRect(gripRect, MouseCursor.ResizeUpLeft);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                gripRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
                _resizeStartMouse = Event.current.mousePosition;
                _resizeStartWidth = _width;
                _resizeStartHeight = viewH;
                Event.current.Use();
            }

            // Click outside popup → close
            if (Event.current.type == EventType.MouseDown && !_popupRect.Contains(Event.current.mousePosition))
            {
                Close();
                Event.current.Use();
                _ctx.RequestRepaint?.Invoke();
            }
        }

        static void EnsureStyles()
        {
            if (s_popupLabel != null) return;

            s_popupLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 22,
                normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }
            };
            s_popupDirBtn = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
            };
            s_popupDirBtnActive = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            s_popupAddBtn = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
            s_popupCloseBtn = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = s_closeBtnHover }
            };
            s_titleBarStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
            s_gripStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = s_resizeGripColor }
            };
        }
    }
}
