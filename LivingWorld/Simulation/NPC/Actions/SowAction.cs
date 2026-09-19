namespace LivingWorld.Simulation;
public sealed class SowAction : SimAction
{
    public override string Id=>"sow";
    public override string Label=>"сеет";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("farming")||c.Air<7)yield break;
        foreach (var plant in c.Definitions.Plants.Values.Where(x=>x.Kind=="crop"&&x.Seed.Length>0))
        {
            var op=Option(c.Position, argument:plant.Id, duration:20, local:true);
            op.Requires=[new(Item(plant.Seed), 1)];
            op.Effects=[new(Item(plant.Seed), -1), new("sown", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var p=s.State.Entities.Get<PositionComponent>(actor).Tile;
        var d=s.Definitions.Plants[step.Argument];
        if (s.State.Map[p].Roof>0||s.State.Map[p].Floor>0||s.State.Map[p].Water!=WaterKind.None||EnvironmentQueries.Air(s.State, p)<d.MinTemperature)return false;
        if (s.Spatial.Query(p, 0).Any(id=>s.State.Entities.Has<PlantComponent>(id)&&s.State.Entities.Get<PositionComponent>(id).Tile==p))return false;
        if (d.Kind!="crop"||d.Seed.Length==0||!s.Definitions.Items.ContainsKey(d.Seed)||
            !s.Inventory.Consume(actor, d.Seed, 1))return false;
        var id=s.State.Entities.Create();
        s.State.Entities.Set(id, new PositionComponent
        {
            Tile=p
        });
        s.State.Entities.Set(id, new PlantComponent
        {
            Definition=d.Id, Growth=.01f, Cultivator=actor
        });
        s.State.Entities.Set(id, new OwnershipComponent
        {
            Owner=actor
        });
        s.Spatial.Add(id, p);
        s.Events.Publish(new SkillUsedEvent(actor, "farming", 4));
        return true;
    }
}
