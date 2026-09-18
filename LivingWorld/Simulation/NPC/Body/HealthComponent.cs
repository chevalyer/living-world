namespace LivingWorld.Simulation;
[Component("Health")]
public sealed class HealthComponent
{
    public float Value { get; set; } = 100;
    public bool Alive { get; set; } = true;
    public string DeathReason { get; set; } = "";
}
