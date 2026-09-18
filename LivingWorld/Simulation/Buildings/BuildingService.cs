namespace LivingWorld.Simulation;
public static class BuildingService
{
    public static GridPoint? FindVisibleSite(SimulationSession session, int actor)
    {
        var s=session.State;
        var p=s.Entities.Get<PositionComponent>(actor).Tile;
        if (!ActionRules.CanWork(s, actor)||s.Entities.Get<FamilyComponent>(actor).HomeProject!=0)return null;
        for (var radius=0; radius<=4; radius++) for (var dy=-radius; dy<=radius; dy++)for (var dx=-radius; dx<=radius; dx++)
        {
            if (Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
            var center=p+new GridPoint(dx, dy);
            if (CanPlace(session, center, 5)&&AllVisible(s.Map, p, center, 5))return center;
        }
        return null;
    }
    private static bool AllVisible(WorldMap map, GridPoint observer, GridPoint center, int size)
    {
        var r=size/2;
        for (var y=-r; y<=r; y++)for (var x=-r; x<=r; x++)
        {
            var p=center+new GridPoint(x, y);
            if (p.Distance(observer)>7||!PerceptionSystem.LineOfSight(map, observer, p))return false;
        }
        return true;
    }
    public static bool CanPlace(SimulationSession session, GridPoint center, int size)
    {
        var s=session.State;
        var radius=size/2;
        for (var dy=-radius; dy<=radius; dy++)for (var dx=-radius; dx<=radius; dx++)
        {
            var p=center+new GridPoint(dx, dy);
            if (!s.Map.Walkable(p)||s.Map[p].Roof!=0||s.Map[p].Floor!=0||s.Map[p].Water!=WaterKind.None)return false;
        }
        if (session.Spatial.Query(center, size).Any(id=>
        {
            var plant=s.Entities.Try<PlantComponent>(id); var p=s.Entities.Get<PositionComponent>(id).Tile; return plant is not null&&session.Definitions.Plants[plant.Definition].Kind=="tree"&&Math.Abs(p.X-center.X)<=radius&&Math.Abs(p.Y-center.Y)<=radius;
        }))return false;
        return !s.Entities.Store<ConstructionComponent>().All.Any(x=>s.Entities.Get<PositionComponent>(x.Key).Tile.Distance(center)<size+1);
    }
    public static int Start(SimulationSession session, int actor, GridPoint center, string definition)
    {
        var s=session.State;
        var d=session.Definitions.Buildings[definition];
        var family=s.Entities.Get<FamilyComponent>(actor);
        if (family.HomeProject!=0||!ActionRules.CanWork(s, actor)||!CanPlace(session, center, d.Size))return 0;
        var project=new ConstructionComponent
        {
            Definition=definition, Contributors=[actor]
        };
        var radius=d.Size/2;
        var interior=new List<GridPoint>();
        for (var y=-radius+1; y<radius; y++)for (var x=-radius+1; x<radius; x++)interior.Add(center+new GridPoint(x, y));
        foreach (var p in interior.OrderBy(p=>p.Distance(center)))project.Elements.Add(new(p, "floor"));
        foreach (var p in interior.OrderBy(p=>p.Distance(center)))project.Elements.Add(new(p, "roof"));
        // The door keeps a navigable entrance even after the final wall closes the room.
        project.Elements.Add(new(center+new GridPoint(0, radius), "door"));
        for (var y=-radius; y<=radius; y++)for (var x=-radius; x<=radius; x++) if ((Math.Abs(x)==radius||Math.Abs(y)==radius)&&!(x==0&&y==radius))project.Elements.Add(new(center+new GridPoint(x, y), "wall"));
        var id=s.Entities.Create();
        s.Entities.Set(id, new PositionComponent
        {
            Tile=center
        });
        s.Entities.Set(id, project);
        s.Entities.Set(id, new OwnershipComponent
        {
            Owner=actor
        });
        session.Spatial.Add(id, center);
        family.HomeProject=id;
        s.Log(s.Entities.Get<IdentityComponent>(actor).FullName+" начал строить дом.");
        return id;
    }
    public static bool BuildNext(SimulationSession session, int actor, int projectId)
    {
        var s=session.State;
        var e=s.Entities;
        var project=e.Try<ConstructionComponent>(projectId);
        if (project is null||project.Finished||!ActionRules.CanWork(s, actor))return false;
        var next=project.Elements[project.Completed];
        if (e.Get<PositionComponent>(actor).Tile.Distance(next.Position)>1)return false;
        var d=session.Definitions.Buildings[project.Definition];
        if (session.Inventory.Count(actor, d.Resource)<d.UnitsPerElement)return false;
        if (next.Kind=="wall")
        {
            var escape=s.Map.Neighbors(next.Position).FirstOrDefault(s.Map.Walkable, new GridPoint(-1, -1));
            var occupants=e.Store<IdentityComponent>().Ids().Where(id=>e.Get<PositionComponent>(id).Tile==next.Position).ToArray();
            if (occupants.Length>0&&!s.Map.Contains(escape))return false;
            foreach (var id in occupants)
            {
                e.Get<PositionComponent>(id).Tile=escape;
                session.Spatial.Add(id, escape);
                e.Get<MovementComponent>(id).Path.Clear();
            }
        }
        if (!session.Inventory.Consume(actor, d.Resource, d.UnitsPerElement))return false;
        if (next.Kind=="floor")
        {
            foreach (var vegetation in session.Spatial.Query(next.Position, 0).Where(id=>e.Has<PlantComponent>(id)&&e.Get<PositionComponent>(id).Tile==next.Position).ToArray())
            {
                e.Remove(vegetation);
                session.Spatial.Remove(vegetation);
            }
        }
        var element=e.Create();
        e.Set(element, new PositionComponent
        {
            Tile=next.Position
        });
        e.Set(element, new BuildingElementComponent
        {
            Kind=next.Kind, Material=d.Material, Project=projectId, Durability=session.Definitions.Materials[d.Material].Strength*150
        });
        var tile=s.Map[next.Position];
        switch (next.Kind)
        {
            case "wall":tile.Wall=element;
            break;
            case "door":tile.Door=element;
            break;
            case "roof":tile.Roof=element;
            break;
            case "floor":tile.Floor=element;
            break;
        }
        session.Spatial.Add(element, next.Position);
        project.Completed++;
        if (!project.Contributors.Contains(actor))project.Contributors.Add(actor);
        if (e.Get<FamilyComponent>(actor).HomeProject==0)e.Get<FamilyComponent>(actor).HomeProject=projectId;
        s.Map.NavigationRevision++;
        s.Map.MarkVisualDirty(next.Position);
        session.RoomsDirty=true;
        session.Events.Publish(new SkillUsedEvent(actor, "building", 5));
        if(project.Finished)
            s.Log("Дом завершен. Участников: "+project.Contributors.Count+".");
        return true;
    }
}
