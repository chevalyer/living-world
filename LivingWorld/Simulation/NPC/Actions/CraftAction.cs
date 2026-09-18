namespace LivingWorld.Simulation;
public sealed class CraftAction : SimAction
{
    public override string Id=>"craft";
    public override string Label=>"изготавливает предмет";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var recipe in c.Definitions.Recipes.Values)
        {
            if (!c.Knowledge.Facts.Contains(recipe.Knowledge))continue;
            var op=Option(c.Position, argument:recipe.Id, duration:recipe.Minutes/(1+c.Skills.Level(recipe.Skill)*.1f), local:true);
            op.Requires=recipe.Inputs.Select(x=>new FactRequirement(Item(x.Key), x.Value)).ToList();
            op.Effects=recipe.Inputs.Select(x=>new FactEffect(Item(x.Key), -x.Value)).ToList();
            op.Effects.Add(new(Item(recipe.Output), recipe.Count));
            if (recipe.Tool.Length>0)op.Requires.Add(new("tool:"+recipe.Tool, 1));
            foreach (var capability in c.Definitions.Items[recipe.Output].Tools)op.Effects.Add(new("tool:"+capability.Key, 1, true));
            if (recipe.Operation=="heat"&&!c.OfKind("fire").Any(o=>o.Quantity>0&&o.Position.Distance(c.Position)<=2))continue;
            op.Skill=recipe.Skill;
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var recipe=s.Definitions.Recipes[step.Argument];
        var e=s.State.Entities;
        if (!e.Get<KnowledgeComponent>(actor).Facts.Contains(recipe.Knowledge)||recipe.Inputs.Any(x=>s.Inventory.Count(actor, x.Key)<x.Value))return false;
        if (recipe.Tool.Length>0&&s.Inventory.Tool(actor, recipe.Tool)<=0)return false;
        if (recipe.Operation=="heat"&&EnvironmentQueries.FireHeat(s, e.Get<PositionComponent>(actor).Tile)<3)return false;
        var inputMass=recipe.Inputs.Sum(x=>s.Definitions.Items[x.Key].Mass*x.Value);
        var inputVolume=recipe.Inputs.Sum(x=>s.Definitions.Items[x.Key].Volume*x.Value);
        var output=s.Definitions.Items[recipe.Output];
        var inv=e.Get<InventoryComponent>(actor);
        if (s.Inventory.Mass(actor)-inputMass+output.Mass*recipe.Count>inv.MaxMass||s.Inventory.Volume(actor)-inputVolume+output.Volume*recipe.Count>inv.MaxVolume)return false;
        foreach (var input in recipe.Inputs)s.Inventory.Consume(actor, input.Key, input.Value);
        for (var i=0; i<recipe.Count; i++)
        {
            var id=s.Inventory.Spawn(recipe.Output, e.Get<PositionComponent>(actor).Tile, actor);
            e.Get<ItemComponent>(id).Quality=Math.Clamp(.6f+e.Get<SkillsComponent>(actor).Level(recipe.Skill)*.07f, .5f, 1.4f);
            s.Inventory.PickUp(actor, id);
        }
        s.Events.Publish(new SkillUsedEvent(actor, recipe.Skill, recipe.Minutes*.12f));
        return true;
    }
}
