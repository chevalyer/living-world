namespace LivingWorld.Simulation;
public sealed class WaterSystem : ISimulationSystem
{
    public string Name=>"water, ice and snow";
    public int Interval=>60;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var changed=false;
        for (var i=0; i<s.Map.Tiles.Length; i++)
        {
            var p=s.Map.Point(i);
            var t=s.Map.Tiles[i];
            var visualBefore=WorldMap.SurfaceVisualKey(t);
            var temp=EnvironmentQueries.Air(s, p);
            var wasWalkable=s.Map.Walkable(p);
            t.Snow=Math.Clamp(t.Snow+(temp<0?s.Weather.Rain*.015f:-temp*.002f), 0, 1);
            if (t.Water!=WaterKind.None)
            {
                t.WaterTemperature+=(temp-t.WaterTemperature)*(t.Water==WaterKind.Ocean ? .025f : .08f);
                var freezePoint=t.Water==WaterKind.Ocean ? -1.8f : 0;
                t.Ice=Math.Clamp(t.Ice+(t.WaterTemperature<freezePoint?(freezePoint-t.WaterTemperature)*.0008f:-t.WaterTemperature*.002f), 0, .7f);
                if (wasWalkable!=s.Map.Walkable(p))changed=true;
            }
            if (visualBefore!=WorldMap.SurfaceVisualKey(t))s.Map.MarkVisualDirty(p);
        }
        if (changed)s.Map.NavigationRevision++;
        return s.Map.Tiles.Length;
    }
}
