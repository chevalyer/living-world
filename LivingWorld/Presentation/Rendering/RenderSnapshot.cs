using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using LivingWorld.Simulation;

namespace LivingWorld.Presentation;

// These records contain only values and read-only collections. They never expose live components.
public readonly record struct RenderTile(float Height, Biome Biome, WaterKind Water, float Snow, float Ice, float Traffic, float Fertility, bool Farm, bool Tilled);
public readonly record struct RenderEntity(int Id, GridPoint Tile, string Kind, string Color, string Accent,
    string Shape, float Growth, float Yield);
public readonly record struct RenderPerson(int Id, GridPoint Tile, string Name, string Action,
    string HeldShape, string HeldColor, string HeldAccent, int Appearance,
    bool Child, bool Alive, bool Moving, bool Sleeping, bool Pregnant);
public readonly record struct RenderMemory(GridPoint Tile, float Confidence);
public sealed record RenderChunk(int Key, int X, int Y, long Revision,
    ReadOnlyCollection<RenderTile> Tiles, ReadOnlyCollection<RenderEntity> Entities);
public readonly record struct RenderSettlementArea(int X,int Y);
public sealed record RenderSettlement(
    int Anchor, string Name, GridPoint Center,
    ReadOnlyCollection<RenderSettlementArea> Areas,
    int Homes, int Members, int Families, float FoodCalories, int Projects, int Facilities,
    ReadOnlyCollection<string> Capabilities, ReadOnlyCollection<string> Specializations);
public sealed record RenderRoom(int Id, ReadOnlyCollection<GridPoint> Tiles);
public sealed record InspectorSnapshot(string Title, string Text);
public sealed record ViewRequest(int Selected = 0, GridPoint? Tile = null, bool Debug = false);

public sealed record RenderSnapshot(
    long Generation, long Sequence, long PublishedAt, long Tick, DateTime Date, int Seed, int Width, int Height,
    GridPoint Start, float Sunlight, string Season, float Air, bool Rain, int Population, int Homes, int Rooms,
    string LastEvent, bool Paused, int Speed, double TicksPerSecond,
    ReadOnlyCollection<RenderChunk> Chunks, ReadOnlyCollection<RenderPerson> People,
    ReadOnlyCollection<RenderSettlement> Settlements, ReadOnlyCollection<RenderRoom> RoomAreas,
    InspectorSnapshot Inspector, string WorldText, string SystemsText,
    ReadOnlyCollection<GridPoint> Path, ReadOnlyCollection<RenderMemory> Memories)
{
    public int Columns => (Width + 15) / 16;
    public bool Contains(GridPoint point) => point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;
    public static int DetailLevel(float pixelsPerTile) => pixelsPerTile >= 12 ? 0 : pixelsPerTile >= 6 ? 1 : 2;
}

// Owned exclusively by the simulation thread. Unchanged chunk payloads are shared between publications.
public sealed class RenderSnapshotBuilder
{
    private RenderChunk[] _chunks = [];
    private int[] _mapRevisions = [];
    private RenderSettlement[] _settlements = [];
    private WorldMap? _map;
    private long _revision;
    private long _sequence;
    private long _lastStatic;
    private ViewRequest? _lastRequest;
    private InspectorSnapshot _inspector = new("местность", "Выбери жителя или клетку.");
    private string _worldText = "", _systemsText = "";
    private int _population, _homes;

