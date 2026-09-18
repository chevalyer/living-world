namespace LivingWorld.Simulation;
public sealed record DesiredState(string Fact, string Reason, float Urgency);
public interface ISituationEvaluator
{
    IEnumerable<DesiredState> Evaluate(PlanningContext context);
}
