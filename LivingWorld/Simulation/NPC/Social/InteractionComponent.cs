namespace LivingWorld.Simulation;
[Component("Interaction")]
public sealed class InteractionComponent
{
    public int Partner { get; set; }
    public int Initiator { get; set; }
    public long Until { get; set; }
}
