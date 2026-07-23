using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class ConnectionToolGroup : ToolGroup
    {
        // ── Colors ──
        static readonly Color s_linkBtnBg       = new Color(0.35f, 0.30f, 0.12f, 1f);
        static readonly Color s_linkSelBorder   = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color s_connectorFill   = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color s_connectorBorder = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        static readonly Color s_connectorIcon   = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        static readonly Color s_connectorPreview = new Color(1f, 1f, 1f, 0.35f);

        // ── Constants ──
        const float ConnectorSize = 0.28f;
        const float ConnectorLength = 0.44f;

        // ── Content ──
        static readonly GUIContent s_linkBtnContent = new GUIContent("⇔", "Link Cubes (5)");

        // ── Local state ──
        int _linkFirstIdx = -1;

        public bool HasPendingLink => _linkFirstIdx >= 0;

        public void CancelPendingLink()
        {
            _linkFirstIdx = -1;
        }

        public ConnectionToolGroup(LevelEditorContext ctx)
            : base(ctx, "Connections", new Color(1.00f, 0.85f, 0.30f, 1f), ToolMode.LinkCube) { }

        public override bool IsToggleTool => true;
        public override Color ToggleSwatchColor => s_linkBtnBg;
        public override GUIContent ToggleSwatchContent => s_linkBtnContent;
        public override string ShortcutKeyLabel => "5";

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (!isClick) return false;

            if (_linkFirstIdx < 0)
            {
                if (Ctx.CellHasCube(idx))
                    _linkFirstIdx = idx;
                Event.current.Use();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }

            if (idx != _linkFirstIdx && Ctx.CheckAdjacent(_linkFirstIdx, idx) && Ctx.CellHasCube(idx))
            {
                long edge = LevelEditorDrawUtils.PackEdge(_linkFirstIdx, idx);
                if (!Ctx.Connections.Remove(edge))
                {
                    Ctx.Connections.Add(edge);
                    Ctx.ShowToast?.Invoke("Cubes connected");
                }
                else
                {
                    Ctx.ShowToast?.Invoke("Connection removed");
                }
                Ctx.MarkStatusDirty();
            }
            _linkFirstIdx = -1;
            Event.current.Use();
            Ctx.RequestRepaint?.Invoke();
            return true;
        }

        public override void OnToolChanged(ToolMode newMode, int colorId)
        {
            _linkFirstIdx = -1;
        }

        public override void OnGridResized(int oldWidth, int newWidth, int newHeight)
        {
            if (_linkFirstIdx >= 0)
            {
                int lx = _linkFirstIdx % oldWidth;
                int ly = _linkFirstIdx / oldWidth;
                if (lx >= newWidth || ly >= newHeight)
                    _linkFirstIdx = -1;
                else
                    _linkFirstIdx = ly * newWidth + lx;
            }
        }

        public override void DrawGridOverlayPreHover()
        {
            DrawLinkFirstHighlight();
        }

        public override void DrawGridOverlayPostHover()
        {
            DrawLinkPreview();
        }

        // ════════════════════════════════════════════════════════
        //  Drawing
        // ════════════════════════════════════════════════════════

        void DrawLinkFirstHighlight()
        {
            if (_linkFirstIdx < 0 || _linkFirstIdx >= Ctx.CachedCellCount)
                return;
            LevelEditorDrawUtils.DrawWireRect(Ctx.CellRects[_linkFirstIdx], s_linkSelBorder, 3f);
        }

        void DrawLinkPreview()
        {
            if (_linkFirstIdx < 0 || Ctx.HoverX < 0 || Ctx.HoverY < 0 || Ctx.ActiveTool != ToolMode.LinkCube)
                return;

            int hoverIdx = Ctx.HoverY * Ctx.GridWidth + Ctx.HoverX;
            if (hoverIdx == _linkFirstIdx || hoverIdx >= Ctx.CachedCellCount)
                return;
            if (!Ctx.CheckAdjacent(_linkFirstIdx, hoverIdx) || !Ctx.CellHasCube(hoverIdx))
                return;

            DrawConnectorBlock(Ctx.CellRects[_linkFirstIdx], Ctx.CellRects[hoverIdx], true);
        }

        public void DrawConnectorBlock(Rect a, Rect b, bool isPreview)
        {
            float cx = (a.center.x + b.center.x) * 0.5f;
            float cy = (a.center.y + b.center.y) * 0.5f;
            bool horizontal = Mathf.Abs(a.center.y - b.center.y) < 1f;

            float shortSide = Ctx.CellSize * ConnectorSize;
            float longSide = Ctx.CellSize * ConnectorLength;
            float rw = horizontal ? longSide : shortSide;
            float rh = horizontal ? shortSide : longSide;

            var block = new Rect(cx - rw * 0.5f, cy - rh * 0.5f, rw, rh);
            var fillColor = isPreview ? s_connectorPreview : s_connectorFill;
            var borderColor = isPreview ? s_connectorPreview : s_connectorBorder;

            EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(block, 1f), borderColor);
            EditorGUI.DrawRect(block, fillColor);

            if (!isPreview)
            {
                float barThick = Mathf.Max(2f, shortSide * 0.12f);
                float barLen = Mathf.Max(4f, longSide * 0.45f);
                float gap = Mathf.Max(2f, barThick * 1.2f);
                if (horizontal)
                {
                    EditorGUI.DrawRect(new Rect(cx - barLen * 0.5f, cy - gap, barLen, barThick), s_connectorIcon);
                    EditorGUI.DrawRect(new Rect(cx - barLen * 0.5f, cy + gap - barThick, barLen, barThick), s_connectorIcon);
                }
                else
                {
                    EditorGUI.DrawRect(new Rect(cx - gap, cy - barLen * 0.5f, barThick, barLen), s_connectorIcon);
                    EditorGUI.DrawRect(new Rect(cx + gap - barThick, cy - barLen * 0.5f, barThick, barLen), s_connectorIcon);
                }
            }
        }
    }
}