    public RenderSnapshot Capture(SimulationSession session, long generation, bool paused, int speed,
        double ticksPerSecond, ViewRequest request, bool force = false)
    {
        var s = session.State;
        var map = s.Map;
        var now = Stopwatch.GetTimestamp();
        if (!ReferenceEquals(_map, map))
        {
            _map = map;
            _chunks = new RenderChunk[((map.Width + 15) / 16) * ((map.Height + 15) / 16)];
            _mapRevisions = Enumerable.Repeat(-1, _chunks.Length).ToArray();
            force = true;
        }
        if (force || Stopwatch.GetElapsedTime(_lastStatic, now).TotalSeconds >= .25)
        {
            RefreshChunks(session);
            _population = s.Population;
            _homes = s.Entities.Store<ConstructionComponent>().All.Count(x => x.Value.Finished);
            _settlements = SettlementAnalyzer.DescribeAll(session).Select(x=>new RenderSettlement(
                x.Anchor,x.Name,x.Center,
                Array.AsReadOnly(x.Areas.Select(area=>new RenderSettlementArea(area.X,area.Y)).ToArray()),
                x.Homes,x.Members,x.Families,x.FoodCalories,x.Projects,x.Facilities,
                Array.AsReadOnly(x.Capabilities.ToArray()),Array.AsReadOnly(x.Specializations.ToArray()))).ToArray();
            _worldText = DescribeWorld(session);
            _systemsText = DescribeSystems(session);
            _inspector = DescribeSelection(session, request);
            _lastStatic = now;
            _lastRequest = request;
        }
        else if (_lastRequest != request)
        {
            _inspector = DescribeSelection(session, request);
            _lastRequest = request;
        }

        var people = new List<RenderPerson>();
        foreach (var (id, identity) in s.Entities.Store<IdentityComponent>().All)
        {
            var e = s.Entities;
            var step=e.Get<DecisionComponent>(id).Plan.FirstOrDefault();
            var action=step is null?"наблюдает":session.Actions[step.Action].Label;
            var held=HeldVisual(session,id,step);
            people.Add(new(id,e.Get<PositionComponent>(id).Tile,identity.FullName,action,
                held.Shape,held.Color,held.Accent,identity.Appearance,
                s.Clock.Age(identity.BirthDate)<18,e.Get<HealthComponent>(id).Alive,
                e.Get<MovementComponent>(id).Path.Count > 0,
                step?.Action == "sleep",
                e.Get<FamilyComponent>(id).PregnancyDueTick.HasValue));
        }

        var roomAreas=s.Rooms.Select(room=>new RenderRoom(room.Id,Array.AsReadOnly(room.Tiles.ToArray()))).ToArray();
        GridPoint[] path = [];
        RenderMemory[] memories = [];
        if (request.Debug && s.Entities.Has<IdentityComponent>(request.Selected))
        {
            path = s.Entities.Get<MovementComponent>(request.Selected).Path.ToArray();
            memories = s.Entities.Get<MemoryComponent>(request.Selected).Observations
                .Where(o => o.Kind is "water" or "plant" or "farm_cell").Select(o => new RenderMemory(o.Position, o.Confidence)).ToArray();
        }
        return new(generation, ++_sequence, now, s.Clock.Tick, s.Clock.Now, s.Seed, map.Width, map.Height,
            s.Start, s.Weather.Sunlight, s.Clock.Season, EnvironmentQueries.Air(s, s.Start), s.Weather.Rain > .1f,
            _population, _homes, s.Rooms.Count, s.Journal.LastOrDefault()?.Text ?? "мир просыпается", paused, speed,
            ticksPerSecond, Array.AsReadOnly((RenderChunk[])_chunks.Clone()), people.AsReadOnly(),
            Array.AsReadOnly((RenderSettlement[])_settlements.Clone()), Array.AsReadOnly(roomAreas),
            _inspector, _worldText, _systemsText, Array.AsReadOnly(path), Array.AsReadOnly(memories));
    }

