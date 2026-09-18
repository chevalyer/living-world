namespace LivingWorld.Simulation;
public sealed class CraftAction : SimAction
{
    public override string Id=>"craft";
    public override string Label=>"изготавливает предмет";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;

    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var recipe in c.Definitions.Recipes.Values)
        {
            if(!c.Knowledge.Facts.Contains(recipe.Knowledge))continue;
            var stations=string.IsNullOrWhiteSpace(recipe.Capability)
                ? new Observation?[]{null}
                : c.Facilities(recipe.Capability).Cast<Observation?>().ToArray();
            foreach(var station in stations)
            {
                var op=Option(station?.Position??c.Position,station?.Entity??0,recipe.Id,
                    recipe.Minutes/(1+c.Skills.Level(recipe.Skill)*.1f),range:station is null?1:1,local:station is null);
                op.Requires=recipe.Inputs.Select(x=>new FactRequirement(Item(x.Key),x.Value)).ToList();
                op.Effects=recipe.Inputs.Select(x=>new FactEffect(Item(x.Key),-x.Value)).ToList();
                op.Effects.Add(new(Item(recipe.Output),recipe.Count));
                if(recipe.Tool.Length>0)
                {
                    op.Requires.Add(new("tool:"+recipe.Tool,1));
                    op.Effects.Add(new("tool:"+recipe.Tool,-1));
                }
                if(recipe.Operation=="heat"&&station is null)op.Requires.Add(new("heat",1));
                var outputDefinition=c.Definitions.Items[recipe.Output];
                if(outputDefinition.Calories>0)
                    op.Effects.Add(new("food.reserve",(int)MathF.Round(outputDefinition.Calories*recipe.Count),false));
                foreach(var capability in outputDefinition.Tools)
                    op.Effects.Add(new("tool:"+capability.Key,Math.Max(1,(int)MathF.Floor(outputDefinition.Durability/2)),true));
                op.Skill=recipe.Skill;
                yield return op;
            }
        }
    }

    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="рабочее место или материалы изменились";
        if(!base.CanExecute(s,actor,step,out _ )||!s.Definitions.Recipes.TryGetValue(step.Argument,out var recipe))return false;
        var e=s.State.Entities;
        if(!e.Get<KnowledgeComponent>(actor).Facts.Contains(recipe.Knowledge)||
           recipe.Inputs.Any(x=>s.Inventory.Count(actor,x.Key)<x.Value))return false;
        if(recipe.Tool.Length>0&&s.Inventory.Tool(actor,recipe.Tool)<=0)return false;
        if(!string.IsNullOrWhiteSpace(recipe.Capability))
        {
            if(step.Target==0||!FacilityService.CanUse(s,actor,step.Target)||
               !FacilityService.HasCapability(s,step.Target,recipe.Capability)||
               e.Get<PositionComponent>(actor).Tile.Distance(e.Get<PositionComponent>(step.Target).Tile)>1)return false;
        }
        if(recipe.Operation=="heat")
        {
            var stationHeat=step.Target!=0&&FacilityService.HasCapability(s,step.Target,"heat");
            if(!stationHeat&&EnvironmentQueries.FireHeat(s,e.Get<PositionComponent>(actor).Tile)<3)return false;
        }
        return true;
    }

    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!CanExecute(s,actor,step,out _))return false;
        var recipe=s.Definitions.Recipes[step.Argument];
        var e=s.State.Entities;
        var inputMass=recipe.Inputs.Sum(x=>s.Definitions.Items[x.Key].Mass*x.Value);
        var inputVolume=recipe.Inputs.Sum(x=>s.Definitions.Items[x.Key].Volume*x.Value);
        var output=s.Definitions.Items[recipe.Output];
        var inv=e.Get<InventoryComponent>(actor);
        if(s.Inventory.Mass(actor)-inputMass+output.Mass*recipe.Count>inv.MaxMass||
           s.Inventory.Volume(actor)-inputVolume+output.Volume*recipe.Count>inv.MaxVolume)return false;
        foreach(var input in recipe.Inputs)
            if(!s.Inventory.Consume(actor,input.Key,input.Value))throw new InvalidOperationException("Craft preflight failed.");
        for(var i=0;i<recipe.Count;i++)
        {
            var id=s.Inventory.Spawn(recipe.Output,e.Get<PositionComponent>(actor).Tile,actor);
            e.Get<ItemComponent>(id).Quality=Math.Clamp(.6f+e.Get<SkillsComponent>(actor).Level(recipe.Skill)*.07f,.5f,1.4f);
            s.Inventory.PickUp(actor,id);
        }
        if(recipe.Tool.Length>0)s.Inventory.WearTool(actor,recipe.Tool,1);
        s.Events.Publish(new SkillUsedEvent(actor,recipe.Skill,recipe.Minutes*.12f));
        return true;
    }
}
