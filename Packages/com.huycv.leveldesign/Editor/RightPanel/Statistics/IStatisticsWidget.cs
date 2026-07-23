namespace Huycv.LevelDesign
{
    internal interface IStatisticsWidget
    {
        float MeasureHeight(float width);
        void Rebuild(LevelEditorContext ctx);
        void Draw(float x, float y, float width);
    }
}