    private static (string Shape,string Color,string Accent) HeldVisual(SimulationSession session,int actor,ActionStep? step)
    {
        if(step is null)return ("","","");
        var e=session.State.Entities;
        string definition="";
        string tool=step.Action switch
        {
            "chop"=>"chop",
            "mine" or "break_ice"=>"mine",
            _=>""
        };
        if(tool.Length>0)
        {
            definition=e.Get<InventoryComponent>(actor).Items
                .Where(id=>e.Get<ItemComponent>(id).Durability>0&&
                    session.Definitions.Items[e.Get<ItemComponent>(id).Definition].Tools.ContainsKey(tool))
                .OrderByDescending(id=>session.Definitions.Items[e.Get<ItemComponent>(id).Definition].Tools[tool])
                .Select(id=>e.Get<ItemComponent>(id).Definition)
                .FirstOrDefault()??"";
        }
        else if(step.Action=="build"&&e.Try<ConstructionComponent>(step.Target) is { } project)
            definition=session.Definitions.Buildings[project.Definition].Resource;
        else if(step.Action=="craft"&&session.Definitions.Recipes.TryGetValue(step.Argument,out var recipe))
        {
            if(recipe.Tool.Length>0)
                definition=e.Get<InventoryComponent>(actor).Items
                    .Where(id=>e.Get<ItemComponent>(id).Durability>0&&
                        session.Definitions.Items[e.Get<ItemComponent>(id).Definition].Tools.ContainsKey(recipe.Tool))
                    .Select(id=>e.Get<ItemComponent>(id).Definition).FirstOrDefault()??"";
            if(definition.Length==0)definition=recipe.Inputs.Keys.FirstOrDefault()??"";
        }
        else if(step.Action=="trade")definition=step.Argument.Split('|',2)[0];
        else if(step.Action=="sow"&&session.Definitions.Plants.TryGetValue(step.Argument,out var crop))
            definition=crop.Seed.Length>0?crop.Seed:crop.Product;
        else if(step.Action=="build_facility"&&session.Definitions.Facilities.TryGetValue(step.Argument,out var facility))
            definition=facility.Inputs.Keys.FirstOrDefault()??"";
        else if(step.Action is "eat" or "give" or "care" or "deposit" or "refuel" or "light_fire" or "repair")
            definition=session.Definitions.Items.ContainsKey(step.Argument)?step.Argument:"";

        if(definition.Length==0||!session.Definitions.Items.TryGetValue(definition,out var item))return ("","","");
        var shape=step.Action=="sow"?"seed":
            tool=="chop"?"axe":tool=="mine"?"pick":
            item.Tags.Contains("construction",StringComparer.Ordinal)||item.Tags.Contains("fuel",StringComparer.Ordinal)?"bulk":
            item.Tags.Contains("food",StringComparer.Ordinal)?"food":
            item.Tags.Contains("seed",StringComparer.Ordinal)?"seed":"item";
        var color=item.Material switch
        {
            "wood"=>"#8d704d",
            "stone"=>"#9b9c94",
            "iron"=>"#a7aaa8",
            "flax"=>"#c8b98b",
            "wool"=>"#d8d1bd",
            "organic"=>"#c8a660",
            _=>"#d4c9ab"
        };
        var accent=shape switch
        {
            "axe" or "pick"=>item.Material=="iron"?"#d0d2cf":"#aaa9a0",
            "food"=>"#d8b16b",
            "seed"=>"#b89d61",
            _=>"#b99c6a"
        };
        return (shape,color,accent);
    }

    private void RefreshChunks(SimulationSession session)
    {
        var s = session.State;
        var columns = (s.Map.Width + 15) / 16;
        for (var key = 0; key < _chunks.Length; key++)
        {
            var cx = key % columns;
            var cy = key / columns;
            var previous = _chunks[key];
            var entities = new List<RenderEntity>();
            // A radius of zero addresses exactly one spatial chunk.
            foreach (var id in session.Spatial.Query(new(cx * 16, cy * 16), 0))
            {
                var e = s.Entities;
                var position = e.Get<PositionComponent>(id).Tile;
                if (e.Try<PlantComponent>(id) is { } plant)
                {
                    var d = session.Definitions.Plants[plant.Definition];
                    entities.Add(new(id, position, d.Kind, d.Color, d.FruitColor, d.Shape,
                        MathF.Round(plant.Growth * 16) / 16, (int)plant.Yield));
                }
                else if (e.Try<ResourceComponent>(id) is { } resource)
                    entities.Add(new(id, position, "resource", "#777b79", resource.Product == "iron_ore" ? "#a68b78" : "#a7aaa0", "", 1, 0));
                else if (e.Try<ItemComponent>(id) is { } item)
                {
                    var d = session.Definitions.Items[item.Definition];
                    var bulk = d.Tags.Contains("construction", StringComparer.Ordinal) || d.Tags.Contains("fuel", StringComparer.Ordinal);
                    entities.Add(new(id, position, "item", d.Calories > 0 ? "#d9b476" : "#ded9c8",
                        d.Material == "wood" ? "#90724e" : "#b6b5a6", bulk ? "bulk" : "", 1, 0));
                }
                else if (e.Try<BuildingElementComponent>(id) is { } part)
                    entities.Add(new(id, position, part.Kind, part.Material == "stone" ? "#a5a59a" : "#a38b67", "", "", 1, 0));
                else if(e.Try<FacilityComponent>(id) is { } facility)
                {
                    var definition=session.Definitions.Facilities[facility.Definition];
                    entities.Add(new(id,position,"facility",definition.Color,definition.Accent,definition.Shape,1,0));
                }
                else if (e.Try<ConstructionComponent>(id) is { Finished: false } project)
                    foreach (var planned in project.Elements.Skip(project.Completed).Where(p => p.Kind == "wall"))
                        entities.Add(new(id, planned.Position, "blueprint", "#b6c4ad", "", "", 1, 0));
                else if (e.Has<StorageComponent>(id))
                    entities.Add(new(id, position, "storage", "#775c3f", "#bb9b66", "", 1, 0));
                else if (e.Try<FireComponent>(id) is { } fire)
                    entities.Add(new(id, position, "fire", "#d68b4b", "#eacb80", "", 1, fire.FuelMinutes > 0 ? 1 : 0));
            }
            entities.Sort((a, b) =>
            {
                var order = a.Tile.Y.CompareTo(b.Tile.Y);
                return order != 0 ? order : a.Id.CompareTo(b.Id);
            });
            var revision = s.Map.ChunkVisualRevision(key);
            var terrainChanged = previous is null || revision != _mapRevisions[key];
            var objectsChanged = previous is null || !previous.Entities.SequenceEqual(entities);
            if (!terrainChanged && !objectsChanged) continue;
            var tiles = previous?.Tiles;
            if (terrainChanged)
            {
                var values = new RenderTile[256];
                for (var y = 0; y < 16; y++) for (var x = 0; x < 16; x++)
                {
                    var point = new GridPoint(cx * 16 + x, cy * 16 + y);
                    if (!s.Map.Contains(point)) continue;
                    var t = s.Map[point];
                    values[y * 16 + x] = new(t.Height, t.Biome, t.Water, t.Snow, t.Ice, t.Traffic, t.Fertility, t.FarmPlot!=0, t.Tilled);
                }
                tiles = Array.AsReadOnly(values);
            }
            _mapRevisions[key] = revision;
            _chunks[key] = new(key, cx, cy, ++_revision, tiles!, entities.AsReadOnly());
        }
    }

