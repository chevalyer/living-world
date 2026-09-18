namespace LivingWorld.Simulation;
public sealed record Observation
{
    public int Entity { get; set; }
    public GridPoint? WorkPosition { get; set; }
    public string Kind { get; set; } = "";
    public GridPoint Position { get; set; }
    public string Definition { get; set; } = "";
    public string Product { get; set; } = "";
    public int Quantity { get; set; }
    public long SeenTick { get; set; }
    public float Confidence { get; set; } = 1;
    public long UnreachableUntil { get; set; }
    public int Owner { get; set; }
    public float Need { get; set; }
    public int Age { get; set; }
    public string Sex { get; set; } = "";
    public int Partner { get; set; }
    public bool Pregnant { get; set; }
    public float TrustBack { get; set; }
    public float AffectionBack { get; set; }
    public Dictionary<string, float> Skills { get; set; } = [];
    public Dictionary<string, int> Items { get; set; } = [];
}
public sealed record MemoryEvent(long Tick, string Text);
