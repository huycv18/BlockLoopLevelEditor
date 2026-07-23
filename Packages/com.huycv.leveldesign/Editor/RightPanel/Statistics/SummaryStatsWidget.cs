using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class SummaryStatsWidget : IStatisticsWidget
    {
        const float RowHeight = 32f;
        const float RowSpacing = 8f;
        const float RowPad = 8f;

        static readonly Color s_rowBg = new Color(0.16f, 0.16f, 0.19f, 1f);
        static readonly Color s_rowAltBg = new Color(0.18f, 0.18f, 0.21f, 1f);

        static GUIStyle s_labelStyle;
        static GUIStyle s_valueStyle;

        readonly HashSet<int> _distinctColors = new HashSet<int>();

        string _cubes = "0";
        string _obstacles = "0";
        string _connections = "0";
        string _garages = "0";
        string _colorsUsed = "0 / 0";

        public float MeasureHeight(float width) => RowHeight * 5f + RowSpacing * 4f;

        public void Rebuild(LevelEditorContext ctx)
        {
            int cubes = 0, obstacles = 0;
            _distinctColors.Clear();
            int total = ctx.GridWidth * ctx.GridHeight;
            for (int i = 0; i < total; i++)
            {
                if (ctx.Cells[i].isObstacle)
                    obstacles++;
                else if (ctx.Cells[i].colorId >= 0 && ctx.Cells[i].garageId < 0)
                {
                    cubes++;
                    _distinctColors.Add(ctx.Cells[i].colorId);
                }
            }
            foreach (var kv in ctx.GarageMap)
            {
                var cars = kv.Value.carColors;
                cubes += cars.Count;
                for (int i = 0; i < cars.Count; i++)
                    _distinctColors.Add(cars[i]);
            }
            _cubes = cubes.ToString();
            _obstacles = obstacles.ToString();
            _connections = ctx.Connections.Count.ToString();
            _garages = ctx.GarageMap.Count.ToString();
            _colorsUsed = string.Concat(_distinctColors.Count.ToString(), " / ", ctx.PaletteCount.ToString());
        }

        public void Draw(float x, float y, float width)
        {
            EnsureStyles();

            DrawRow(x, y, width, "Cubes", _cubes, false);
            y += RowHeight + RowSpacing;
            DrawRow(x, y, width, "Obstacles", _obstacles, true);
            y += RowHeight + RowSpacing;
            DrawRow(x, y, width, "Connections", _connections, false);
            y += RowHeight + RowSpacing;
            DrawRow(x, y, width, "Garages", _garages, true);
            y += RowHeight + RowSpacing;
            DrawRow(x, y, width, "Colors Used", _colorsUsed, false);
        }

        static void DrawRow(float x, float y, float w, string label, string value, bool alt)
        {
            var rowRect = new Rect(x, y, w, RowHeight);
            EditorGUI.DrawRect(rowRect, alt ? s_rowAltBg : s_rowBg);
            GUI.Label(new Rect(x + RowPad, y, w * 0.55f - RowPad, RowHeight), label, s_labelStyle);
            GUI.Label(new Rect(x + w * 0.55f, y, w * 0.45f - RowPad, RowHeight), value, s_valueStyle);
        }

        static void EnsureStyles()
        {
            if (s_labelStyle != null) return;
            s_labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.60f) }
            };
            s_valueStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
        }
    }
}
