namespace LivingWorld.Infrastructure;
using System.Text.Json;

public static class DefinitionLoader
{
    private static readonly JsonSerializerOptions Options=new()
    {
        PropertyNameCaseInsensitive=true, ReadCommentHandling=JsonCommentHandling.Skip
    };

    public static DefinitionCatalog Load(string directory)
    {
        var root=Path.GetFullPath(directory);
        return LoadFiles(group=>
        {
            var folder=Path.Combine(root, group);
            if (!Directory.Exists(folder))return Array.Empty<string>();
            return Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(path=>Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path=>path, StringComparer.Ordinal)
                .ToArray();
        }, path=>File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
    }

    public static DefinitionCatalog LoadFiles(Func<string, IEnumerable<string>> listFiles, Func<string, string> read)
    {
        var catalog=new DefinitionCatalog();

        string[] Files(string group)=>listFiles(group)
            .Where(path=>path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path=>path, StringComparer.Ordinal)
            .ToArray();

        T Read<T>(string path)
        {
            var text=read(path);
            return JsonSerializer.Deserialize<T>(text, Options)??throw new InvalidDataException(path+" is empty");
        }

        static void ValidateFileName(string path, string id)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), id, StringComparison.Ordinal))
                throw new InvalidDataException($"Definition id '{id}' must match file name '{Path.GetFileName(path)}'.");
        }

        foreach (var path in Files("Materials"))
        {
            var definition=Read<MaterialDefinition>(path);
            ValidateFileName(path, definition.Id);
            catalog.Materials.Add(definition.Id, definition);
        }

        foreach (var path in Files("Items"))
        {
            var text=read(path);
            var item=JsonSerializer.Deserialize<ItemDefinition>(text, Options)??throw new InvalidDataException(path+" is empty");
            ValidateFileName(path, item.Id);
            catalog.Items.Add(item.Id, item);

            using var document=JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("recipes", out var embeddedRecipes))continue;
            if (embeddedRecipes.ValueKind!=JsonValueKind.Array)throw new InvalidDataException(path+": recipes must be an array.");
            foreach (var element in embeddedRecipes.EnumerateArray())
            {
                if (element.TryGetProperty("output", out _))throw new InvalidDataException(path+": embedded recipes must not declare output.");
                var recipe=element.Deserialize<RecipeDefinition>(Options)??throw new InvalidDataException(path+": invalid recipe.");
                if (string.IsNullOrWhiteSpace(recipe.Id))throw new InvalidDataException(path+": recipe id is required.");
                recipe=recipe with
                {
                    Output=item.Id
                };
                catalog.Recipes.Add(recipe.Id, recipe);
            }
        }

        foreach (var path in Files("Plants"))
        {
            var definition=Read<PlantDefinition>(path);
            ValidateFileName(path, definition.Id);
            catalog.Plants.Add(definition.Id, definition);
        }

        foreach (var path in Files("Buildings"))
        {
            var definition=Read<BuildingDefinition>(path);
            ValidateFileName(path, definition.Id);
            catalog.Buildings.Add(definition.Id, definition);
        }

        var nameFiles=Files("Names");
        if (nameFiles.Length!=1)throw new InvalidDataException("Definitions/Data/Names must contain exactly one JSON file.");
        catalog.Names=Read<NameDefinition>(nameFiles[0]);

        catalog.Validate();
        catalog.Manifest=DefinitionFingerprint.Manifest(catalog);
        catalog.Fingerprint=DefinitionFingerprint.OfManifest(catalog.Manifest);
        return catalog;
    }
}
