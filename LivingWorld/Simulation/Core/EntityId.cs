namespace LivingWorld.Simulation;
public readonly record struct EntityId(int Value)
{
    public static readonly EntityId None = new(0);
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
