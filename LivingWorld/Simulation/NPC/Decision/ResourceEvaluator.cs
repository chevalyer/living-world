namespace LivingWorld.Simulation;
public sealed class ResourceEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        var personalSpoiled=c.Inventory.Any(x=>x.Item.Freshness<.1f&&c.Definitions.Items[x.Item.Definition].Calories>0);
        var storageSpoiled=c.OfKind("storage").Any(o=>o.Spoiled>0);
        if(personalSpoiled||storageSpoiled)yield return new("cleaned","освободить место от испорченной еды",.8f);
        if(!c.CanWork)yield break;
        if(c.OfKind("storage").Any()&&c.Inventory.GroupBy(x=>x.Item.Definition).Any(g=>g.Count()>5))
            yield return new("distributed","общий запас ресурсов",c.Personality.Generosity*.6f);
        var calories=(int)c.Inventory.Sum(x=>c.Definitions.Items[x.Item.Definition].Calories*Math.Max(0,x.Item.Freshness));
        if(calories<2400)yield return new("food.reserve","небольшой запас пищи",.3f*(1-calories/2400f),2400);
        var recentCrop=c.Known.Any(o=>o.Kind=="plant"&&o.Owner==c.Actor&&
            c.Definitions.Plants[o.Definition].Kind=="crop"&&c.Tick-o.SeenTick<1440*3);
        if(c.Knowledge.Facts.Contains("farming")&&!recentCrop)
            yield return new("sown","возобновляемая еда рядом",.18f);
    }
}
