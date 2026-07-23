namespace BlockLoop.LevelDesign
{
    internal interface IGenerateSection
    {
        string Title { get; }
        float MeasureHeight(float contentWidth);
        float Draw(float x, float y, float contentWidth);
    }
}
