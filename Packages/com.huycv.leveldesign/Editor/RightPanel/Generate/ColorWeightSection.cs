using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class ColorWeightSection : IGenerateSection
    {
        const float RowHeight = 28f;
        const float SwatchSize = 22f;
        const float RemoveBtnSize = 22f;
        const float PaletteSwatchSize = 32f;
        const float PaletteSwatchSpacing = 4f;
        const float RandomLabelWidth = 42f;
        const float RandomGap = 6f;

        static readonly Color s_removeBtnBg = new Color(0.45f, 0.20f, 0.20f, 1f);

        static readonly GUIContent s_addColorBtn = new GUIContent("+ Add Color");
        static readonly GUIContent s_removeBtn = new GUIContent("✕");
        static readonly GUIContent s_weightHint = new GUIContent("0–9999");
        static readonly GUIContent s_countHint = new GUIContent("count");
        static readonly GUIContent s_randomizeBtn = new GUIContent("Randomize Colors",
            "Pick N random colors from the palette with the given weight. Replaces the list below — you can still edit each color/weight afterward.");

        static GUIStyle s_removeBtnStyle;

        readonly LevelEditorContext _ctx;
        readonly Action _requestRepaint;
        readonly List<string> _fieldWeights = new List<string>();
        string _fieldRandomCount = "";
        string _fieldRandomWeight = "";

        public ColorWeightSection(LevelEditorContext ctx, Action requestRepaint)
        {
            _ctx = ctx;
            _requestRepaint = requestRepaint;
        }

        public string Title => "Weight";

        public float MeasureHeight(float cw)
        {
            var cfg = _ctx.GenerateConfig;
            float h = RowHeight + RightPanelHelpers.RowSpacing; // Count/Weight randomize row
            h += RightPanelHelpers.ButtonHeight + RightPanelHelpers.RowSpacing; // Randomize Colors button
            h += cfg.ColorWeights.Count * (RowHeight + RightPanelHelpers.RowSpacing);
            h += RightPanelHelpers.ButtonHeight;
            if (cfg.ShowAddColorPalette)
            {
                h += RightPanelHelpers.RowSpacing;
                int palCols = 6;
                int palRows = (_ctx.PaletteCount + palCols - 1) / palCols;
                h += palRows * (PaletteSwatchSize + PaletteSwatchSpacing);
            }
            h += RightPanelHelpers.RowSpacing;
            return h;
        }

        public float Draw(float x, float y, float cw)
        {
            EnsureStyles();
            var cfg = _ctx.GenerateConfig;

            // Count + Weight → Randomize Colors (auto-pick random palette colors; still editable below)
            float half = (cw - RandomGap) * 0.5f;
            int defaultCount = Mathf.Clamp(cfg.ColorWeights.Count > 0 ? cfg.ColorWeights.Count : 5, 1, Mathf.Max(1, _ctx.PaletteCount));

            GUI.Label(new Rect(x, y, RandomLabelWidth, RowHeight), "Count", LevelEditorStyles.PanelLabelStyle);
            var countRect = new Rect(x + RandomLabelWidth, y, half - RandomLabelWidth, RowHeight);
            int randomCount = RightPanelHelpers.IntFieldWithHint(countRect, ref _fieldRandomCount, defaultCount,
                1, Mathf.Max(1, _ctx.PaletteCount), s_countHint);

            float rightHalfX = x + half + RandomGap;
            GUI.Label(new Rect(rightHalfX, y, RandomLabelWidth, RowHeight), "Weight", LevelEditorStyles.PanelLabelStyle);
            var randomWeightRect = new Rect(rightHalfX + RandomLabelWidth, y, half - RandomLabelWidth, RowHeight);
            int randomWeight = RightPanelHelpers.IntFieldWithHint(randomWeightRect, ref _fieldRandomWeight, 100,
                1, 9999, s_weightHint);
            y += RowHeight + RightPanelHelpers.RowSpacing;

            if (GUI.Button(new Rect(x, y, cw, RightPanelHelpers.ButtonHeight),
                s_randomizeBtn, RightPanelHelpers.ButtonStyle))
            {
                DoRandomizeColors(randomCount, randomWeight);
                _requestRepaint?.Invoke();
            }
            y += RightPanelHelpers.ButtonHeight + RightPanelHelpers.RowSpacing;

            while (_fieldWeights.Count < cfg.ColorWeights.Count) _fieldWeights.Add("");
            while (_fieldWeights.Count > cfg.ColorWeights.Count) _fieldWeights.RemoveAt(_fieldWeights.Count - 1);

            int removeIdx = -1;
            for (int i = 0; i < cfg.ColorWeights.Count; i++)
            {
                var entry = cfg.ColorWeights[i];
                float rowX = x;

                var swatchRect = new Rect(rowX, y + (RowHeight - SwatchSize) * 0.5f, SwatchSize, SwatchSize);
                EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(swatchRect, 1f), LevelEditorStyles.SwatchBorderColor);
                Color col = _ctx.ColorLookup.TryGetValue(entry.materialId, out var pc) ? pc : Color.gray;
                EditorGUI.DrawRect(swatchRect, col);
                rowX += SwatchSize + 6f;

                float fieldW = cw - SwatchSize - 6f - RemoveBtnSize - 6f;
                var fieldRect = new Rect(rowX, y, fieldW, RowHeight);
                string wStr = _fieldWeights[i];
                entry.weight = RightPanelHelpers.IntFieldWithHint(fieldRect, ref wStr, entry.weight,
                    0, 9999, s_weightHint);
                _fieldWeights[i] = wStr;
                cfg.ColorWeights[i] = entry;
                rowX += fieldW + 6f;

                var removeBtnRect = new Rect(rowX, y + (RowHeight - RemoveBtnSize) * 0.5f,
                    RemoveBtnSize, RemoveBtnSize);
                EditorGUI.DrawRect(removeBtnRect, s_removeBtnBg);
                GUI.Label(removeBtnRect, s_removeBtn, s_removeBtnStyle);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    removeBtnRect.Contains(Event.current.mousePosition))
                {
                    removeIdx = i;
                    Event.current.Use();
                }

                y += RowHeight + RightPanelHelpers.RowSpacing;
            }

            if (removeIdx >= 0)
            {
                cfg.ColorWeights.RemoveAt(removeIdx);
                _fieldWeights.RemoveAt(removeIdx);
                _requestRepaint?.Invoke();
            }

            // Add Color button
            if (GUI.Button(new Rect(x, y, cw, RightPanelHelpers.ButtonHeight),
                s_addColorBtn, RightPanelHelpers.ButtonStyle))
            {
                cfg.ShowAddColorPalette = !cfg.ShowAddColorPalette;
                _requestRepaint?.Invoke();
            }
            y += RightPanelHelpers.ButtonHeight;

            // Palette picker
            if (cfg.ShowAddColorPalette)
            {
                y += RightPanelHelpers.RowSpacing;
                int palCols = 6;
                float palStep = PaletteSwatchSize + PaletteSwatchSpacing;

                for (int i = 0; i < _ctx.PaletteCount; i++)
                {
                    int col2 = i % palCols;
                    int row = i / palCols;
                    var pr = new Rect(x + col2 * palStep, y + row * palStep,
                        PaletteSwatchSize, PaletteSwatchSize);

                    EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(pr, 1f), LevelEditorStyles.SwatchBorderColor);
                    EditorGUI.DrawRect(pr, _ctx.PaletteEntries[i].color);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                        pr.Contains(Event.current.mousePosition))
                    {
                        int matId = _ctx.PaletteEntries[i].materialId;
                        bool exists = false;
                        for (int j = 0; j < cfg.ColorWeights.Count; j++)
                        {
                            if (cfg.ColorWeights[j].materialId == matId)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            cfg.ColorWeights.Add(new LevelGenerateConfig.ColorWeightEntry
                            {
                                materialId = matId,
                                weight = 0
                            });
                        }

                        cfg.ShowAddColorPalette = false;
                        Event.current.Use();
                        _requestRepaint?.Invoke();
                    }
                }

                int palRows = (_ctx.PaletteCount + palCols - 1) / palCols;
                y += palRows * palStep;
            }

            y += RightPanelHelpers.RowSpacing;
            return y;
        }

        void DoRandomizeColors(int count, int weight)
        {
            if (_ctx.PaletteCount == 0) return;

            count = Mathf.Clamp(count, 1, _ctx.PaletteCount);
            weight = Mathf.Clamp(weight, 1, 9999);

            var indices = new List<int>(_ctx.PaletteCount);
            for (int i = 0; i < _ctx.PaletteCount; i++)
                indices.Add(i);
            ObstacleStrategyHelper.Shuffle(indices, new System.Random());

            var cfg = _ctx.GenerateConfig;
            cfg.ColorWeights.Clear();
            _fieldWeights.Clear();
            for (int i = 0; i < count; i++)
            {
                int materialId = _ctx.PaletteEntries[indices[i]].materialId;
                cfg.ColorWeights.Add(new LevelGenerateConfig.ColorWeightEntry { materialId = materialId, weight = weight });
                _fieldWeights.Add(weight.ToString());
            }
            cfg.ShowAddColorPalette = false;
        }

        static void EnsureStyles()
        {
            if (s_removeBtnStyle != null) return;
            RightPanelHelpers.EnsureStyles();
            s_removeBtnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
        }
    }
}
