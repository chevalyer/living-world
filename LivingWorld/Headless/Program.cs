using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
try
{
    string? Arg(string name)
    {
        var index=Array.IndexOf(args, name);
        return index>=0&&index+1<args.Length?args[index+1]:null;
    }
    int IntArg(string name, int fallback)=>int.TryParse(Arg(name), out var value)?value:fallback;
    if (args.Contains("--help"))
    {
        Console.WriteLine("--seed 1847 --size 128 --npcs 14 --ticks 1440 --save path --load path --map map.svg --json metrics.json");
        return 0;
    }
    var definitions=DefinitionLoader.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
    var saves=new SaveService();
    var timer=Stopwatch.StartNew();
    var session=Arg("--load") is { } load?saves.Load(load, definitions):new SimulationSession(new WorldGenerator().Generate(definitions, IntArg("--seed", 1847), IntArg("--size", 128), IntArg("--npcs", 14)), definitions);
    var generated=timer.Elapsed.TotalMilliseconds;
    timer.Restart();
    var ticks=IntArg("--ticks", 1440);
    session.Step(ticks);
    timer.Stop();
    if (Arg("--save") is { } save)saves.Save(session, save);
    if (Arg("--map") is { } mapPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(mapPath))!);
        File.WriteAllText(mapPath, MapSvg.Render(session));
    }
    var summary=new
    {
        seed=session.State.Seed, ticks, simulation_tick=session.State.Clock.Tick, date=session.State.Clock.Now, population=session.State.Population, entities=session.State.Entities.Count, rooms=session.State.Rooms.Count, settlement=SettlementAnalyzer.Describe(session), generation_ms=generated, simulation_ms=timer.Elapsed.TotalMilliseconds, hash=saves.Hash(session),
        facilities=session.State.Entities.Store<FacilityComponent>().Count,
        facility_counts=session.State.Entities.Store<FacilityComponent>().All
            .GroupBy(x=>x.Value.Definition).OrderBy(x=>x.Key,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Count()),
        action_failures=session.State.Entities.Store<DecisionComponent>().All.Sum(x=>x.Value.Failures),
        failure_reasons=session.FailureReasons.OrderByDescending(x=>x.Value).ToDictionary(x=>x.Key,x=>x.Value),
        failure_actions=session.FailureActions.OrderByDescending(x=>x.Value).ToDictionary(x=>x.Key,x=>x.Value),
        deaths=session.State.Entities.Store<HealthComponent>().All.Where(x=>!x.Value.Alive).GroupBy(x=>x.Value.DeathReason).ToDictionary(g=>g.Key,g=>g.Count()),
        profiles=session.Profiles.Values.Select(p=>new
        {
            p.Name, p.AverageMilliseconds, p.MaxMilliseconds, p.Calls, p.Entities
        }), journal=session.State.Journal.TakeLast(10)
    };
    var json=JsonSerializer.Serialize(summary, new JsonSerializerOptions
    {
        WriteIndented=true
    });
    Console.WriteLine(json);
    if (Arg("--json") is { } jsonPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
        File.WriteAllText(jsonPath, json);
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
