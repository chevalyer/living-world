namespace LivingWorld.Definitions;
public sealed record BuildingDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Material { get; init; } = "wood";
    public string Resource { get; init; } = "log";
    public int UnitsPerElement { get; init; } = 1;
    // Size remains for compatibility with existing definitions and saves.
    public int Size { get; init; } = 5;
    public int MinWidth { get; init; } = 5;
    public int MaxWidth { get; init; } = 9;
    public int MinHeight { get; init; } = 5;
    public int MaxHeight { get; init; } = 9;
    public int MaxInteriorArea { get; init; } = 42;
    public float ShapeVariety { get; init; } = .45f;
    public float WorkMinutes { get; init; } = 14;
}
