namespace LivingWorld.Definitions;
public sealed record FacilityDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Placement { get; init; } = "indoor";
    public Dictionary<string,int> Inputs { get; init; } = [];
    public string[] Capabilities { get; init; } = [];
    public string Knowledge { get; init; } = "crafting";
    public string Skill { get; init; } = "crafting";
    public float WorkMinutes { get; init; } = 30;
    public float StorageMass { get; init; }
    public float StorageVolume { get; init; }
    public float RestMultiplier { get; init; } = 1;
    public float MinMoisture { get; init; }
    public string Shape { get; init; } = "workbench";
    public string Color { get; init; } = "#8a7354";
    public string Accent { get; init; } = "#c0a16e";
}
