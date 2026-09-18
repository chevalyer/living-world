namespace LivingWorld.Infrastructure;
using System.Text.Json;
public static class DefinitionLoader
{
    private static readonly JsonSerializerOptions Options=new()
    {
        PropertyNameCaseInsensitive=true, ReadCommentHandling=JsonCommentHandling.Skip
    };
    public static DefinitionCatalog Load(string directory)=>LoadText(name=>File.ReadAllText(Path.Combine(directory, name)));
    public static DefinitionCatalog LoadText(Func<string, string> read)
    {
        var catalog=new DefinitionCatalog();
        T Read<T>(string file)
        {
            var text=read(file);
            return JsonSerializer.Deserialize<T>(text, Options)??throw new InvalidDataException(file+" is empty");
        }
        foreach (var d in Read<MaterialDefinition[]>("materials.json"))catalog.Materials.Add(d.Id, d);
        foreach (var d in Read<ItemDefinition[]>("items.json"))catalog.Items.Add(d.Id, d);
        foreach (var d in Read<PlantDefinition[]>("plants.json"))catalog.Plants.Add(d.Id, d);
        foreach (var d in Read<RecipeDefinition[]>("recipes.json"))catalog.Recipes.Add(d.Id, d);
        foreach (var d in Read<BuildingDefinition[]>("buildings.json"))catalog.Buildings.Add(d.Id, d);
        catalog.Names=Read<NameDefinition>("names.json");
        catalog.Validate();
        catalog.Manifest=DefinitionFingerprint.Manifest(catalog);
        catalog.Fingerprint=DefinitionFingerprint.OfManifest(catalog.Manifest);
        return catalog;
    }
}
