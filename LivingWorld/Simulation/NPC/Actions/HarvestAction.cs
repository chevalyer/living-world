namespace LivingWorld.Simulation;
public sealed class HarvestAction : SimAction
{
    public override string Id=>"harvest";
    public override string Label=>"собирает растения";
    public override bool Exclusive=>true;
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("plant"))
        {
            if (o.Owner!=0&&o.Owner!=c.Actor)continue;
            var plant=c.Definitions.Plants[o.Definition];
            if (plant.Kind=="tree"||o.Quantity<1)continue;
            if (c.Definitions.Items[plant.Product].Calories>0&&!c.Knowledge.EdiblePlants.Contains(plant.Id))continue;
            var units=Math.Min(3, o.Quantity);
            var op=Option(o.Position, o.Entity, plant.Product, 3);
            op.Requires=[new(Source(o.Entity), units)];
            op.Effects=[new(Source(o.Entity), -units), new("ground:"+o.Entity+":"+plant.Product, units)];
            if (plant.Kind=="crop"&&plant.Seed.Length>0&&plant.SeedYield>0&&
                (!plant.HarvestRemovesPlant||o.Quantity<=units))
                op.Effects.Add(new("ground:"+o.Entity+":"+plant.Seed, plant.SeedYield));
            op.Skill="foraging";
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var owner=s.State.Entities.Try<OwnershipComponent>(step.Target)?.Owner??0;
        if (owner!=0&&owner!=actor)return false;
        var plant=s.State.Entities.Try<PlantComponent>(step.Target);
        if (plant is null||plant.Yield<1)return false;
        var d=s.Definitions.Plants[plant.Definition];
        if (d.Kind=="tree")return false;
        if (s.Definitions.Items[d.Product].Calories>0&&!s.State.Entities.Get<KnowledgeComponent>(actor).EdiblePlants.Contains(d.Id))return false;
        var units=Math.Min(3, (int)plant.Yield);
        var depleted=plant.Yield-units<1;
        plant.Yield-=units;
        for (var i=0; i<units; i++)s.Inventory.Spawn(d.Product, step.Position);
        if (d.Kind=="crop"&&d.Seed.Length>0&&d.SeedYield>0&&(!d.HarvestRemovesPlant||depleted))
            for (var i=0; i<d.SeedYield; i++)s.Inventory.Spawn(d.Seed, step.Position);
        if (depleted&&d.HarvestRemovesPlant)
        {
            s.State.Entities.Remove(step.Target);
            s.Spatial.Remove(step.Target);
        }
        s.Events.Publish(new SkillUsedEvent(actor, "foraging", units));
        return true;
    }
}
