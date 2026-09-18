namespace LivingWorld.Simulation;
public sealed class WarmUpAction : SimAction
{
    public override string Id=>"warm_up";
    public override string Label=>"греется";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var o in c.OfKind("fire").Where(o=>o.Quantity>40).Take(3))
        {
            var op=Option(o.Position,o.Entity,duration:45);
            op.Effects=[new("warm",1,true)];
            yield return op;
        }
        var home=c.Family.HomeProject==0?null:c.Known.FirstOrDefault(o=>o.Kind=="project"&&o.Entity==c.Family.HomeProject);
        foreach(var o in c.OfKind("shelter").Take(4))
        {
            var op=Option(o.Position,duration:45,range:0);
            op.Effects=[new("warm",1,true),new("sheltered",1,true)];
            if(home is not null&&o.Position.Distance(home.Position)<=4)op.Cost=30;
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="источник тепла изменился";
        var p=s.State.Entities.Get<PositionComponent>(actor).Tile;
        if(EnvironmentQueries.FireHeat(s,p)>1)return true;
        if(!EnvironmentQueries.Sheltered(s.State,p))return false;
        var local=EnvironmentQueries.Local(s.State,p);
        var outdoor=EnvironmentQueries.Air(s.State,p);
        return local>=10||local>outdoor+.5f;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)=>CanExecute(s,actor,step,out _);
}
