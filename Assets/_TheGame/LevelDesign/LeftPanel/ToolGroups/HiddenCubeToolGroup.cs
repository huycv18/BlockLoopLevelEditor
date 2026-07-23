using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class HiddenCubeToolGroup : ToolGroup
    {
        static readonly Color s_hiddenBtnBg = new Color(0.30f, 0.28f, 0.38f, 1f);

        static readonly GUIContent s_hiddenBtnContent = new GUIContent("?", "Hidden Cube (3)");

        public HiddenCubeToolGroup(LevelEditorContext ctx)
            : base(ctx, "Hidden Cube", new Color(0.70f, 0.45f, 1.00f, 1f), ToolMode.ToggleHidden) { }

        public override bool IsToggleTool => true;
        public override Color ToggleSwatchColor => s_hiddenBtnBg;
        public override GUIContent ToggleSwatchContent => s_hiddenBtnContent;
        public override string ShortcutKeyLabel => "3";

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (cell.colorId >= 0 && !hasGarage)
            {
                if (isClick)
                {
                    cell.isHidden = !cell.isHidden;
                    Ctx.MarkStatusDirty();
                    Event.current.Use();
                    Ctx.RequestRepaint?.Invoke();
                    return true;
                }
                if (isDrag && !cell.isHidden)
                {
                    cell.isHidden = true;
                    Ctx.MarkStatusDirty();
                    Event.current.Use();
                    Ctx.RequestRepaint?.Invoke();
                    return true;
                }
            }
            else if (isClick)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