    private static InspectorSnapshot DescribeSelection(SimulationSession session, ViewRequest request)
    {
        var s = session.State;
        if (s.Entities.Has<IdentityComponent>(request.Selected)) return DescribePerson(session, request.Selected);
        if(request.Tile is { } p&&s.Map.Contains(p))
        {
            var t=s.Map[p];
            var text=new StringBuilder();
            text.AppendLine($"клетка {p.X}, {p.Y}");
            text.AppendLine($"\n{BiomeName(t.Biome)}");
            text.AppendLine($"высота {t.Height:F3}");
            text.AppendLine($"влажность {t.Moisture:P0}");
            text.AppendLine($"плодородие {t.Fertility:P0}");
            text.AppendLine($"вода {t.Water}");
            text.AppendLine($"лед {t.Ice*100:F1} см");
            text.AppendLine($"снег {t.Snow:P0}");
            text.AppendLine($"грядка {(t.FarmPlot==0?"нет":t.Tilled?"вспахана":"да")}");
            text.AppendLine($"комната {(t.Room==0?"нет":t.Room)}");
            text.AppendLine($"проходимость {(s.Map.Walkable(p)?"да":"нет")}");
            text.AppendLine($"тропа {t.Traffic:F0}");
            text.AppendLine($"температура {EnvironmentQueries.Air(s,p):F1} °C");
            foreach(var id in session.Spatial.Query(p,0).Where(id=>s.Entities.Get<PositionComponent>(id).Tile==p))
            {
                if(s.Entities.Try<FacilityComponent>(id) is not { } facility)continue;
                var definition=session.Definitions.Facilities[facility.Definition];
                Section(text,"объект");
                text.AppendLine(Safe(definition.Name));
                if(definition.Capabilities.Length>0)text.AppendLine("возможности: "+string.Join(", ",definition.Capabilities));
                if(s.Entities.Try<InventoryComponent>(id) is { } inventory)
                {
                    text.AppendLine($"хранилище: {session.Inventory.Mass(id):F1}/{inventory.MaxMass:F0} кг");
                    foreach(var group in inventory.Items.GroupBy(item=>s.Entities.Get<ItemComponent>(item).Definition))
                        text.AppendLine($"{Safe(session.Definitions.Items[group.Key].Name)} × {group.Count()}");
                }
            }
            return new("местность",text.ToString());
        }
        return new("выбери жителя", "Нажми на жителя или найди его по имени. Здесь появятся нужды, вещи, навыки, отношения и объяснение текущего решения.\n\nF2 покажет известные ему ресурсы и маршрут.");
    }

