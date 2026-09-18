namespace LivingWorld.Infrastructure;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
public sealed class SaveService
{
    public const int CurrentVersion=1;
    private static readonly JsonSerializerOptions Options=new()
    {
        IgnoreReadOnlyProperties=true, WriteIndented=false, MaxDepth=64
    };
    private static readonly Dictionary<string, Type> ComponentTypes=typeof(WorldState).Assembly.GetTypes().Where(t=>t.GetCustomAttribute<ComponentAttribute>() is not null).ToDictionary(t=>t.GetCustomAttribute<ComponentAttribute>()!.Id, t=>t, StringComparer.Ordinal);
    private readonly List<ISaveMigration> _migrations=[];
    public void RegisterMigration(ISaveMigration migration)=>_migrations.Add(migration);
    public WorldSnapshot Capture(SimulationSession session)
    {
        var s=session.State;
        var snapshot=new WorldSnapshot
        {
            Seed=s.Seed, DecisionCursor=s.DecisionCursor, GenerationVersion=s.GenerationVersion, NextEntityId=s.Entities.NextId, EntityIds=s.Entities.All.ToArray(), Clock=s.Clock, Map=s.Map, Weather=s.Weather, Start=s.Start, RandomStates=s.Random.Capture(), Rooms=s.Rooms, Journal=s.Journal, Reservations=session.Reservations.Entries, RoomsDirty=session.RoomsDirty, DefinitionsFingerprint=session.Definitions.Fingerprint,DefinitionManifest=new(session.Definitions.Manifest,StringComparer.Ordinal)
        };
        foreach (var store in s.Entities.Stores)
        {
            if (!store.Entries().Any())continue;
            var key=store.ComponentType.GetCustomAttribute<ComponentAttribute>()?.Id??throw new InvalidOperationException("Component has no stable save id: "+store.ComponentType.Name);
            snapshot.Components[key]=store.Entries().ToDictionary(x=>x.Key, x=>JsonSerializer.SerializeToElement(x.Value, store.ComponentType, Options));
        }
        return snapshot;
    }
    public string Serialize(SimulationSession session)=>JsonSerializer.Serialize(Capture(session), Options);
    public string Hash(SimulationSession session)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(session))));
    public void Save(SimulationSession session, string path)
    {
        var full=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using (var stream=new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, Capture(session), Options);
                stream.Flush(true);
            }
            File.Move(temporary, full, true);
        }
        finally
        {
            if (File.Exists(temporary))File.Delete(temporary);
        }
    }
    public SimulationSession Load(string path, DefinitionCatalog definitions)
    {
        if (new FileInfo(path).Length>256L*1024*1024)throw new InvalidDataException("Save exceeds 256 MiB.");
        return Deserialize(File.ReadAllText(path), definitions);
    }
    public SimulationSession Deserialize(string json, DefinitionCatalog definitions)
    {
        using var document=JsonDocument.Parse(json);
        var version=document.RootElement.GetProperty("FormatVersion").GetInt32();
        if (version>CurrentVersion)throw new InvalidDataException("Сохранение создано более новой версией игры.");
        while (version<CurrentVersion)
        {
            var migration=_migrations.FirstOrDefault(m=>m.FromVersion==version&&m.ToVersion>version)??throw new InvalidDataException("Нет миграции формата "+version);
            json=migration.Migrate(json);
            version=migration.ToVersion;
        }
        var snapshot=JsonSerializer.Deserialize<WorldSnapshot>(json, Options)??throw new InvalidDataException("Пустое сохранение.");
        if(snapshot.DefinitionManifest.Count==0)
        {if(snapshot.DefinitionsFingerprint!=definitions.Fingerprint)throw new InvalidDataException("Определения изменились; нужна миграция.");}
        else
        {
            if(DefinitionFingerprint.OfManifest(snapshot.DefinitionManifest)!=snapshot.DefinitionsFingerprint)throw new InvalidDataException("Нарушена целостность определений сохранения.");
            foreach(var entry in snapshot.DefinitionManifest)
            {
                // Names already live as strings on identities. New phonetic rules only affect future births.
                // Verify the old manifest above, then permit this one presentation-only definition to evolve.
                if(entry.Key=="names")continue;
                if(definitions.Manifest.TryGetValue(entry.Key,out var current)&&current==entry.Value)continue;
                if(entry.Key.StartsWith("recipe:",StringComparison.Ordinal))
                {
                    var id=entry.Key["recipe:".Length..];
                    if(definitions.Recipes.TryGetValue(id,out var recipe)&&
                       DefinitionFingerprint.LegacyRecipeHash(recipe)==entry.Value)continue;
                }
                throw new InvalidDataException("Изменено старое определение "+entry.Key+"; нужна миграция.");
            }
        }
        if (snapshot.Map.Width<1||snapshot.Map.Height<1||snapshot.Map.Width>512||snapshot.Map.Height>512||snapshot.Map.Tiles.Length!=snapshot.Map.Width*snapshot.Map.Height) throw new InvalidDataException("Повреждены размеры карты.");
        var state=new WorldState
        {
            Seed=snapshot.Seed, DecisionCursor=snapshot.DecisionCursor, GenerationVersion=snapshot.GenerationVersion, Clock=snapshot.Clock, Map=snapshot.Map, Weather=snapshot.Weather, Start=snapshot.Start, Random=new(snapshot.Seed), Rooms=snapshot.Rooms, Journal=snapshot.Journal
        };
        foreach (var id in snapshot.EntityIds)
        {
            if (id<=0||state.Entities.Exists(id))throw new InvalidDataException("Invalid entity id.");
            state.Entities.RestoreIdentity(id);
        }
        if (snapshot.NextEntityId<state.Entities.NextId)throw new InvalidDataException("Invalid entity counter.");
        state.Entities.NextId=snapshot.NextEntityId;
        foreach (var (key, entries) in snapshot.Components)
        {
            if (!ComponentTypes.TryGetValue(key, out var type))throw new InvalidDataException("Неизвестный компонент: "+key);
            var store=state.Entities.Store(type);
            foreach (var (id, element) in entries)
            {
                if (!state.Entities.Exists(id))throw new InvalidDataException("Orphan component.");
                store.SetObject(id, element.Deserialize(type, Options)??throw new InvalidDataException(key));
            }
        }
        state.Random.Restore(snapshot.RandomStates);
        foreach (var (_, knowledge) in state.Entities.Store<KnowledgeComponent>().All)
        {
            knowledge.Facts=new(knowledge.Facts, StringComparer.Ordinal);
            knowledge.EdiblePlants=new(knowledge.EdiblePlants, StringComparer.Ordinal);
        }
        foreach (var (id, item) in state.Entities.Store<ItemComponent>().All)
        {
            if (!definitions.Items.ContainsKey(item.Definition)||item.Holder!=0&&!state.Entities.Exists(item.Holder))throw new InvalidDataException("Invalid item reference.");
        }
        SaveValidator.Validate(state,definitions);
        var session=new SimulationSession(state, definitions)
        {
            RoomsDirty=snapshot.RoomsDirty
        };
        session.Reservations.Entries=snapshot.Reservations;
        return session;
    }
}
