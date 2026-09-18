namespace LivingWorld.Simulation;
public sealed class ItemConditionSystem : ISimulationSystem
{
    public string Name=>"item condition";
    public int Interval=>60;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var count=0;
        foreach (var (id, item) in s.Entities.Store<ItemComponent>().All)
        {
            count++;
            var d=session.Definitions.Items[item.Definition];
            var p=item.Holder!=0?s.Entities.Try<PositionComponent>(item.Holder)?.Tile:s.Entities.Try<PositionComponent>(id)?.Tile;
            if (p is null)continue;
            var exposed=!EnvironmentQueries.Sheltered(s, p.Value);
            var material=session.Definitions.Materials[d.Material];
            item.Wetness=Math.Clamp(item.Wetness+(exposed?s.Weather.Rain*(1-material.WaterResistance)*.08f:0)-.025f, 0, 1);
            if (d.ShelfLifeDays>0)
            {
                var temp=EnvironmentQueries.Local(s, p.Value);
                var rate=Math.Clamp(MathF.Pow(2, (temp-15)/10), .05f, 5);
                item.Freshness=Math.Max(0, item.Freshness-rate/(d.ShelfLifeDays*24));
            }
            if (item.Holder!=0&&(s.Entities.Try<EquipmentComponent>(item.Holder)?.Items.Contains(id)??false))item.Durability=Math.Max(0, item.Durability-.012f);
        }
        return count;
    }
}
