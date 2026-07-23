namespace BlockLoop.LevelDesign
{
    internal interface IRightPanelGroup
    {
        float MeasureHeight(float panelWidth);
        float Draw(float startY, float panelWidth);
    }
}
