namespace LivingWorld.Simulation;
public sealed class MovementSystem : ISimulationSystem
{
    public string Name=>"movement";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var count=0;
        foreach (var (id, movement) in e.Store<MovementComponent>().All)
        {
            movement.Previous=e.Get<PositionComponent>(id).Tile;
            if (session.Interactions.Waiting(id)||!e.Get<HealthComponent>(id).Alive||movement.Path.Count==0)continue;
            count++;
            if (!ActionRules.CanMove(s, id))
            {
                session.FailPlan(id, "нет возможности двигаться");
                continue;
            }
            var next=movement.Path[0];
            if (!s.Map.Walkable(next))
            {
                movement.Path.Clear();
                movement.Destination=null;
                continue;
            }
            movement.Progress+=e.Get<BodyComponent>(id).Mobility/s.Map.Cost(next);
            if (movement.Progress<1)continue;
            movement.Progress-=1;
            var position=e.Get<PositionComponent>(id);
            movement.Previous=position.Tile;
            position.Tile=next;
            movement.Path.RemoveAt(0);
            session.Spatial.Add(id, next);
            var tile=s.Map[next];
            var visualBefore=WorldMap.SurfaceVisualKey(tile);
            tile.Traffic=Math.Min(100, tile.Traffic+1);
            if (visualBefore!=WorldMap.SurfaceVisualKey(tile))s.Map.MarkVisualDirty(next);
        }
        return count;
    }
}
