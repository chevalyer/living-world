namespace LivingWorld.Infrastructure;
using System.Text.Json;
public sealed class WorldSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public int GenerationVersion { get; set; } = 1;
    public Dictionary<string,string> DefinitionManifest { get; set; } = new(StringComparer.Ordinal);
    public string DefinitionsFingerprint { get; set; } = "";
    public int Seed { get; set; }
    public int DecisionCursor { get; set; }
    public int NextEntityId { get; set; }
    public int[] EntityIds { get; set; } = [];
    public SimulationClock Clock { get; set; } = new();
    public WorldMap Map { get; set; } = new();
    public WeatherState Weather { get; set; } = new();
    public GridPoint Start { get; set; }
    public Dictionary<string, ulong> RandomStates { get; set; } = [];
    public Dictionary<int, Reservation> Reservations { get; set; } = [];
    public Dictionary<string, Dictionary<int, JsonElement>> Components { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<MemoryEvent> Journal { get; set; } = [];
    public bool RoomsDirty { get; set; }
}
