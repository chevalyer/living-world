namespace LivingWorld.Simulation;
public sealed class CoolDownAction : SimAction
{
    public override string Id=>"cool_down";
    public override string Label=>"охлаждается";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(c.Temperature<=37.6f||c.Air>=c.Temperature)yield break;
        var op=Option(c.Position,duration:30,range:0,local:true);
        op.Effects=[new("cooled",1,true)];
        yield return op;
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="место больше не охлаждает";
        var e=s.State.Entities;
        var p=e.Get<PositionComponent>(actor).Tile;
        var ambient=EnvironmentQueries.Local(s.State,p)+EnvironmentQueries.FireHeat(s,p);
        return ambient<e.Get<ThermalComponent>(actor).Temperature;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!CanExecute(s,actor,step,out _))return false;
        var e=s.State.Entities;
        var thermal=e.Get<ThermalComponent>(actor);
        var p=e.Get<PositionComponent>(actor).Tile;
        var ambient=EnvironmentQueries.Local(s.State,p)+EnvironmentQueries.FireHeat(s,p);
        thermal.Temperature=Math.Max(37.2f,thermal.Temperature-Math.Clamp((thermal.Temperature-ambient)*.08f,.15f,1.2f));
        return true;
    }
}
