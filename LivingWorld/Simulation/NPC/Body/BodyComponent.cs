namespace LivingWorld.Simulation;
[Component("Body")]
public sealed class BodyComponent
{
    public float Strength { get; set; } = .7f;
    public float Mobility { get; set; } = 1;
    public float Mass { get; set; } = 65;
}
