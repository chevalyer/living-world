namespace LivingWorld.Simulation;
[Component("Needs")]
public sealed class NeedsComponent
{
    public float Hunger { get; set; } = .2f;
    public float Thirst { get; set; } = .2f;
    public float Fatigue { get; set; } = .1f;
    public float Loneliness { get; set; } = .15f;
}
