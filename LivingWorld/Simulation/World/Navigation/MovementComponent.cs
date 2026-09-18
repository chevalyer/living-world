namespace LivingWorld.Simulation;
[Component("Movement")]
public sealed class MovementComponent
{
    public List<GridPoint> Path { get; set; } = [];
    public GridPoint? Destination { get; set; }
    public int NavigationRevision { get; set; } = -1;
    public int StuckTicks { get; set; }
    public float Progress { get; set; }
    public GridPoint Previous { get; set; }
}
