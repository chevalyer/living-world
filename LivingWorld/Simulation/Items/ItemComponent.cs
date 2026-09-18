namespace LivingWorld.Simulation;
[Component("Item")]
public sealed class ItemComponent
{
    public string Definition { get; set; } = "";
    public int Holder { get; set; }
    public float Quality { get; set; } = 1;
    public float Durability { get; set; } = 100;
    public float Wetness { get; set; }
    public float Freshness { get; set; } = 1;
    public long CreatedTick { get; set; }
    public float Sharpness { get; set; } = 1;
}
