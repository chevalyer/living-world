namespace LivingWorld.Simulation;
[Component("Health")]
public sealed class HealthComponent
{
    public float Value { get; set; } = 100;
    public bool Alive { get; set; } = true;
    public string DeathReason { get; set; } = "";
    public string LastDamageReason { get; set; } = "";
    public long LastDamageTick { get; set; } = -1;

    public void Damage(float amount, string reason, long tick)
    {
        if (!Alive||Value<=0||amount<=0)return;
        Value-=amount;
        LastDamageReason=reason;
        LastDamageTick=tick;
    }
}
