namespace LivingWorld.Simulation;
public sealed record ActionStep
{
    public string Action { get; set; } = "";
    public bool Local { get; set; }
    public int Target { get; set; }
    public GridPoint Position { get; set; }
    public string Argument { get; set; } = "";
    public float Duration { get; set; } = 1;
    public int Range { get; set; } = 1;
}
public sealed record DecisionScore(string Motive, string FirstAction, float Score, float Cost, string Reason);
