using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal static class LeftPanelHelpers
    {
        // ── Layout constants ──
        public const float ToggleSwatchSize    = 66f;
        public const float ToggleSwatchSpacing = 12f;
        public const float ZoneChildIndent     = 10f;

        // ── Zone accent colors ──
        public static readonly Color AccentToolsZone    = new Color(0.45f, 0.75f, 0.95f, 1f);
        public static readonly Color AccentPalettesZone = new Color(0.95f, 0.65f, 0.30f, 1f);

        // ── Toggle swatch rendering ──
        public const float SwatchSelectionBorder = 3f;

        // ── Cached content (zero-GC) ──
        public static readonly GUIContent ToolsZoneTitle    = new GUIContent("Tools");
        public static readonly GUIContent PalettesZoneTitle = new GUIContent("Palettes");

        // ── Styles (lazy-init) ──
        public static GUIStyle ToggleIconStyle { get; private set; }

        public static void EnsureStyles()
        {
            LevelEditorStyles.EnsureStyles();
            if (ToggleIconStyle != null) return;
            ToggleIconStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
            };
        }

        // ════════════════════════════════════════════════════════
        //  Zone header helper
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Vẽ header bar của một zone (title rect + accent bar + label).
        /// Returns Y cursor ngay sau header, nơi content bắt đầu.
        /// </summary>
        public static float BeginLeftZoneHeader(float startY, float panelWidth,
            GUIContent title, Color accent)
        {
            float pad = LevelEditorStyles.PanelPadding;
            float titleH = LevelEditorStyles.GroupTitleHeight;
            float w = panelWidth - pad * 2f;

            var headerRect = new Rect(pad, startY, w, titleH);
            EditorGUI.DrawRect(headerRect, LevelEditorStyles.GroupTitleBgColor);
            EditorGUI.DrawRect(new Rect(pad, startY, LevelEditorStyles.GroupAccentBarWidth, titleH), accent);
            GUI.Label(headerRect, title, LevelEditorStyles.GroupTitleStyle);

            return startY + titleH;
        }
    }
}
