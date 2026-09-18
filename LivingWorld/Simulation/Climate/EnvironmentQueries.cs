namespace LivingWorld.Simulation;
public static class EnvironmentQueries
{
    public static float Air(WorldState s, GridPoint p)
    {
        var daily=MathF.Cos((s.Clock.Hour-15)/24*MathF.Tau)*3;
        return s.Map[p].BaseTemperature+s.Weather.SeasonalOffset+s.Weather.Anomaly+daily;
    }
    public static bool Sheltered(WorldState s, GridPoint p)=>s.Map[p].Room>0&&s.Map[p].Roof>0;
    public static float Local(WorldState s, GridPoint p)
    {
        var room=s.Rooms.FirstOrDefault(r=>r.Id==s.Map[p].Room);
        return room is
        {
            Enclosed:true
        }
        ?room.Temperature:Air(s, p);
    }
    public static float FireHeat(SimulationSession session, GridPoint p)
    {
        float heat=0;
        foreach (var id in session.Spatial.Query(p, 4))
        {
            var fire=session.State.Entities.Try<FireComponent>(id);
            if (fire is null||fire.FuelMinutes<=0)continue;
            var distance=session.State.Entities.Get<PositionComponent>(id).Tile.Distance(p);
            if (distance<=4)heat+=fire.Heat/(1+distance*distance);
        }
        return Math.Min(25, heat);
    }
}
