namespace LivingWorld.Definitions;
public sealed record BuildingDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Material { get; init; } = "wood";
    public string Resource { get; init; } = "log";
    public int UnitsPerElement { get; init; } = 1;
    public int Size { get; init; } = 5;
    public float WorkMinutes { get; init; } = 14;
}
