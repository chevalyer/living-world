namespace LivingWorld.Simulation;
[Component("Skills")]
public sealed class SkillsComponent
{
    public Dictionary<string, float> Experience { get; set; } = [];
    public float Level(string skill) => MathF.Sqrt(Experience.GetValueOrDefault(skill) / 25f);
}
