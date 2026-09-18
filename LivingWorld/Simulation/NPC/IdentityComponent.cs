namespace LivingWorld.Simulation;
[Component("Identity")]
public sealed class IdentityComponent
{
    public string FirstName { get; set; } = "";
    public string Surname { get; set; } = "";
    public string Sex { get; set; } = "male";
    public DateTime BirthDate { get; set; }
    public int LifespanYears { get; set; } = 82;
    public int Appearance { get; set; }
    public string FullName => FirstName + " " + Surname;
}
