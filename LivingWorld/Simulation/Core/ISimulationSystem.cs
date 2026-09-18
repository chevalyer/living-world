namespace LivingWorld.Simulation;
public interface ISimulationSystem
{
    string Name
    { get; }
    int Interval
    { get; }
    int Update(SimulationSession session);
}
