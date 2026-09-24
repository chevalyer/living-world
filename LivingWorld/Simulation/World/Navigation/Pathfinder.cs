namespace LivingWorld.Simulation;
public sealed class Pathfinder(WorldMap map)
{
    private readonly float[] _cost=new float[map.Tiles.Length];
    private readonly int[] _cameFrom=new int[map.Tiles.Length];
    private readonly int[] _stamp=new int[map.Tiles.Length];
    private readonly int[] _components=Enumerable.Repeat(-1,map.Tiles.Length).ToArray();
    private int _generation;
    private int _componentRevision=int.MinValue;

    public int LastExpanded { get; private set; }

    public bool CanReach(GridPoint start,GridPoint destination,int range=1)
    {
        if(!map.Contains(start)||!map.Contains(destination)||range<0)return false;
        if(start.Distance(destination)<=range)return true;
        EnsureComponents();

        var starts=new HashSet<int>();
        AddComponent(start,starts);
        if(starts.Count==0)
            foreach(var neighbor in map.Neighbors(start))AddComponent(neighbor,starts);
        if(starts.Count==0)return false;

        for(var dy=-range;dy<=range;dy++)
        for(var dx=-range;dx<=range;dx++)
        {
            if(Math.Abs(dx)+Math.Abs(dy)>range)continue;
            var p=destination+new GridPoint(dx,dy);
            if(!map.Contains(p)||!map.Walkable(p))continue;
            var component=_components[map.Index(p)];
            if(component>=0&&starts.Contains(component))return true;
        }
        return false;
    }

    public List<GridPoint>? Find(GridPoint start,GridPoint destination,int range=1,int budget=8000)
    {
        LastExpanded=0;
        if(!map.Contains(start)||!map.Contains(destination))return null;
        if(start.Distance(destination)<=range)return [];
        if(!CanReach(start,destination,range))return null;
        if(++_generation==int.MaxValue)
        {
            Array.Clear(_stamp);
            _generation=1;
        }
        var startIndex=map.Index(start);
        var queue=new PriorityQueue<int,(float,int)>();
        _cost[startIndex]=0;
        _stamp[startIndex]=_generation;
        _cameFrom[startIndex]=-1;
        queue.Enqueue(startIndex,(start.Distance(destination)*.55f,startIndex));
        while(queue.TryDequeue(out var current,out _)&&LastExpanded++<budget)
        {
            var p=map.Point(current);
            if(p.Distance(destination)<=range)
            {
                var path=new List<GridPoint>();
                for(var c=current;c!=startIndex;c=_cameFrom[c])path.Add(map.Point(c));
                path.Reverse();
                return path;
            }
            foreach(var n in map.Neighbors(p))
            {
                if(!map.Walkable(n))continue;
                var index=map.Index(n);
                var cost=_cost[current]+map.Cost(n);
                if(_stamp[index]==_generation&&_cost[index]<=cost)continue;
                _stamp[index]=_generation;
                _cost[index]=cost;
                _cameFrom[index]=current;
                queue.Enqueue(index,(cost+Math.Max(0,n.Distance(destination)-range)*.55f,index));
            }
        }
        return null;
    }

    private void AddComponent(GridPoint p,HashSet<int> target)
    {
        if(!map.Contains(p)||!map.Walkable(p))return;
        var component=_components[map.Index(p)];
        if(component>=0)target.Add(component);
    }

    private void EnsureComponents()
    {
        if(_componentRevision==map.NavigationRevision)return;
        Array.Fill(_components,-1);
        var queue=new Queue<int>();
        var nextComponent=0;
        for(var index=0;index<map.Tiles.Length;index++)
        {
            var start=map.Point(index);
            if(_components[index]>=0||!map.Walkable(start))continue;
            _components[index]=nextComponent;
            queue.Enqueue(index);
            while(queue.TryDequeue(out var current))
            {
                foreach(var p in map.Neighbors(map.Point(current)))
                {
                    if(!map.Walkable(p))continue;
                    var neighbor=map.Index(p);
                    if(_components[neighbor]>=0)continue;
                    _components[neighbor]=nextComponent;
                    queue.Enqueue(neighbor);
                }
            }
            nextComponent++;
        }
        _componentRevision=map.NavigationRevision;
    }
}
