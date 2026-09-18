namespace LivingWorld.Simulation;
public sealed class SowAction : SimAction
{
    public override string Id=>"sow";
    public override string Label=>"сеет";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(!c.Knowledge.Facts.Contains("farming")||c.SowSite is not { } site)yield break;
        foreach(var plant in c.Definitions.Plants.Values.Where(x=>x.Kind=="crop"&&c.OutdoorAir>=x.MinTemperature))
        {
            var reservation=-(site.Y*c.MapWidth+site.X+1);
            var op=Option(site,reservation,plant.Id,20);
            op.Requires=[new(Item(plant.Product),1)];
            op.Effects=[new(Item(plant.Product),-1),new("sown",1,true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor)||!s.State.Map.Contains(step.Position))return false;
        var actorPosition=s.State.Entities.Get<PositionComponent>(actor).Tile;
        if(actorPosition.Distance(step.Position)>1)return false;
        var p=step.Position;
        var d=s.Definitions.Plants[step.Argument];
        var tile=s.State.Map[p];
        if(!s.State.Map.Walkable(p)||tile.Roof>0||tile.Floor>0||tile.Water!=WaterKind.None||EnvironmentQueries.Air(s.State,p)<d.MinTemperature)return false;
        if(s.Spatial.Query(p,0).Any(id=>s.State.Entities.Has<PlantComponent>(id)&&s.State.Entities.Get<PositionComponent>(id).Tile==p))return false;
        if(!s.Inventory.Consume(actor,d.Product,1))return false;
        var id=s.State.Entities.Create();
        s.State.Entities.Set(id,new PositionComponent { Tile=p });
        s.State.Entities.Set(id,new PlantComponent { Definition=d.Id,Growth=.01f,Cultivator=actor });
        s.State.Entities.Set(id,new OwnershipComponent { Owner=actor });
        s.Spatial.Add(id,p);
        s.Events.Publish(new SkillUsedEvent(actor,"farming",4));
        return true;
    }
}
