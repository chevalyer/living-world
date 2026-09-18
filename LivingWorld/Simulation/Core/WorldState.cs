namespace LivingWorld.Simulation;
public sealed class WorldState
{
    public int Seed { get; set; }
    public int DecisionCursor { get; set; }
    public int GenerationVersion { get; set; } = 1;
    public SimulationClock Clock { get; set; } = new();
    public WorldMap Map { get; set; } = new();
    public EntityRegistry Entities { get; set; } = new();
    public RandomService Random { get; set; } = new(0);
    public WeatherState Weather { get; set; } = new();
    public List<Room> Rooms { get; set; } = [];
    public List<MemoryEvent> Journal { get; set; } = [];
    public GridPoint Start { get; set; }
    public int Population => Entities.Store<HealthComponent>().All.Count(x => x.Value.Alive);
    public void Log(string message)
    {
        Journal.Add(new(Clock.Tick, message));
        if (Journal.Count > 120) Journal.RemoveRange(0, Journal.Count - 120);
    }
}
