namespace LivingWorld.Simulation;
[Component("Family")]
public sealed class FamilyComponent
{
    public int Partner { get; set; }
    public int Mother { get; set; }
    public int Father { get; set; }
    public List<int> Children { get; set; } = [];
    public long? PregnancyDueTick { get; set; }
    public int PregnancyFather { get; set; }
    public long LastBirthTick { get; set; } = -10000000;
    public int HomeProject { get; set; }
}
