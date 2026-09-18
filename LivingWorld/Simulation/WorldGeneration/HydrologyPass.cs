namespace LivingWorld.Simulation;
// Priority-flood fills depressions to their spill elevation; parent links form an acyclic drainage tree.
public sealed class HydrologyPass : IGenerationPass
{
    public string Name => "водосбор, реки и озера";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var map=state.Map;
        var visited=new bool[map.Tiles.Length];
        var queue=new PriorityQueue<int, (float, int)>();
        var order=new List<int>(visited.Length);
        for (var i=0; i<visited.Length; i++)
        {
            var p=map.Point(i);
            if (map.Tiles[i].Water==WaterKind.Ocean || p.X==0 || p.Y==0 || p.X==map.Width-1 || p.Y==map.Height-1)
            {
                visited[i]=true;
                queue.Enqueue(i, (map.Tiles[i].Height, i));
            }
        }
        while (queue.TryDequeue(out var index, out _))
        {
            order.Add(index);
            foreach (var p in map.Neighbors(map.Point(index)))
            {
                var n=map.Index(p);
                if (visited[n]) continue;
                visited[n]=true;
                var tile=map.Tiles[n];
                tile.Downstream=index;
                tile.DrainageHeight=Math.Max(tile.Height, map.Tiles[index].DrainageHeight);
                queue.Enqueue(n, (tile.DrainageHeight, n));
            }
        }
        for (var k=order.Count-1; k>=0; k--)
        {
            var tile=map.Tiles[order[k]];
            if (tile.Downstream>=0) map.Tiles[tile.Downstream].Flow+=tile.Flow;
        }
        foreach (var tile in map.Tiles)
        {
            if (tile.Water==WaterKind.Ocean) continue;
            if (tile.DrainageHeight-tile.Height>.009f) tile.Water=WaterKind.Lake;
            else if (tile.Flow>Math.Max(22, map.Width*.30f)) tile.Water=WaterKind.River;
        }
    }
}
