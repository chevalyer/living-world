namespace LivingWorld.Simulation;
[Component("Inventory")]
public sealed class InventoryComponent
{
    public List<int> Items { get; set; } = [];
    public float MaxMass { get; set; } = 28;
    public float MaxVolume { get; set; } = 40;
}
