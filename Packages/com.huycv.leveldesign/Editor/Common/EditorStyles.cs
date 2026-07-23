using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Shared layout constants, colors, và GUIStyles dùng chung bởi tất cả panel subsystems.
    /// </summary>
    internal static class LevelEditorStyles
    {
        // ── Group layout constants ──
        public const float GroupTitleHeight   = 42f;
        public const float GroupSpacing       = 8f;
        public const float GroupInnerPadding  = 8f;
        public const float GroupAccentBarWidth = 4f;
        public const float PanelPadding       = 12f;

        // ── Sub-header constants ──
        public const float SubHeaderHeight      = 27f;
        public const float SubHeaderAccentWidth = 4f;
        public const float SubHeaderPadLeft     = 9f;

        // ── Group colors ──
        public static readonly Color GroupTitleBgColor   = new Color(0.21f, 0.21f, 0.24f, 1f);
        public static readonly Color GroupContentBgColor = new Color(0.19f, 0.19f, 0.22f, 1f);
        public static readonly Color SubHeaderBgColor    = new Color(0.17f, 0.17f, 0.20f, 1f);

        // ── Swatch colors (shared by all swatch-drawing code) ──
        public static readonly Color SelectionBorderColor = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color SwatchBorderColor    = new Color(0f, 0f, 0f, 0.5f);
        public static readonly Color SwatchHighlightColor = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color ObstacleBgColor      = new Color(0.10f, 0.10f, 0.10f, 1f);

        // ── Shared styles (lazy-init) ──
        public static GUIStyle GroupTitleStyle { get; private set; }
        public static GUIStyle SubHeaderStyle { get; private set; }
        public static GUIStyle PanelLabelStyle { get; private set; }

        public static void EnsureStyles()
        {
            if (GroupTitleStyle != null) return;
            GroupTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 0, 0),
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
            };
            SubHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset((int)SubHeaderPadLeft + (int)SubHeaderAccentWidth, 2, 1, 1),
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
            };
            PanelLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
            };
        }
    }
}
