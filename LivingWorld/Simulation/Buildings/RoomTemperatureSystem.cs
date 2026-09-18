namespace LivingWorld.Simulation;
public sealed class RoomTemperatureSystem : ISimulationSystem
{
    public string Name=>"room heat";
    public int Interval=>10;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        foreach (var room in s.Rooms)
        {
            var p=room.Tiles[0];
            var heat=room.Tiles.Select(tile=>EnvironmentQueries.FireHeat(session, tile)).DefaultIfEmpty(0).Max();
            var people=s.Entities.Store<HealthComponent>().All.Count(x=>x.Value.Alive&&s.Map[s.Entities.Get<PositionComponent>(x.Key).Tile].Room==room.Id);
            var target=EnvironmentQueries.Air(s, p)+heat+Math.Min(5, people*.5f);
            room.Temperature+=(target-room.Temperature)/(5+room.Insulation*20);
        }
        return s.Rooms.Count;
    }
}
