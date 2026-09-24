namespace LivingWorld.Simulation;
public sealed class PlantSystem : ISimulationSystem
{
    public string Name=>"plants";
    public int Interval=>60;
    public static float GrowthRate(PlantDefinition d, float temperature, float moisture, float sunlight)
    {
        if (temperature<d.MinTemperature||temperature>d.MaxTemperature||moisture<d.MinMoisture)return 0;
        return Math.Clamp((temperature-d.MinTemperature)/10, 0, 1)*Math.Clamp(moisture, 0, 1)*Math.Clamp(sunlight, 0, 1);
    }
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var ids=e.Store<PlantComponent>().Ids();
        var r=s.Random.Stream("plant-reproduction");
        foreach (var id in ids)
        {
            var plant=e.Get<PlantComponent>(id);
            var p=e.Get<PositionComponent>(id).Tile;
            var d=session.Definitions.Plants[plant.Definition];
            var temperature=EnvironmentQueries.Air(s, p);
            var rate=GrowthRate(d, temperature, s.Map[p].Moisture, s.Weather.Sunlight);
            if(d.Kind is "grass" or "flower" or "crop")
            {rate*=1-Math.Clamp(s.Map[p].Traffic/40,0,1);plant.Health-=Math.Max(0,s.Map[p].Traffic-15)/6000;}
            plant.AgeDays+=1/24f;
            plant.Growth=Math.Min(1, plant.Growth+rate/(d.GrowthDays*24));
            if (plant.Growth>.6f)plant.Yield=Math.Min(d.Yield*plant.Growth, plant.Yield+rate*d.Yield/(d.RegrowthDays*24));
            if (temperature<d.FrostTolerance)plant.Health-=.005f;
            if (plant.AgeDays>d.LifespanDays)plant.Health-=.005f;
            if (plant.Health<=0)
            {
                e.Remove(id);
                session.Spatial.Remove(id);
                continue;
            }
            if (d.Kind=="crop")continue;
            plant.ReproductionProgress+=rate/24f;
            if (plant.ReproductionProgress<20||ids.Length>s.Map.Tiles.Length/3)continue;
            plant.ReproductionProgress=0;
            var next=p+new GridPoint(r.Range(-3, 4), r.Range(-3, 4));
            if (!s.Map.Walkable(next)||s.Map[next].Roof>0||s.Map[next].Floor>0||s.Map[next].Traffic>20||session.Spatial.Query(next, 0).Any(other=>e.Has<PlantComponent>(other)&&e.Get<PositionComponent>(other).Tile==next))continue;
            var child=e.Create();
            e.Set(child, new PositionComponent
            {
                Tile=next
            });
            e.Set(child, new PlantComponent
            {
                Definition=plant.Definition, Growth=.05f
            });
            session.Spatial.Add(child, next);
        }
        return ids.Length;
    }
}
