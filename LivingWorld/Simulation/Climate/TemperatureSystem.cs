namespace LivingWorld.Simulation;
public sealed class TemperatureSystem : ISimulationSystem
{
    public string Name=>"temperature";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var count=0;
        foreach (var (id, thermal) in s.Entities.Store<ThermalComponent>().All)
        {
            var p=s.Entities.Try<PositionComponent>(id);
            if (p is null)continue;
            count++;
            var air=EnvironmentQueries.Local(s, p.Tile)+EnvironmentQueries.FireHeat(session, p.Tile);
            var protection=ClothingPhysics.Total(s, session.Definitions, id);
            var wind=EnvironmentQueries.Sheltered(s, p.Tile)?0:s.Weather.Wind*4;
            var effective=air-wind+protection*12;
            var target=thermal.Metabolism<=0?air:37+Math.Clamp((effective-18)*.06f, -5, 3);
            thermal.Temperature+=(target-thermal.Temperature)/Math.Max(1, thermal.HeatCapacity);
            var health=s.Entities.Try<HealthComponent>(id);
            if (health is not { Alive:true })continue;
            var cold=Math.Max(0,34.5f-thermal.Temperature)*.06f;
            var heat=Math.Max(0,thermal.Temperature-39.5f)*.05f;
            if(cold>0)health.Damage(cold,"переохлаждение",s.Clock.Tick);
            if(heat>0)health.Damage(heat,"перегрев",s.Clock.Tick);
        }
        return count;
    }
}
