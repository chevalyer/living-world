namespace LivingWorld.Simulation;
public sealed class TrafficSystem : ISimulationSystem
{
    public string Name=>"trail recovery";
    public int Interval=>1440;
    public int Update(SimulationSession session)
    {
        var map=session.State.Map;
        for (var index=0; index<map.Tiles.Length; index++)
        {
            var tile=map.Tiles[index];
            var before=WorldMap.SurfaceVisualKey(tile);
            tile.Traffic=Math.Max(0, tile.Traffic*.985f-.05f);
            if (before!=WorldMap.SurfaceVisualKey(tile))map.MarkVisualDirty(map.Point(index));
        }
        return session.State.Map.Tiles.Length;
    }
}
