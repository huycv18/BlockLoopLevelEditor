using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class GenerateCategory
    {
        const float CategoryHeaderHeight = 30f;
        const float SectionTitleHeight = 24f;
        const float SectionSpacing = 4f;

        const float SectionPad = 4f;

        static readonly Color s_contentBg = new Color(0.15f, 0.15f, 0.18f, 1f);
        static readonly Color s_sectionBg = new Color(0.13f, 0.13f, 0.16f, 1f);

        static GUIStyle s_categoryHeaderStyle;
        static GUIStyle s_sectionTitleStyle;

        readonly Color _accent;
        readonly Color _accentFaded;
        readonly GUIContent _titleContent;
        readonly IGenerateSection[] _sections;
        readonly GUIContent[] _sectionTitleContents;

        public GenerateCategory(string title, Color accent, IGenerateSection[] sections)
        {
            _accent = accent;
            _accentFaded = new Color(accent.r, accent.g, accent.b, 0.6f);
            _titleContent = new GUIContent(title);
            _sections = sections;
            _sectionTitleContents = new GUIContent[sections.Length];
            for (int i = 0; i < sections.Length; i++)
                _sectionTitleContents[i] = new GUIContent(sections[i].Title);
        }

        float SectionBlockHeight(int i, float cw)
        {
            return SectionTitleHeight + SectionPad + _sections[i].MeasureHeight(cw - SectionPad * 2f) + SectionPad;
        }

        float ComputeInnerHeight(float cw)
        {
            float h = 0f;
            for (int i = 0; i < _sections.Length; i++)
            {
                h += SectionBlockHeight(i, cw);
                if (i < _sections.Length - 1)
                    h += SectionSpacing;
            }
            return h;
        }

        public float MeasureHeight(float cw)
        {
            return CategoryHeaderHeight
                + LevelEditorStyles.GroupInnerPadding * 2f
                + ComputeInnerHeight(cw);
        }

        public float Draw(float x, float y, float cw)
        {
            EnsureStyles();
            float innerH = ComputeInnerHeight(cw);
            float contentH = innerH + LevelEditorStyles.GroupInnerPadding * 2f;

            var headerRect = new Rect(x, y, cw, CategoryHeaderHeight);
            EditorGUI.DrawRect(headerRect, LevelEditorStyles.SubHeaderBgColor);
            EditorGUI.DrawRect(new Rect(x, y, LevelEditorStyles.SubHeaderAccentWidth,
                CategoryHeaderHeight), _accent);
            GUI.Label(headerRect, _titleContent, s_categoryHeaderStyle);
            y += CategoryHeaderHeight;

            var contentRect = new Rect(x, y, cw, contentH);
            EditorGUI.DrawRect(contentRect, s_contentBg);

            float ix = x + LevelEditorStyles.GroupInnerPadding;
            float iw = cw - LevelEditorStyles.GroupInnerPadding * 2f;
            y += LevelEditorStyles.GroupInnerPadding;

            for (int i = 0; i < _sections.Length; i++)
            {
                float blockH = SectionBlockHeight(i, iw);
                EditorGUI.DrawRect(new Rect(ix, y, iw, blockH), s_sectionBg);

                EditorGUI.DrawRect(new Rect(ix, y + 4f, 2f, SectionTitleHeight - 8f), _accentFaded);
                GUI.Label(new Rect(ix + 6f, y, iw - 6f, SectionTitleHeight),
                    _sectionTitleContents[i], s_sectionTitleStyle);
                y += SectionTitleHeight + SectionPad;

                float sectionW = iw - SectionPad * 2f;
                y = _sections[i].Draw(ix + SectionPad, y, sectionW);
                y += SectionPad;

                if (i < _sections.Length - 1)
                    y += SectionSpacing;
            }

            y += LevelEditorStyles.GroupInnerPadding;
            return y;
        }

        static void EnsureStyles()
        {
            if (s_categoryHeaderStyle != null) return;
            LevelEditorStyles.EnsureStyles();
            s_categoryHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(
                    (int)LevelEditorStyles.SubHeaderPadLeft + (int)LevelEditorStyles.SubHeaderAccentWidth,
                    2, 1, 1),
                normal = { textColor = new Color(1f, 1f, 1f, 0.65f) }
            };
            s_sectionTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.50f) }
            };
        }
    }
}
