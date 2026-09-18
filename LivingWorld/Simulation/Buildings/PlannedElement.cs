namespace LivingWorld.Simulation;
public sealed record PlannedElement(GridPoint Position, string Kind);
public sealed class Room
{
    public int Id { get; set; }
    public List<GridPoint> Tiles { get; set; } = [];
    public float Insulation { get; set; }
    public float Temperature { get; set; } = 14;
    public bool Enclosed { get; set; }
}
