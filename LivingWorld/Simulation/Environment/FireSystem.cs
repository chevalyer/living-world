namespace LivingWorld.Simulation;
public sealed class FireSystem : ISimulationSystem
{
    public string Name=>"fires";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var ids=s.Entities.Store<FireComponent>().Ids();
        foreach (var id in ids)
        {
            var fire=s.Entities.Get<FireComponent>(id);
            var p=s.Entities.Get<PositionComponent>(id).Tile;
            fire.FuelMinutes=Math.Max(0, fire.FuelMinutes-1-(EnvironmentQueries.Sheltered(s, p)?0:s.Weather.Rain*2));
        }
        return ids.Length;
    }
}