    private static InspectorSnapshot DescribePerson(SimulationSession session, int id)
    {
        var s=session.State;
        var e=s.Entities;
        var identity=e.Get<IdentityComponent>(id);
        var health=e.Get<HealthComponent>(id);
        var needs=e.Get<NeedsComponent>(id);
        var decision=e.Get<DecisionComponent>(id);
        var family=e.Get<FamilyComponent>(id);
        var memory=e.Get<MemoryComponent>(id);
        var text=new StringBuilder();
        text.AppendLine($"{s.Clock.Age(identity.BirthDate)} лет · {(identity.Sex=="female"?"женский пол":"мужской пол")}");
        text.AppendLine($"рождение: {identity.BirthDate:dd.MM.yyyy}");
        if (!health.Alive) return new(identity.FullName, text.AppendLine("\nумер: "+health.DeathReason).ToString());
        Section(text, "состояние");
        text.AppendLine($"здоровье  {health.Value:F0}/100\nголод  {needs.Hunger:P0}   жажда {needs.Thirst:P0}");
        text.AppendLine($"усталость  {needs.Fatigue:P0}\nодиночество  {needs.Loneliness:P0}");
        text.AppendLine($"температура тела  {e.Get<ThermalComponent>(id).Temperature:F1} °C");
        Section(text, "сейчас");
        var step=decision.Plan.FirstOrDefault();
        text.AppendLine(step is null?"оценивает обстановку":Safe(session.Actions[step.Action].Label));
        text.AppendLine("причина: "+Safe(decision.Motive));
        if (step is not null&&decision.RemainingMinutes>0)text.AppendLine($"осталось {decision.RemainingMinutes:F0} игровых минут");
        if (decision.Plan.Count>1)text.AppendLine("далее: "+string.Join(" → ", decision.Plan.Skip(1).Take(4).Select(x=>session.Actions[x.Action].Label)));
        Section(text, "оценка вариантов");
        foreach (var choice in decision.Alternatives.Take(5))text.AppendLine($"{choice.Score:F2} · {Safe(choice.Motive)}\n[color=#8fa38b]{Safe(choice.FirstAction)} · {choice.Cost:F0} мин[/color]");
        if (decision.LastFailure.Length>0)text.AppendLine("последняя неудача: "+Safe(decision.LastFailure));
        Section(text, "инвентарь");
        text.AppendLine($"{session.Inventory.Mass(id):F1} / {e.Get<InventoryComponent>(id).MaxMass:F0} кг");
        foreach (var group in e.Get<InventoryComponent>(id).Items.GroupBy(item=>e.Get<ItemComponent>(item).Definition))text.AppendLine($"{Safe(session.Definitions.Items[group.Key].Name)} × {group.Count()}");
        Section(text, "одежда");
        foreach (var itemId in e.Get<EquipmentComponent>(id).Items)
        {
            var item=e.Get<ItemComponent>(itemId);
            text.AppendLine($"{Safe(session.Definitions.Items[item.Definition].Name)}\n[color=#8fa38b]прочность {item.Durability:F0} · влажность {item.Wetness:P0}[/color]");
        }
        Section(text, "навыки");
        foreach (var skill in e.Get<SkillsComponent>(id).Experience.OrderByDescending(x=>x.Value).Take(6))text.AppendLine($"{Safe(skill.Key)}  {e.Get<SkillsComponent>(id).Level(skill.Key):F1}");
        Section(text, "знания и память");
        text.AppendLine(string.Join(", ", e.Get<KnowledgeComponent>(id).Facts));
        text.AppendLine($"наблюдений: {memory.Observations.Count}");
        foreach (var observation in memory.Observations.Where(x=>x.Kind is "plant" or "water" or "project").OrderByDescending(x=>x.SeenTick).Take(3))text.AppendLine($"{observation.Kind} · {observation.Position.X}, {observation.Position.Y} · уверенность {observation.Confidence:P0}");
        Section(text, "отношения");
        foreach (var person in e.Get<RelationshipComponent>(id).People.OrderByDescending(x=>x.Value.Familiarity).Take(4))
        {
            var name=e.Try<IdentityComponent>(person.Key)?.FullName??"неизвестно";
            text.AppendLine($"{Safe(name)}\n[color=#8fa38b]доверие {person.Value.Trust:P0} · симпатия {person.Value.Affection:P0}[/color]");
        }
        if (family.Partner!=0)text.AppendLine("партнер: "+Safe(e.Get<IdentityComponent>(family.Partner).FullName));
        if (family.Children.Count>0)text.AppendLine("детей: "+family.Children.Count);
        if (family.PregnancyDueTick is { } due)text.AppendLine($"до рождения: {Math.Max(0,(due-s.Clock.Tick)/1440)} дней");
        return new(identity.FullName, text.ToString());
    }

