namespace LivingWorld.Simulation;
public sealed class WarmUpAction : SimAction
{
    public override string Id=>"warm_up";
    public override string Label=>"греется";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("fire").Where(o=>o.Quantity>40).Take(3))
        {
            var op=Option(o.Position, o.Entity, duration:45);
            op.Effects=[new("warm", 1, true)];
            yield return op;
        }
        foreach (var o in c.OfKind("shelter").Take(3))
        {
            var op=Option(o.Position, duration:45, range:0);
            op.Effects=[new("warm", 1, true), new("sheltered", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var p=s.State.Entities.Get<PositionComponent>(actor).Tile;
        return EnvironmentQueries.Sheltered(s.State, p)||EnvironmentQueries.FireHeat(s, p)>1;
    }
}
