namespace LivingWorld.Simulation;
[Component("Decision")]
public sealed class DecisionComponent
{
    public List<ActionStep> Plan { get; set; } = [];
    public string Motive { get; set; } = "наблюдение";
    public string DesiredFact { get; set; } = "";
    public float ChosenScore { get; set; }
    public float RemainingMinutes { get; set; } = -1;
    public List<DecisionScore> Alternatives { get; set; } = [];
    public long NextDecision { get; set; }
    public long LastSocialTick { get; set; } = -1000;
    public long LastFailureTick { get; set; } = -1000;
    public string LastFailure { get; set; } = "";
    public int Failures { get; set; }
}
