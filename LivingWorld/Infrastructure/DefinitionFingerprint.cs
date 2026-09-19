namespace LivingWorld.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class DefinitionFingerprint
{
    public static string OfManifest(Dictionary<string,string> manifest)
        =>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",manifest.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value)))));
    public static Dictionary<string,string> Manifest(DefinitionCatalog catalog)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        void Add<T>(string key,T definition)=>result.Add(key,Hash(definition));
        foreach(var d in catalog.Materials.Values)Add("material:"+d.Id,d);
        foreach(var d in catalog.Items.Values)Add("item:"+d.Id,d);
        foreach(var d in catalog.Plants.Values)Add("plant:"+d.Id,d);
        foreach(var d in catalog.Recipes.Values)Add("recipe:"+d.Id,d);
        foreach(var d in catalog.Buildings.Values)Add("building:"+d.Id,d);
        Add("names",catalog.Names);return result;
    }

    public static string LegacyBuildingHash(BuildingDefinition building)=>Hash(new LegacyBuildingDefinition(
        building.Id,building.Name,building.Material,building.Resource,building.UnitsPerElement,
        building.Size,building.WorkMinutes));

    public static string LegacyPlantHash(PlantDefinition plant)=>Hash(new LegacyPlantDefinition(
        plant.Id,plant.Name,plant.Kind,plant.Product,plant.MinTemperature,plant.MaxTemperature,
        plant.FrostTolerance,plant.MinMoisture,plant.GrowthDays,plant.Yield,plant.RegrowthDays,
        plant.LifespanDays,plant.Color,plant.SpawnWeight,plant.OptimalTemperature,plant.Shape,plant.FruitColor));

    public static string LegacyFingerprint(DefinitionCatalog catalog)
    {
        var manifest=Manifest(catalog);
        foreach(var building in catalog.Buildings.Values)
            manifest["building:"+building.Id]=LegacyBuildingHash(building);
        foreach(var plant in catalog.Plants.Values)
            manifest["plant:"+plant.Id]=LegacyPlantHash(plant);
        return OfManifest(manifest);
    }

    private static string Hash<T>(T value)=>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private sealed record LegacyBuildingDefinition(
        string Id,string Name,string Material,string Resource,int UnitsPerElement,int Size,float WorkMinutes);

    private sealed record LegacyPlantDefinition(
        string Id,string Name,string Kind,string Product,float MinTemperature,float MaxTemperature,
        float FrostTolerance,float MinMoisture,float GrowthDays,float Yield,float RegrowthDays,
        float LifespanDays,string Color,float SpawnWeight,float OptimalTemperature,string Shape,string FruitColor);
}
