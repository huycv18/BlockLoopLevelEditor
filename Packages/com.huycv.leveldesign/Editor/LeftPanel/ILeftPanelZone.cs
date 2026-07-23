namespace Huycv.LevelDesign
{
    internal interface ILeftPanelZone
    {
        float MeasureHeight(float panelWidth);
        float Draw(float startY, float panelWidth);
    }
}
