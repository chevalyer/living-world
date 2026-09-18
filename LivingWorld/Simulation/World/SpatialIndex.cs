namespace LivingWorld.Simulation;
public sealed class SpatialIndex(WorldMap map)
{
    private readonly Dictionary<int, SortedSet<int>> _chunks=[];
    private readonly Dictionary<int, int> _location=[];
    public void Rebuild(EntityRegistry registry)
    {
        _chunks.Clear();
        _location.Clear();
        foreach (var (id, pos) in registry.Store<PositionComponent>().All) Add(id, pos.Tile);
    }
    public void Add(int id, GridPoint p)
    {
        Remove(id);
        var key=map.ChunkKey(p);
        if (!_chunks.TryGetValue(key, out var ids))_chunks[key]=ids=[];
        ids.Add(id);
        _location[id]=key;
    }
    public void Remove(int id)
    {
        if (_location.Remove(id, out var old)&&_chunks.TryGetValue(old, out var ids))ids.Remove(id);
    }
    public IEnumerable<int> Query(GridPoint center, int radius)
    {
        var cols=(map.Width+15)/16;
        for (var y=Math.Max(0, (center.Y-radius)/16); y<=Math.Min((map.Height-1)/16, (center.Y+radius)/16); y++) for (var x=Math.Max(0, (center.X-radius)/16); x<=Math.Min((map.Width-1)/16, (center.X+radius)/16); x++) if (_chunks.TryGetValue(y*cols+x, out var ids)) foreach (var id in ids)yield return id;
    }
}
