using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Static draw utilities và bit-packing helpers dùng chung toàn hệ thống.
    /// </summary>
    internal static class LevelEditorDrawUtils
    {
        // ════════════════════════════════════════════════════════
        //  Zero-GC number content cache
        // ════════════════════════════════════════════════════════

        public const int MaxCachedNumberStrings = 100;
        public static readonly GUIContent[] NumberContents = new GUIContent[MaxCachedNumberStrings];

        static LevelEditorDrawUtils()
        {
            for (int i = 0; i < MaxCachedNumberStrings; i++)
                NumberContents[i] = new GUIContent(i.ToString());
        }

        public static GUIContent GetNumberContent(int n)
        {
            return n >= 0 && n < MaxCachedNumberStrings ? NumberContents[n] : new GUIContent(n.ToString());
        }

        // ════════════════════════════════════════════════════════
        //  Rect / draw helpers
        // ════════════════════════════════════════════════════════

        public static Rect ExpandRect(Rect r, float a)
        {
            return new Rect(r.x - a, r.y - a, r.width + a * 2f, r.height + a * 2f);
        }

        public static void DrawWireRect(Rect rect, Color color, float borderWidth)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, borderWidth), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + borderWidth, borderWidth, rect.height - borderWidth * 2f), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - borderWidth, rect.y + borderWidth, borderWidth, rect.height - borderWidth * 2f), color);
        }

        // ════════════════════════════════════════════════════════
        //  Bit-packing helpers (connection edges, coord keys)
        // ════════════════════════════════════════════════════════

        public static long PackEdge(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            return ((long)a << 32) | (uint)b;
        }

        public static void UnpackEdge(long edge, out int a, out int b)
        {
            a = (int)(edge >> 32);
            b = (int)(edge & 0xFFFFFFFFL);
        }

        public static long PackCoordKey(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
