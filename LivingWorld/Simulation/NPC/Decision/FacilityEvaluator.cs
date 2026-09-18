namespace LivingWorld.Simulation;
public sealed class FacilityEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        if(!c.CanWork||c.Family.HomeProject==0)yield break;
        var fullness=Math.Max(
            c.Inventory.Sum(x=>c.Definitions.Items[x.Item.Definition].Mass)/Math.Max(1,c.MaxMass),
            c.Inventory.Sum(x=>c.Definitions.Items[x.Item.Definition].Volume)/Math.Max(1,c.MaxVolume));
        var nearestWater=c.OfKind("water").Select(o=>o.Position.Distance(c.Position)).DefaultIfEmpty(99).Min();

        foreach(var definition in c.Definitions.Facilities.Values)
        {
            if(c.Known.Any(o=>o.Kind=="facility"&&o.Definition==definition.Id&&
                (o.Project==0||o.Project==c.Family.HomeProject)))continue;
            if(!c.Knowledge.Facts.Contains(definition.Knowledge))continue;
            var urgency=.02f;
            foreach(var capability in definition.Capabilities)
            {
                if(capability=="sleep")urgency=Math.Max(urgency,.08f+c.Needs.Fatigue*.18f);
                else if(capability=="storage")urgency=Math.Max(urgency,.06f+Math.Max(0,fullness-.35f)*.5f);
                else if(capability=="water"&&nearestWater>8)urgency=Math.Max(urgency,.1f+c.Needs.Thirst*.2f);
                var usefulRecipes=c.Definitions.Recipes.Values.Where(r=>r.Capability==capability&&c.Knowledge.Facts.Contains(r.Knowledge)).ToArray();
                if(usefulRecipes.Length>0)
                {
                    urgency=Math.Max(urgency,.055f+Math.Min(.12f,usefulRecipes.Length*.025f));
                    if(usefulRecipes.Any(r=>r.Inputs.Keys.Any(item=>c.Inventory.Any(x=>x.Item.Definition==item))))
                        urgency+=.05f;
                }
            }
            if(urgency>.025f)yield return new("facility:"+definition.Id,"не хватает возможности: "+definition.Name,urgency);
        }
    }
}
