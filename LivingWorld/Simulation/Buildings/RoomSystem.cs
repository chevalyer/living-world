namespace LivingWorld.Simulation;
public sealed class RoomSystem : ISimulationSystem
{
    public string Name=>"rooms";
    public int Interval=>10;
    public int Update(SimulationSession session)
    {
        if (!session.RoomsDirty)return 0;
        session.RoomsDirty=false;
        var s=session.State;
        var map=s.Map;
        var seen=new bool[map.Tiles.Length];
        var old=s.Rooms.ToDictionary(r=>r.Id);
        var rooms=new List<Room>();
        foreach (var tile in map.Tiles)tile.Room=0;
        for (var i=0; i<map.Tiles.Length; i++)
        {
            if (seen[i]||map.Tiles[i].Roof==0||map.Tiles[i].Wall>0)continue;
            var cells=new List<GridPoint>();
            var walls=new HashSet<int>();
            var q=new Queue<GridPoint>();
            q.Enqueue(map.Point(i));
            seen[i]=true;
            var enclosed=true;
            while (q.TryDequeue(out var p))
            {
                cells.Add(p);
                if (cells.Count>512)
                {
                    enclosed=false;
                    break;
                }
                foreach (var direction in GridPoint.Cardinal)
                {
                    var next=p+direction;
                    if (!map.Contains(next))
                    {
                        enclosed=false;
                        continue;
                    }
                    var t=map[next];
                    if (t.Wall>0)
                    {
                        walls.Add(t.Wall);
                        continue;
                    }
                    if (t.Door>0)
                    {
                        walls.Add(t.Door);
                        continue;
                    }
                    if (t.Roof==0)
                    {
                        enclosed=false;
                        continue;
                    }
                    var index=map.Index(next);
                    if (!seen[index])
                    {
                        seen[index]=true;
                        q.Enqueue(next);
                    }
                }
            }
            if (!enclosed||cells.Count==0)continue;
            var id=cells.Min(map.Index)+1;
            var insulation=walls.Count==0?0:walls.Average(w=>session.Definitions.Materials[s.Entities.Get<BuildingElementComponent>(w).Material].Insulation);
            var room=new Room
            {
                Id=id, Tiles=cells, Enclosed=true, Insulation=insulation, Temperature=old.TryGetValue(id, out var previous)?previous.Temperature:EnvironmentQueries.Air(s, cells[0])
            };
            foreach (var p in cells)map[p].Room=id;
            rooms.Add(room);
        }
        s.Rooms=rooms;
        return map.Tiles.Length;
    }
}
