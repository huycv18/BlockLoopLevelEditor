using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal static class RightPanelHelpers
    {
        // ── Weight status bar (shared by color + density) ──
        public const float WeightStatusHeight = 22f;

        static readonly Color s_weightStatusValid = new Color(0.30f, 0.75f, 0.35f, 1f);
        static readonly Color s_weightStatusWarning = new Color(0.90f, 0.70f, 0.20f, 1f);
        static readonly Color s_weightStatusOverflow = new Color(0.95f, 0.35f, 0.30f, 1f);
        internal static readonly Color WeightStatusBgColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        static GUIStyle s_weightStatusStyle;

        public static void DrawWeightStatusBar(Rect barRect, float totalWeight,
            GUIContent content)
        {
            if (s_weightStatusStyle == null)
            {
                s_weightStatusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            EditorGUI.DrawRect(barRect, WeightStatusBgColor);

            Color statusColor;
            bool valid = Mathf.Abs(totalWeight - 1f) < 0.001f;
            if (valid)
            {
                content.text = "Total: 1.0 ✓";
                statusColor = s_weightStatusValid;
            }
            else
            {
                content.text = string.Concat("Total: ",
                    totalWeight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    " / 1.0");
                statusColor = totalWeight > 1f ? s_weightStatusOverflow : s_weightStatusWarning;
            }

            float fillRatio = Mathf.Clamp01(totalWeight);
            if (fillRatio > 0f)
            {
                var fillColor = statusColor;
                fillColor.a = 0.25f;
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y,
                    barRect.width * fillRatio, barRect.height), fillColor);
            }

            EditorGUI.DrawRect(new Rect(barRect.xMax - 1f, barRect.y, 1f, barRect.height),
                new Color(1f, 1f, 1f, 0.15f));

            s_weightStatusStyle.normal.textColor = statusColor;
            GUI.Label(barRect, content, s_weightStatusStyle);
        }

        // ── Shared constants ──
        public const float RowHeight = 32f;
        public const float RowSpacing = 8f;
        public const float ButtonHeight = 38f;
        public const float ButtonSpacing = 8f;
        public const float LabelWidth = 52f;
        public const float SliderLabelWidth = 10f;
        public const float SliderFieldWidth = 42f;

        // ── Shared styles (lazy-init) ──
        public static GUIStyle ButtonStyle { get; private set; }
        public static GUIStyle FieldStyle { get; private set; }
        public static GUIStyle PopupStyle { get; private set; }
        public static GUIStyle LabelStyle { get; private set; }
        public static GUIStyle PlaceholderStyle { get; private set; }

        public static void EnsureStyles()
        {
            LevelEditorStyles.EnsureStyles();
            if (ButtonStyle != null) return;
            ButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 13,
                fixedHeight = 0,
                padding = new RectOffset(8, 8, 4, 4),
            };
            FieldStyle = new GUIStyle(EditorStyles.numberField)
            {
                fontSize = 13,
                fixedHeight = 0,
                alignment = TextAnchor.MiddleCenter,
            };
            PopupStyle = new GUIStyle(EditorStyles.popup)
            {
                fontSize = 13,
                fixedHeight = 0,
                alignment = TextAnchor.MiddleCenter,
            };
            LabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.50f) }
            };
            PlaceholderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.25f) }
            };
        }

        // ── Control name cache (zero-GC) ──
        const int MaxControlNames = 64; // was 32; avoids control-name aliasing with many ColorWeightSection entries plus the Level ID field
        static readonly string[] s_controlNames = new string[MaxControlNames];
        static int s_controlNameCounter;

        static RightPanelHelpers()
        {
            for (int i = 0; i < MaxControlNames; i++)
                s_controlNames[i] = "_rp" + i;
        }

        public static void ResetControlNameCounter() => s_controlNameCounter = 0;

        static string NextControlName()
        {
            var name = s_controlNames[s_controlNameCounter % MaxControlNames];
            s_controlNameCounter++;
            return name;
        }

        // ── Field helpers ──

        public static int IntFieldWithHint(Rect rect, ref string text, int fallback,
            int min, int max, GUIContent hint)
        {
            var name = NextControlName();
            GUI.SetNextControlName(name);
            text = EditorGUI.TextField(rect, text, FieldStyle);
            bool focused = GUI.GetNameOfFocusedControl() == name;
            if (!focused && string.IsNullOrEmpty(text))
                GUI.Label(rect, hint, PlaceholderStyle);
            if (string.IsNullOrEmpty(text))
                return fallback;
            if (int.TryParse(text, out int parsed))
            {
                int clamped = Mathf.Clamp(parsed, min, max);
                if (!focused && clamped != parsed)
                    text = clamped.ToString();
                return clamped;
            }
            return fallback;
        }

        public static float FloatFieldWithHint(Rect rect, ref string text, float fallback,
            float min, float max, GUIContent hint)
        {
            var name = NextControlName();
            GUI.SetNextControlName(name);
            text = EditorGUI.TextField(rect, text, FieldStyle);
            bool focused = GUI.GetNameOfFocusedControl() == name;
            if (!focused && string.IsNullOrEmpty(text))
                GUI.Label(rect, hint, PlaceholderStyle);
            if (string.IsNullOrEmpty(text))
                return fallback;
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                float clamped = Mathf.Clamp(parsed, min, max);
                if (!focused && Mathf.Abs(clamped - parsed) > 0.0001f)
                    text = clamped.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                return clamped;
            }
            return fallback;
        }

        public static int ParseFieldInt(string text, int fallback, int min, int max)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            return int.TryParse(text, out int v) ? Mathf.Clamp(v, min, max) : fallback;
        }

        // ── Slider+Field — zero-GC after first call per (min,max) pair ──

        static readonly Dictionary<long, GUIContent[]> s_sliderLabelCache
            = new Dictionary<long, GUIContent[]>();

        static GUIContent[] GetSliderLabels(float min, float max)
        {
            long key = ((long)Mathf.RoundToInt(min * 1000f) << 32) | (uint)Mathf.RoundToInt(max * 1000f);
            if (!s_sliderLabelCache.TryGetValue(key, out var labels))
            {
                string sMin = min.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                string sMax = max.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                labels = new[] {
                    new GUIContent(sMin),
                    new GUIContent(sMax),
                    new GUIContent(sMin + "–" + sMax)
                };
                s_sliderLabelCache[key] = labels;
            }
            return labels;
        }

        public static float SliderWithField(float x, float y, float totalW, float rowH,
            float value, float min, float max, ref string fieldText, System.Action requestRepaint)
        {
            EnsureStyles();
            var labels = GetSliderLabels(min, max);
            float sliderW = totalW - SliderLabelWidth * 2f - SliderFieldWidth - 12f;

            GUI.Label(new Rect(x, y, SliderLabelWidth, rowH), labels[0], LabelStyle);
            float slX = x + SliderLabelWidth + 2f;

            var sliderRect = new Rect(slX, y + (rowH - EditorGUIUtility.singleLineHeight) * 0.5f,
                sliderW, EditorGUIUtility.singleLineHeight);
            float sliderVal = GUI.HorizontalSlider(sliderRect, value, min, max);

            GUI.Label(new Rect(slX + sliderW + 2f, y, SliderLabelWidth, rowH), labels[1], LabelStyle);

            if (!Mathf.Approximately(sliderVal, value))
            {
                value = Mathf.Round(sliderVal * 100f) / 100f;
                fieldText = value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                requestRepaint?.Invoke();
            }

            var fieldRect = new Rect(slX + sliderW + SliderLabelWidth + 6f, y, SliderFieldWidth, rowH);
            value = FloatFieldWithHint(fieldRect, ref fieldText, value, min, max, labels[2]);

            return value;
        }

        // ── Right panel group layout helpers ──

        public static float MeasureRightGroup(float contentInnerHeight)
        {
            return LevelEditorStyles.GroupTitleHeight + contentInnerHeight
                + LevelEditorStyles.GroupInnerPadding * 2f + LevelEditorStyles.GroupSpacing;
        }

        public static float BeginRightGroup(float startY, float pw, string title, Color accent,
            float contentInnerHeight, out Rect content)
        {
            EnsureStyles();
            float pad = LevelEditorStyles.PanelPadding;
            float titleH = LevelEditorStyles.GroupTitleHeight;
            float w = pw - pad * 2f;

            var headerRect = new Rect(pad, startY, w, titleH);
            EditorGUI.DrawRect(headerRect, LevelEditorStyles.GroupTitleBgColor);
            EditorGUI.DrawRect(new Rect(pad, startY, LevelEditorStyles.GroupAccentBarWidth, titleH), accent);
            GUI.Label(headerRect, title, LevelEditorStyles.GroupTitleStyle);

            float ch = contentInnerHeight + LevelEditorStyles.GroupInnerPadding * 2f;
            content = new Rect(pad, startY + titleH, w, ch);
            EditorGUI.DrawRect(content, LevelEditorStyles.GroupContentBgColor);

            return startY + titleH + ch + LevelEditorStyles.GroupSpacing;
        }
    }
}
