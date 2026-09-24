namespace LivingWorld.Simulation;
public sealed class SowAction : SimAction
{
    public override string Id=>"sow";
    public override string Label=>"сеет";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("farming"))yield break;
        var availableSeeds=c.Inventory.GroupBy(x=>x.Item.Definition)
            .ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        foreach(var item in c.Known.Where(x=>x.Kind=="item"&&x.Quantity>0&&x.UnreachableUntil<=c.Tick))
            availableSeeds[item.Product]=availableSeeds.GetValueOrDefault(item.Product)+item.Quantity;
        foreach(var storage in c.Storages())
            foreach(var item in storage.Items.Where(x=>x.Value>0))
                availableSeeds[item.Key]=availableSeeds.GetValueOrDefault(item.Key)+item.Value;
        var crops=c.Definitions.Plants.Values
            .Where(x=>x.Kind=="crop"&&x.Seed.Length>0&&availableSeeds.GetValueOrDefault(x.Seed)>=1&&c.Definitions.Items.ContainsKey(x.Seed))
            .OrderByDescending(x=>c.Definitions.Items[x.Product].Calories*x.Yield/Math.Max(1,x.GrowthDays))
            .ThenBy(x=>x.Id,StringComparer.Ordinal)
            .ToArray();
        var farmPositions=c.Known.Where(x=>x.Kind=="farm_cell").Select(x=>x.Position).ToHashSet();
        foreach (var cell in c.OfKind("farm_cell").Where(x=>x.Definition=="tilled").Take(3))
            foreach (var plant in crops)
            {
                var seedCount=availableSeeds.GetValueOrDefault(plant.Seed);
                var safeReserveRestart=seedCount==1&&
                    !c.Known.Any(x=>x.Kind=="plant"&&x.Definition==plant.Id&&farmPositions.Contains(x.Position))&&
                    cell.Temperature>=plant.MinTemperature+4&&cell.Temperature<=plant.MaxTemperature-4;
                if(seedCount<2&&!safeReserveRestart)continue;
                if(cell.Temperature<plant.MinTemperature||cell.Temperature>plant.MaxTemperature)continue;
                var op=Option(cell.Position, cell.Entity, plant.Id, 20);
                // Normally keep one seed back. If the crop is locally extinct, the last seed may restart it
                // only in a comfortably safe temperature window.
                op.Requires=[new(Item(plant.Seed), seedCount>=2?2:1)];
                op.Effects=[new(Item(plant.Seed), -1), new("sown", 1, true)];
                if(c.Definitions.Items[plant.Product].Calories>0)op.Effects.Add(new("food.sown",1,true));
                yield return op;
            }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        if (!base.CanExecute(s,actor,step,out reason))return false;
        if (FarmService.CanSow(s,actor,step.Target,step.Position,step.Argument))return true;
        reason="сеять можно только на вспаханной свободной грядке";
        return false;
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
        =>FarmService.Sow(s,actor,step.Target,step.Position,step.Argument);
}
