namespace LivingWorld.Definitions;
public sealed record RecipeDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Operation { get; init; } = "assemble";
    public Dictionary<string, int> Inputs { get; init; } = [];
    public string Output { get; init; } = "";
    public int Count { get; init; } = 1;
    public string Skill { get; init; } = "crafting";
    public string Knowledge { get; init; } = "crafting";
    public string Tool { get; init; } = "";
    public float Minutes { get; init; } = 30;
}
