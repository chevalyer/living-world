namespace LivingWorld.Definitions;
public sealed record PlantDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Product { get; init; } = "";
    public float MinTemperature { get; init; }
    public float MaxTemperature { get; init; } = 35;
    public float FrostTolerance { get; init; } = -25;
    public float MinMoisture { get; init; } = .2f;
    public float GrowthDays { get; init; } = 20;
    public float Yield { get; init; } = 8;
    public float RegrowthDays { get; init; } = 8;
    public float LifespanDays { get; init; } = 3650;
    public string Color { get; init; } = "#638547";
    public float SpawnWeight { get; init; } = 1;
    public float OptimalTemperature { get; init; } = 16;
    public string Shape { get; init; } = "round";
    public string FruitColor { get; init; } = "#c45769";
}
