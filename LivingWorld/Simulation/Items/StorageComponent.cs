namespace LivingWorld.Simulation;
[Component("Storage")]
public sealed class StorageComponent
{
    public string Name { get; set; } = "общий склад";
    public int Project { get; set; }
}
