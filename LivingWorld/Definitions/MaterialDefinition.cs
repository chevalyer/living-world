namespace LivingWorld.Definitions;
public sealed record MaterialDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public float Density { get; init; }
    public float Insulation { get; init; }
    public float WaterResistance { get; init; }
    public float Strength { get; init; }
    public float Flammability { get; init; }
    public float Flexibility { get; init; }
}
