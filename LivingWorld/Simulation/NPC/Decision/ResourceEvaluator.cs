namespace LivingWorld.Simulation;
public sealed class ResourceEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        if (c.Inventory.Any(x=>x.Item.Freshness<.1f&&c.Definitions.Items[x.Item.Definition].Calories>0))yield return new("cleaned", "освободить место от испорченной еды", .8f);
        if (!c.CanWork)yield break;
        if (c.OfKind("storage").Any()&&c.Inventory.GroupBy(x=>x.Item.Definition).Any(g=>g.Count()>5))yield return new("distributed", "общий запас ресурсов", c.Personality.Generosity*.6f);
        var calories=c.Inventory.Sum(x=>c.Definitions.Items[x.Item.Definition].Calories*x.Item.Freshness);
        if (calories<2400)yield return new("stocked", "небольшой запас пищи", .3f*(1-calories/2400));
        if (c.Knowledge.Facts.Contains("farming"))
        {
            var farm=c.OfKind("farm_cell").ToArray();
            var hasCrop=farm.Any(x=>x.Definition=="planted");
            if (!hasCrop)
            {
                if (farm.Any(x=>x.Definition=="tilled"))
                    yield return new("sown", "засеять подготовленную грядку", .18f);
                else if (farm.Any(x=>x.Definition=="untilled"))
                    yield return new("tilled", "подготовить землю для посева", .18f);
                else if (farm.Length==0)
                    yield return new("farm_plotted", "выделить место под грядки", .18f);
            }
        }
    }
}
