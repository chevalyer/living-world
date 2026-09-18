namespace LivingWorld.Definitions;
public sealed record ItemDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Material { get; init; } = "";
    public float Mass { get; init; }
    public float Volume { get; init; }
    public float Durability { get; init; } = 100;
    public float Calories { get; init; }
    public float Water { get; init; }
    public float Toxicity { get; init; }
    public float ShelfLifeDays { get; init; }
    public string[] Slots { get; init; } = [];
    public float Coverage { get; init; }
    public float Thickness { get; init; }
    public float Comfort { get; init; } = 1;
    public Dictionary<string, float> Tools { get; init; } = [];
    public string[] Tags { get; init; } = [];
}
