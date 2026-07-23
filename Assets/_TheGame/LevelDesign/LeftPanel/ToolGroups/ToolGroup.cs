using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal abstract class ToolGroup
    {
        // ── Aliases for shared constants (originals on LevelEditorStyles) ──
        protected const float GroupInnerPadding = LevelEditorStyles.GroupInnerPadding;
        protected const float PanelPadding = LevelEditorStyles.PanelPadding;

        // ── Constants (tool-specific) ──
        protected const float HScrollBarHeight = 14f;
        protected const float SwatchSize = 44f;
        protected const float SwatchSpacing = 8f;
        protected const float SwatchSelectionBorder = 3f;

        // ── Shared swatch icon style (lazy-init, used by all tool groups) ──
        static GUIStyle s_swatchIconStyle;
        protected static GUIStyle SwatchIconStyle
        {
            get
            {
                if (s_swatchIconStyle == null)
                {
                    s_swatchIconStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
                    };
                }
                return s_swatchIconStyle;
            }
        }

        // ── Shared shortcut-key badge style (lazy-init) ──
        static readonly Color s_shortcutBadgeBg = new Color(0f, 0f, 0f, 0.7f);
        static GUIStyle s_shortcutBadgeStyle;
        protected static GUIStyle ShortcutBadgeStyle
        {
            get
            {
                if (s_shortcutBadgeStyle == null)
                {
                    s_shortcutBadgeStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 9,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.85f, 0.3f, 1f) }
                    };
                }
                return s_shortcutBadgeStyle;
            }
        }

        // ── Instance ──
        protected readonly LevelEditorContext Ctx;
        public string Title { get; }
        public Color AccentColor { get; }
        public bool HasHScroll { get; }
        public ToolMode AssociatedTool { get; }

        protected ToolGroup(LevelEditorContext ctx, string title, Color accentColor, ToolMode associatedTool, bool hasHScroll = false)
        {
            Ctx = ctx;
            Title = title;
            AccentColor = accentColor;
            AssociatedTool = associatedTool;
            HasHScroll = hasHScroll;
        }

        // ════════════════════════════════════════════════════════
        //  Toggle tool classification
        // ════════════════════════════════════════════════════════

        /// <summary>True for single-swatch tools drawn in the compact Tools zone matrix.</summary>
        public virtual bool IsToggleTool => false;

        /// <summary>Swatch background color (only used when IsToggleTool == true).</summary>
        public virtual Color ToggleSwatchColor => default;

        /// <summary>Swatch icon content (only used when IsToggleTool == true).</summary>
        public virtual GUIContent ToggleSwatchContent => null;

        /// <summary>Whether this toggle tool is currently the active tool.</summary>
        public virtual bool IsToggleSelected => Ctx.ActiveTool == AssociatedTool;

        /// <summary>Called when the toggle swatch is clicked in the Tools zone.</summary>
        public virtual void OnToggleClicked() => Ctx.SelectTool(AssociatedTool);

        /// <summary>Keyboard shortcut shown as a small badge on the swatch (e.g. "2", "V"), or null for none.</summary>
        public virtual string ShortcutKeyLabel => null;

        // ════════════════════════════════════════════════════════
        //  Abstract / Virtual
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Vẽ tool panel. Toggle tools (IsToggleTool == true) dùng default implementation:
        /// BeginToolGroup → DrawSwatch → SelectTool. Palette tools phải override.
        /// </summary>
        public virtual float DrawPanel(float startY, float panelWidth)
        {
            float nextY = BeginToolGroup(startY, panelWidth, out var c);
            if (DrawSwatch(new Rect(c.x + GroupInnerPadding, c.y + GroupInnerPadding, SwatchSize, SwatchSize),
                ToggleSwatchColor, IsToggleSelected, ToggleSwatchContent, SwatchIconStyle, ShortcutKeyLabel))
                OnToggleClicked();
            return nextY;
        }

        public virtual bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage) => false;

        /// <summary>Fired on MouseUp for the currently active tool, regardless of cursor position
        /// (e.g. even if the drag ended outside the grid). Used to finalize multi-step drag gestures.</summary>
        public virtual void OnMouseUp() { }

        public virtual bool CanHandleTool(ToolMode mode) => mode == AssociatedTool;

        public virtual void OnToolChanged(ToolMode newMode, int colorId) { }

        public virtual void DrawGridOverlayPreHover() { }

        public virtual void DrawGridOverlayPostHover() { }

        public virtual void OnGridResized(int oldWidth, int newWidth, int newHeight) { }

        public virtual float MeasureHeight(float panelWidth) => MeasureGroupHeight(HasHScroll);

        // ════════════════════════════════════════════════════════
        //  Shared drawing helpers (zero-GC)
        // ════════════════════════════════════════════════════════

        public static float MeasureGroupHeight(bool hasHScroll)
        {
            return LevelEditorStyles.GroupTitleHeight + SwatchSize + LevelEditorStyles.GroupInnerPadding * 2f
                + (hasHScroll ? HScrollBarHeight : 0f) + LevelEditorStyles.GroupSpacing;
        }

        protected float BeginToolGroup(float startY, float pw, out Rect content)
        {
            LevelEditorStyles.EnsureStyles();
            float pad = LevelEditorStyles.PanelPadding;
            float w = pw - pad * 2f;
            float titleH = LevelEditorStyles.GroupTitleHeight;

            var headerRect = new Rect(pad, startY, w, titleH);
            EditorGUI.DrawRect(headerRect, LevelEditorStyles.GroupTitleBgColor);
            EditorGUI.DrawRect(new Rect(pad, startY, LevelEditorStyles.GroupAccentBarWidth, titleH), AccentColor);
            GUI.Label(headerRect, Title, LevelEditorStyles.GroupTitleStyle);

            float ch = SwatchSize + LevelEditorStyles.GroupInnerPadding * 2f + (HasHScroll ? HScrollBarHeight : 0f);
            content = new Rect(pad, startY + titleH, w, ch);
            EditorGUI.DrawRect(content, LevelEditorStyles.GroupContentBgColor);

            return startY + titleH + ch + LevelEditorStyles.GroupSpacing;
        }

        protected bool DrawSwatch(Rect rect, Color fill, bool isSel, GUIContent label, GUIStyle style,
            string shortcutKey = null)
        {
            if (isSel)
                EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(rect, SwatchSelectionBorder), LevelEditorStyles.SelectionBorderColor);

            EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(rect, 1f), LevelEditorStyles.SwatchBorderColor);
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), LevelEditorStyles.SwatchHighlightColor);

            if (label != null && style != null)
                GUI.Label(rect, label, style);

            if (!string.IsNullOrEmpty(shortcutKey))
            {
                float badgeSize = Mathf.Min(16f, rect.width * 0.4f);
                var badgeRect = new Rect(rect.xMax - badgeSize - 2f, rect.yMax - badgeSize - 2f, badgeSize, badgeSize);
                EditorGUI.DrawRect(badgeRect, s_shortcutBadgeBg);
                GUI.Label(badgeRect, shortcutKey, ShortcutBadgeStyle);
            }

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }

            return false;
        }
    }
}
