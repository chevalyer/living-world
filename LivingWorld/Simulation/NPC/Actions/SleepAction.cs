namespace LivingWorld.Simulation;
public sealed class SleepAction : SimAction
{
    public override string Id=>"sleep";
    public override string Label=>"спит";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        var home=c.Family.HomeProject==0?null:c.Known.FirstOrDefault(o=>o.Kind=="project"&&o.Entity==c.Family.HomeProject);
        var local=Option(c.Position,duration:120,range:0,local:true);
        local.Effects=[new("rested",1,true)];
        local.Risk=c.Sheltered?0:12;
        if(c.Sheltered&&home is not null&&c.Position.Distance(home.Position)<=4)local.Cost=90;
        yield return local;
        foreach(var o in c.OfKind("shelter").Take(4))
        {
            var op=Option(o.Position,duration:100,range:0);
            op.Effects=[new("rested",1,true)];
            var ownHome=home is not null&&o.Position.Distance(home.Position)<=4;
            op.Cost=ownHome?70:105;
            op.Risk=ownHome?0:1.5f;
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        var n=s.State.Entities.Get<NeedsComponent>(actor);
        n.Fatigue=Math.Max(0,n.Fatigue-.72f);
        return true;
    }
}
