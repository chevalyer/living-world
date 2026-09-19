namespace LivingWorld.Simulation;
[Component("FarmPlot")]
public sealed class FarmPlotComponent
{
    public List<GridPoint> Cells { get; set; } = [];
}
