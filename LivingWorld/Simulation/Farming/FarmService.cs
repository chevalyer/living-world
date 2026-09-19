namespace LivingWorld.Simulation;
public static class FarmService
{
    public const int Width=4;
    public const int Height=4;
    private const float MinimumFertility=.25f;

    public static GridPoint? FindVisibleSite(SimulationSession session,int actor)
    {
        var s=session.State;
        if (!ActionRules.CanWork(s,actor))return null;
        var observer=s.Entities.Get<PositionComponent>(actor).Tile;
        for (var radius=1;radius<=8;radius++)
            for (var dy=-radius;dy<=radius;dy++)
                for (var dx=-radius;dx<=radius;dx++)
                {
                    if (Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                    var origin=observer+new GridPoint(dx,dy);
                    if (CanPlace(session,origin)&&Cells(origin).All(p=>p.Distance(observer)<=12&&PerceptionSystem.LineOfSight(s.Map,observer,p)))
                        return origin;
                }
        return null;
    }

    public static bool CanPlace(SimulationSession session,GridPoint origin)
    {
        var s=session.State;
        var e=s.Entities;
        var proposed=Cells(origin).ToHashSet();
        foreach (var (_,project) in e.Store<ConstructionComponent>().All)
        {
            IEnumerable<GridPoint> occupied=project.Clearance.Count>0
                ?project.Clearance
                :project.Footprint.Count>0?project.Footprint:project.Elements.Select(x=>x.Position);
            if (occupied.Any(proposed.Contains))return false;
        }
        foreach (var p in proposed)
        {
            if (!s.Map.Contains(p))return false;
            var tile=s.Map[p];
            if (!s.Map.Walkable(p)||tile.Water!=WaterKind.None||tile.Roof!=0||tile.Floor!=0||
                tile.Wall!=0||tile.Door!=0||tile.FarmPlot!=0||tile.Fertility<MinimumFertility||
                tile.Biome is Biome.Beach or Biome.Marsh or Biome.Mountain or Biome.Alpine)return false;
            if (session.Spatial.Query(p,0).Any(id=>
                e.Try<PositionComponent>(id) is { } pos&&pos.Tile==p&&
                (e.Has<PlantComponent>(id)||e.Has<ResourceComponent>(id)||e.Has<BuildingElementComponent>(id)||
                 e.Has<ConstructionComponent>(id)||e.Has<StorageComponent>(id)||e.Has<FireComponent>(id)||
                 e.Has<FarmPlotComponent>(id)||e.Has<FarmCellComponent>(id))))return false;
        }
        return true;
    }

    public static int Start(SimulationSession session,int actor,GridPoint origin)
    {
        if (!ActionRules.CanWork(session.State,actor)||!CanPlace(session,origin))return 0;
        var s=session.State;
        var e=s.Entities;
        var cells=Cells(origin).ToList();
        var id=e.Create();
        e.Set(id,new PositionComponent { Tile=new(origin.X+Width/2,origin.Y+Height/2) });
        e.Set(id,new FarmPlotComponent { Cells=cells });
        e.Set(id,new OwnershipComponent());
        session.Spatial.Add(id,e.Get<PositionComponent>(id).Tile);
        foreach (var p in cells)
        {
            s.Map[p].FarmPlot=id;
            s.Map[p].Tilled=false;
            var cell=e.Create();
            e.Set(cell,new PositionComponent { Tile=p });
            e.Set(cell,new FarmCellComponent { Plot=id });
            e.Set(cell,new OwnershipComponent());
            session.Spatial.Add(cell,p);
            s.Map.MarkVisualDirty(p);
        }
        s.Log(e.Get<IdentityComponent>(actor).FullName+" разметил грядки.");
        return id;
    }

    public static bool CanTill(SimulationSession session,int actor,int cellId,GridPoint cell)
    {
        var s=session.State;
        if (!ActionRules.CanWork(s,actor)||session.Inventory.Tool(actor,"till")<=0||!s.Map.Contains(cell))return false;
        var farmCell=s.Entities.Try<FarmCellComponent>(cellId);
        if (farmCell is null||s.Entities.Try<PositionComponent>(cellId)?.Tile!=cell)return false;
        var plot=s.Entities.Try<FarmPlotComponent>(farmCell.Plot);
        if (plot is null||!plot.Cells.Contains(cell)||s.Map[cell].FarmPlot!=farmCell.Plot||s.Map[cell].Tilled)return false;
        if (s.Entities.Get<PositionComponent>(actor).Tile.Distance(cell)>1)return false;
        return !HasPlant(session,cell);
    }

    public static bool Till(SimulationSession session,int actor,int cellId,GridPoint cell)
    {
        if (!CanTill(session,actor,cellId,cell))return false;
        session.State.Map[cell].Tilled=true;
        session.State.Map.MarkVisualDirty(cell);
        session.Inventory.WearTool(actor,"till",.8f);
        session.Events.Publish(new SkillUsedEvent(actor,"farming",2));
        return true;
    }

    public static bool CanSow(SimulationSession session,int actor,int cellId,GridPoint cell,string plantId)
    {
        var s=session.State;
        if (!ActionRules.CanWork(s,actor)||!s.Map.Contains(cell)||!session.Definitions.Plants.TryGetValue(plantId,out var plant))return false;
        var farmCell=s.Entities.Try<FarmCellComponent>(cellId);
        if (farmCell is null||s.Entities.Try<PositionComponent>(cellId)?.Tile!=cell)return false;
        var plot=s.Entities.Try<FarmPlotComponent>(farmCell.Plot);
        if (plot is null||!plot.Cells.Contains(cell)||s.Map[cell].FarmPlot!=farmCell.Plot||!s.Map[cell].Tilled||
            plant.Kind!="crop"||plant.Seed.Length==0||!session.Definitions.Items.ContainsKey(plant.Seed)||
            session.Inventory.Count(actor,plant.Seed)<1||HasPlant(session,cell))return false;
        if (s.Entities.Get<PositionComponent>(actor).Tile.Distance(cell)>1)return false;
        var air=EnvironmentQueries.Air(s,cell);
        return air>=plant.MinTemperature&&air<=plant.MaxTemperature;
    }

    public static bool Sow(SimulationSession session,int actor,int cellId,GridPoint cell,string plantId)
    {
        if (!CanSow(session,actor,cellId,cell,plantId))return false;
        var s=session.State;
        var e=s.Entities;
        var plant=session.Definitions.Plants[plantId];
        if (!session.Inventory.Consume(actor,plant.Seed,1))return false;
        var id=e.Create();
        e.Set(id,new PositionComponent { Tile=cell });
        e.Set(id,new PlantComponent { Definition=plant.Id,Growth=.01f,Cultivator=actor });
        e.Set(id,new OwnershipComponent());
        session.Spatial.Add(id,cell);
        s.Map[cell].Tilled=false;
        s.Map.MarkVisualDirty(cell);
        session.Events.Publish(new SkillUsedEvent(actor,"farming",4));
        return true;
    }

    public static string CellState(SimulationSession session,int cellId)
    {
        var e=session.State.Entities;
        var farmCell=e.Try<FarmCellComponent>(cellId);
        var position=e.Try<PositionComponent>(cellId);
        if (farmCell is null||position is null||!session.State.Map.Contains(position.Tile)||
            session.State.Map[position.Tile].FarmPlot!=farmCell.Plot)return "invalid";
        if (HasPlant(session,position.Tile))return "planted";
        return session.State.Map[position.Tile].Tilled?"tilled":"untilled";
    }

    public static bool HasPlant(SimulationSession session,GridPoint cell)
    {
        var e=session.State.Entities;
        return session.Spatial.Query(cell,0).Any(id=>e.Has<PlantComponent>(id)&&e.Get<PositionComponent>(id).Tile==cell);
    }

    public static IEnumerable<GridPoint> Cells(GridPoint origin)
    {
        for (var y=0;y<Height;y++)
            for (var x=0;x<Width;x++)
                yield return origin+new GridPoint(x,y);
    }
}
