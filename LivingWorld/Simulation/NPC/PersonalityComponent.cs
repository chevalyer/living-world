namespace LivingWorld.Simulation;
[Component("Personality")]
public sealed class PersonalityComponent
{
    public float Sociability { get; set; } = .5f;
    public float Generosity { get; set; } = .5f;
    public float Caution { get; set; } = .5f;
    public float Aggression { get; set; } = .2f;
    public float Curiosity { get; set; } = .5f;
    public float FamilyDesire { get; set; } = .5f;
}
