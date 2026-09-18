namespace LivingWorld.Simulation;
public sealed record DesiredState(string Fact, string Reason, float Urgency, int Minimum=1);
public interface ISituationEvaluator
{
    IEnumerable<DesiredState> Evaluate(PlanningContext context);
}
