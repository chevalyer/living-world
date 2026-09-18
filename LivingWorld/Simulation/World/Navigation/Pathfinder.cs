namespace LivingWorld.Simulation;
public sealed class Pathfinder(WorldMap map)
{
    private readonly float[] _cost=new float[map.Tiles.Length];
    private readonly int[] _cameFrom=new int[map.Tiles.Length];
    private readonly int[] _stamp=new int[map.Tiles.Length];
    private int _generation;
    public int LastExpanded
    { get; private set; }
    public List<GridPoint>? Find(GridPoint start, GridPoint destination, int range=1, int budget=8000)
    {
        LastExpanded=0;
        if (!map.Contains(start)||!map.Contains(destination))return null;
        if (start.Distance(destination)<=range)return [];
        if (++_generation==int.MaxValue)
        {
            Array.Clear(_stamp);
            _generation=1;
        }
        var startIndex=map.Index(start);
        var queue=new PriorityQueue<int, (float, int)>();
        _cost[startIndex]=0;
        _stamp[startIndex]=_generation;
        _cameFrom[startIndex]=-1;
        queue.Enqueue(startIndex, (start.Distance(destination)*.55f, startIndex));
        while (queue.TryDequeue(out var current, out _) && LastExpanded++<budget)
        {
            var p=map.Point(current);
            if (p.Distance(destination)<=range)
            {
                var path=new List<GridPoint>();
                for (var c=current; c!=startIndex; c=_cameFrom[c])path.Add(map.Point(c));
                path.Reverse();
                return path;
            }
            foreach (var n in map.Neighbors(p))
            {
                if (!map.Walkable(n))continue;
                var index=map.Index(n);
                var cost=_cost[current]+map.Cost(n);
                if (_stamp[index]==_generation && _cost[index]<=cost)continue;
                _stamp[index]=_generation;
                _cost[index]=cost;
                _cameFrom[index]=current;
                queue.Enqueue(index, (cost+Math.Max(0, n.Distance(destination)-range)*.55f, index));
            }
        }
        return null;
    }
}
