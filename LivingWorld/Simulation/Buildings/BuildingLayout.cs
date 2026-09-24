namespace LivingWorld.Simulation;
public sealed record BuildingLayout
{
    public List<GridPoint> Interior { get; init; } = [];
    public List<GridPoint> Boundary { get; init; } = [];
    public List<GridPoint> Footprint { get; init; } = [];
    public List<GridPoint> Clearance { get; init; } = [];
    public GridPoint Door { get; init; }
    public GridPoint DoorOutside { get; init; }
    public bool HasDoor { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
