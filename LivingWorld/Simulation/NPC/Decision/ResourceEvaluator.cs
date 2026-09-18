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
            var ownCrops=c.Known.Count(o=>o.Kind=="plant"&&o.Owner==c.Actor&&
                c.Definitions.Plants.TryGetValue(o.Definition,out var plant)&&plant.Kind=="crop"&&c.Tick-o.SeenTick<1440*20);
            const int targetCrops=3;
            if(ownCrops<targetCrops)
                yield return new("sown","расширить возобновляемый запас еды",.18f+(targetCrops-ownCrops)*.055f);
        }
    }
}
