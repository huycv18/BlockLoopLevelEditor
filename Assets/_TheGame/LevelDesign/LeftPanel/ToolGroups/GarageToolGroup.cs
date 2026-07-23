using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class GarageToolGroup : ToolGroup
    {
        static readonly GUIContent s_garageBtnContent = new GUIContent("G", "Garage (4)");

        readonly GaragePopupController _popup;

        public GarageToolGroup(LevelEditorContext ctx, GaragePopupController popup)
            : base(ctx, "Garage", new Color(0.35f, 0.85f, 0.45f, 1f), ToolMode.PlaceGarage)
        {
            _popup = popup;
        }

        public override bool IsToggleTool => true;
        public override Color ToggleSwatchColor => GaragePopupController.s_garageBtnBg;
        public override GUIContent ToggleSwatchContent => s_garageBtnContent;
        public override string ShortcutKeyLabel => "4";

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (isClick && !hasGarage && cell.colorId < 0 && !cell.isObstacle)
            {
                int gid = Ctx.CreateGarage(cx, cy);
                cell.garageId = gid;
                cell.colorId = -1;
                cell.isObstacle = false;
                cell.isHidden = false;
                var windowRect = Ctx.CellRectToWindow != null
                    ? Ctx.CellRectToWindow(Ctx.CellRects[idx])
                    : Ctx.CellRects[idx];
                _popup.Open(gid, windowRect, Ctx.WindowWidth);
                Event.current.Use();
                Ctx.MarkStatusDirty();
                Ctx.RequestRepaint?.Invoke();
                Ctx.ShowToast?.Invoke("Garage created");
                return true;
            }
            if (isClick)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
