namespace LivingWorld.Simulation;
public sealed class ResourceEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        var personalSpoiled=c.Inventory.Any(x=>x.Item.Freshness<.1f&&c.Definitions.Items[x.Item.Definition].Calories>0);
        var storageSpoiled=c.Storages().Any(o=>o.Spoiled>0);
        if(personalSpoiled||storageSpoiled)yield return new("cleaned","освободить место от испорченной еды",.8f);
        if(!c.CanWork)yield break;

        if(c.Storages().Any()&&c.Inventory.GroupBy(x=>x.Item.Definition).Any(g=>g.Count()>3))
            yield return new("distributed","семейный запас ресурсов",c.Personality.Generosity*.5f);

        var calories=(int)c.Inventory.Sum(x=>c.Definitions.Items[x.Item.Definition].Calories*Math.Max(0,x.Item.Freshness));
        if(calories<2400)yield return new("food.reserve","небольшой запас пищи",.3f*(1-calories/2400f),2400);

        if(c.Knowledge.Facts.Contains("farming"))
        {
            var farm=c.OfKind("farm_cell").ToArray();
            var planted=farm.Count(x=>x.Definition=="planted");
            const int targetCrops=3;
            if(planted<targetCrops)
            {
                var urgency=.18f+(targetCrops-planted)*.055f;
                if(farm.Any(x=>x.Definition=="tilled"))
                    yield return new("sown","засеять подготовленную грядку",urgency);
                else if(farm.Any(x=>x.Definition=="untilled"))
                    yield return new("tilled","подготовить землю для посева",urgency);
                else if(farm.Length==0)
                    yield return new("farm_plotted","выделить место под грядки",urgency);
            }
        }
    }
}
