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

        var storages=c.Storages().ToArray();
        if(storages.Length>0)
        {
            var storedCalories=c.StoredFoodCalories();
            var preservedCalories=c.PreservedFoodCalories();
            var household=1+(c.Family.Partner!=0?1:0)+c.Family.Children.Count;
            // Needs consume about 960 definition-calories per person/day. A 45-day household
            // reserve lets harvests bridge cold periods instead of being eaten only hand-to-mouth.
            var targetStoredCalories=Math.Max(43200,household*43200);
            if(storedCalories<targetStoredCalories)
            {
                var missing=1-storedCalories/(float)targetStoredCalories;
                // Keep the goal incremental so a bounded planner can make steady progress toward a large seasonal reserve.
                yield return new("food.stocked","запас пищи семьи на холодный период",.35f+.35f*missing,storedCalories+1);
            }

            var perishableCalories=(int)MathF.Round(
                c.Inventory.Where(x=>x.Item.Freshness>=.1f&&!c.IsPreservedFood(x.Item.Definition))
                    .Sum(x=>c.Definitions.Items[x.Item.Definition].Calories*x.Item.Freshness)+
                storages.Sum(storage=>storage.Items.Sum(item=>
                    c.Definitions.Items.ContainsKey(item.Key)&&!c.IsPreservedFood(item.Key)
                        ?c.Definitions.Items[item.Key].Calories*item.Value:0)));
            if(perishableCalories>0&&preservedCalories<targetStoredCalories/2)
                yield return new("food.preserved","сохранить скоропортящуюся пищу",.28f+
                    Math.Min(.3f,perishableCalories/2400f*.3f),preservedCalories+1);
        }

        if(c.Knowledge.Facts.Contains("farming"))
        {
            var farmCells=c.Known.Where(x=>x.Kind=="farm_cell"&&x.UnreachableUntil<=c.Tick).ToArray();
            var farmCapacity=c.Known.Where(x=>x.Kind=="farm_plot"&&x.UnreachableUntil<=c.Tick)
                .Sum(x=>Math.Max(0,x.Quantity));
            var knownPeople=1+c.Known.Where(x=>x.Kind=="npc")
                .Select(x=>x.Entity).Distinct().Count();
            var targetFoodCrops=Math.Max(12,knownPeople*12);
            var farmPositions=farmCells.Select(x=>x.Position).ToHashSet();
            var plantedFood=c.Known.Where(x=>x.Kind=="plant"&&x.UnreachableUntil<=c.Tick&&farmPositions.Contains(x.Position))
                .Count(x=>c.Definitions.Plants.TryGetValue(x.Definition,out var plant)&&plant.Kind=="crop"&&
                    c.Definitions.Items.TryGetValue(plant.Product,out var product)&&product.Calories>0);
            var shortage=Math.Max(0,targetFoodCrops-plantedFood);
            if(shortage>0)
            {
                var urgency=.22f+Math.Min(.5f,shortage/(float)targetFoodCrops*.5f);
                if(farmCapacity<targetFoodCrops)
                    yield return new("farm_plotted","расширить запас продовольственных грядок",urgency*.9f);
                if(farmCells.Any(x=>x.Definition=="tilled"))
                    yield return new("food.sown","засеять продовольственную грядку",urgency);
                else if(farmCells.Any(x=>x.Definition=="untilled"))
                    yield return new("tilled","подготовить землю для посева",urgency);
            }
        }
    }
}
