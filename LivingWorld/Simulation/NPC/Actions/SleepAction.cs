namespace LivingWorld.Simulation;
public sealed class SleepAction : SimAction
{
    public override string Id=>"sleep";
    public override string Label=>"спит";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        var local=Option(c.Position, duration:120, range:0, local:true);
        local.Effects=[new("rested", 1, true)];
        local.Risk=c.Sheltered?0:12;
        yield return local;
        foreach (var o in c.OfKind("shelter").Take(3))
        {
            var op=Option(o.Position, duration:100, range:0);
            op.Effects=[new("rested", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var n=s.State.Entities.Get<NeedsComponent>(actor);
        n.Fatigue=Math.Max(0, n.Fatigue-.72f);
        return true;
    }
}