    private static string DescribeWorld(SimulationSession session)
    {
        var s=session.State;
        var text=new StringBuilder();
        text.AppendLine($"сид {s.Seed}\n{s.Map.Width} × {s.Map.Height} клеток\n{s.Entities.Count} сущностей\nтик {s.Clock.Tick}");
        Section(text,"поселения");
        var settlements=SettlementAnalyzer.DescribeAll(session);
        if (settlements.Count==0)text.AppendLine("пока не сформированы");
        foreach(var settlement in settlements)
            text.AppendLine($"{Safe(settlement.Name)} · {settlement.Members} жителей · {settlement.Homes} домов");
        Section(text,"природа");
        text.AppendLine($"растений {s.Entities.Store<PlantComponent>().Count}\nпредметов {s.Entities.Store<ItemComponent>().Count}\nрабочих объектов {s.Entities.Store<FacilityComponent>().Count}\nпроектов {s.Entities.Store<ConstructionComponent>().Count}");
        Section(text,"погода");
        text.AppendLine($"световой день {s.Weather.DaylightHours:F1} ч\nветер {s.Weather.Wind:P0}\nосадки {s.Weather.Rain:P0}");
        Section(text,"история");
        foreach(var ev in s.Journal.AsEnumerable().Reverse().Take(16))text.AppendLine($"[color=#8fa38b]{s.Clock.Epoch.AddMinutes(ev.Tick):dd.MM HH:mm}[/color]\n{Safe(ev.Text)}\n");
        return text.ToString();
    }

    private static string DescribeSystems(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var decisions=e.Store<DecisionComponent>().All.Select(x=>x.Value).ToArray();
        var activePlans=decisions.Count(x=>x.Plan.Count>0);
        var averagePlan=decisions.Length==0?0:decisions.Average(x=>x.Plan.Count);
        var failures=decisions.Sum(x=>x.Failures);
        var text=new StringBuilder();
        text.AppendLine($"последний тик {session.LastTickMilliseconds:F2} мс");
        text.AppendLine($"активных планов {activePlans} · средняя длина {averagePlan:F1}");
        text.AppendLine($"ошибок планов {failures} · резервов {session.Reservations.Entries.Count}");
        text.AppendLine($"жителей {s.Population} · предметов {e.Store<ItemComponent>().Count} · растений {e.Store<PlantComponent>().Count}");
        text.AppendLine($"строек {e.Store<ConstructionComponent>().All.Count(x=>!x.Value.Finished)} · facilities {e.Store<FacilityComponent>().Count} · огней {e.Store<FireComponent>().Count}");
        foreach(var p in session.Profiles.Values.OrderByDescending(x=>x.AverageMilliseconds))
        {
            Section(text,p.Name);
            text.AppendLine($"последний {p.LastMilliseconds:F3} мс\nсреднее {p.AverageMilliseconds:F3} мс · максимум {p.MaxMilliseconds:F3} мс\nобработано {p.Entities} · вызовов {p.Calls}");
        }
        return text.ToString();
    }

    private static string Safe(string value)=>value.Replace("[","[lb]");
    private static void Section(StringBuilder text,string title)=>text.AppendLine("\n[color=#b8c7a4]"+title.ToUpperInvariant()+"[/color]");
    private static string BiomeName(Biome biome)=>biome switch
    {
        Biome.Forest=>"лес",Biome.Meadow=>"луг",Biome.Marsh=>"болото",Biome.Beach=>"берег",Biome.Mountain=>"горы",Biome.Alpine=>"высокогорье",_=>"море"
    };
}
