namespace LivingWorld.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class DefinitionFingerprint
{
    public static string LegacyRecipeHash(RecipeDefinition recipe)
    {
        var legacy=new LegacyRecipeDefinition(
            recipe.Id,recipe.Name,recipe.Operation,recipe.Inputs,recipe.Output,recipe.Count,
            recipe.Skill,recipe.Knowledge,recipe.Tool,recipe.Minutes);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(legacy)));
    }

    private sealed record LegacyRecipeDefinition(
        string Id,string Name,string Operation,Dictionary<string,int> Inputs,string Output,int Count,
        string Skill,string Knowledge,string Tool,float Minutes);

    public static string OfManifest(Dictionary<string,string> manifest)
        =>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",manifest.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value)))));
    public static Dictionary<string,string> Manifest(DefinitionCatalog catalog)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        void Add<T>(string key,T definition)=>result.Add(key,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(definition))));
        foreach(var d in catalog.Materials.Values)Add("material:"+d.Id,d);
        foreach(var d in catalog.Items.Values)Add("item:"+d.Id,d);
        foreach(var d in catalog.Plants.Values)Add("plant:"+d.Id,d);
        foreach(var d in catalog.Recipes.Values)Add("recipe:"+d.Id,d);
        foreach(var d in catalog.Buildings.Values)Add("building:"+d.Id,d);
        foreach(var d in catalog.Facilities.Values)Add("facility:"+d.Id,d);
        Add("names",catalog.Names);return result;
    }
}
