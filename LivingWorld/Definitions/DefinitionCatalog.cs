namespace LivingWorld.Definitions;
public sealed class DefinitionCatalog
{
    public Dictionary<string, MaterialDefinition> Materials
    { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ItemDefinition> Items
    { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PlantDefinition> Plants
    { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, RecipeDefinition> Recipes
    { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, BuildingDefinition> Buildings
    { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, FacilityDefinition> Facilities
    { get; } = new(StringComparer.Ordinal);
    public NameDefinition Names { get; set; } = new();
    public Dictionary<string,string> Manifest { get; set; } = new(StringComparer.Ordinal);
    public string Fingerprint { get; set; } = "";
    public void Validate()
    {
        foreach (var item in Items.Values)
        {
            if (!Materials.ContainsKey(item.Material) || item.Mass <= 0 || item.Volume <= 0 || item.Durability <= 0) throw new InvalidDataException($"Invalid item: {item.Id}");
            if (item.Coverage is < 0 or > 1 || item.Toxicity is < 0 or > 1) throw new InvalidDataException($"Invalid properties: {item.Id}");
        }
        foreach (var plant in Plants.Values) if (!Items.ContainsKey(plant.Product) || plant.GrowthDays <= 0 || plant.RegrowthDays <= 0) throw new InvalidDataException($"Invalid plant: {plant.Id}");
        foreach (var recipe in Recipes.Values) if (!Items.ContainsKey(recipe.Output) || recipe.Inputs.Count == 0 || recipe.Inputs.Any(x => !Items.ContainsKey(x.Key) || x.Value <= 0)) throw new InvalidDataException($"Invalid recipe: {recipe.Id}");
        foreach (var building in Buildings.Values) if (!Materials.ContainsKey(building.Material) || !Items.ContainsKey(building.Resource) || building.Size < 3) throw new InvalidDataException($"Invalid building: {building.Id}");
        foreach (var facility in Facilities.Values)
        {
            if(string.IsNullOrWhiteSpace(facility.Id)||facility.WorkMinutes<=0||facility.Inputs.Count==0||
               facility.Inputs.Any(x=>!Items.ContainsKey(x.Key)||x.Value<=0)||
               facility.Placement is not ("indoor" or "outdoor")||
               facility.Access is not ("household" or "community")||
               facility.StorageMass<0||facility.StorageVolume<0||facility.RestMultiplier<=0||
               facility.MinMoisture is <0 or >1)
                throw new InvalidDataException($"Invalid facility: {facility.Id}");
        }
        Names.Validate();
    }
}
