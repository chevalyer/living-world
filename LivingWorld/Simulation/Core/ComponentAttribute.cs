namespace LivingWorld.Simulation;
[AttributeUsage(AttributeTargets.Class)]
public sealed class ComponentAttribute(string id) : Attribute
{
    public string Id
    { get; } = id;
}
