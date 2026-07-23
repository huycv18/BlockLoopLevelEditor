using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class ObstacleToolGroup : ToolGroup
    {
        static readonly GUIContent s_obstacleContent = new GUIContent("▧", "Obstacle (2)");

        public ObstacleToolGroup(LevelEditorContext ctx)
            : base(ctx, "Obstacles", new Color(1.00f, 0.40f, 0.35f, 1f), ToolMode.PaintObstacle) { }

        public override bool IsToggleTool => true;
        public override Color ToggleSwatchColor => LevelEditorStyles.ObstacleBgColor;
        public override GUIContent ToggleSwatchContent => s_obstacleContent;
        public override string ShortcutKeyLabel => "2";

        public override bool HandleCellEvent(int idx, int cx, int cy,
            ref CellData cell, bool isClick, bool isDrag, bool hasGarage)
        {
            if (hasGarage) return false;

            if (isClick && cell.isObstacle)
            {
                cell.isObstacle = false;
                Event.current.Use();
                Ctx.MarkStatusDirty();
                Ctx.RequestRepaint?.Invoke();
                return true;
            }

            if (!cell.isObstacle)
            {
                Ctx.RemoveConnectionsForCell(idx);
                cell.isObstacle = true;
                cell.colorId = -1;
                cell.isHidden = false;
                Ctx.VehicleImportData[idx] = default;
                Event.current.Use();
                Ctx.MarkStatusDirty();
                Ctx.RequestRepaint?.Invoke();
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
