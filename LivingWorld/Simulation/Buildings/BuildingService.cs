namespace LivingWorld.Simulation;
public static class BuildingService
{
    public static GridPoint? FindVisibleSite(SimulationSession session, int actor)
    {
        var s=session.State;
        var origin=s.Entities.Get<PositionComponent>(actor).Tile;
        if (!ActionRules.CanWork(s, actor)||s.Entities.Get<FamilyComponent>(actor).HomeProject!=0)return null;

        var definitions=session.Definitions.Buildings.Values.OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
        for (var radius=1; radius<=10; radius++)
        for (var dy=-radius; dy<=radius; dy++)
        for (var dx=-radius; dx<=radius; dx++)
        {
            if (Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
            var center=origin+new GridPoint(dx, dy);
            foreach (var definition in definitions)
            {
                var layout=CreateLayout(session, actor, center, definition);
                if (AllVisible(s.Map, origin, layout)&&CanPlace(session, actor, layout))return center;
            }
        }
        return null;
    }

    public static BuildingLayout CreateLayout(SimulationSession session, int actor, GridPoint center, BuildingDefinition definition)
    {
        var random=new DeterministicRandom(RandomService.Hash(session.State.Seed,
            $"building:{actor}:{center.X}:{center.Y}"));

        var minWidth=Math.Clamp(definition.MinWidth,5,11);
        var maxWidth=Math.Clamp(Math.Max(minWidth,definition.MaxWidth),minWidth,11);
        var minHeight=Math.Clamp(definition.MinHeight,5,11);
        var maxHeight=Math.Clamp(Math.Max(minHeight,definition.MaxHeight),minHeight,11);
        var maxArea=Math.Clamp(definition.MaxInteriorArea,6,81);
        var targetArea=Math.Clamp(ResidentialTargetArea(session,actor)+random.Range(-2,3),6,maxArea);

        var dimensions=(
            from width in Enumerable.Range(minWidth,maxWidth-minWidth+1)
            from height in Enumerable.Range(minHeight,maxHeight-minHeight+1)
            let area=(width-2)*(height-2)
            where area>=6&&area<=maxArea
            let score=Math.Abs(area-targetArea)+Math.Abs(width-height)*.18f
            orderby score,width,height
            select (Width:width,Height:height,Score:score)
        ).ToArray();
        if (dimensions.Length==0)throw new InvalidOperationException("Building definition has no valid dimensions: "+definition.Id);

        var bestScore=dimensions[0].Score;
        var choices=dimensions.Where(x=>x.Score<=bestScore+1.1f).ToArray();
        var chosen=choices[random.Range(0,choices.Length)];
        var left=center.X-chosen.Width/2;
        var top=center.Y-chosen.Height/2;

        var interior=new HashSet<GridPoint>();
        for (var y=top+1; y<top+chosen.Height-1; y++)
        for (var x=left+1; x<left+chosen.Width-1; x++)
            interior.Add(new(x,y));

        if (chosen.Width>=7&&chosen.Height>=7&&random.Chance(Math.Clamp(definition.ShapeVariety,0,1)))
        {
            var original=interior.ToHashSet();
            var cutWidth=random.Range(1,Math.Min(3,chosen.Width-3)+1);
            var cutHeight=random.Range(1,Math.Min(3,chosen.Height-3)+1);
            var corner=random.Range(0,4);
            var xs=corner is 0 or 3
                ?Enumerable.Range(left+1,cutWidth)
                :Enumerable.Range(left+chosen.Width-1-cutWidth,cutWidth);
            var ys=corner is 0 or 1
                ?Enumerable.Range(top+1,cutHeight)
                :Enumerable.Range(top+chosen.Height-1-cutHeight,cutHeight);
            foreach (var y in ys)foreach (var x in xs)interior.Remove(new(x,y));
            if (interior.Count<6||!Connected(interior))interior=original;
        }

        var boundary=new HashSet<GridPoint>();
        foreach (var cell in interior)
        foreach (var direction in GridPoint.Cardinal)
        {
            var wall=cell+direction;
            if (!interior.Contains(wall))boundary.Add(wall);
        }

        var footprint=interior.Concat(boundary).ToHashSet();
        var actorPosition=session.State.Entities.Get<PositionComponent>(actor).Tile;
        var candidates=new List<(GridPoint Wall,GridPoint Outside,GridPoint Approach,float Score)>();
        foreach (var wall in boundary)
        {
            var insideNeighbors=GridPoint.Cardinal.Select(d=>wall+d).Where(interior.Contains).ToArray();
            if (insideNeighbors.Length!=1)continue;
            var inside=insideNeighbors[0];
            var direction=new GridPoint(wall.X-inside.X,wall.Y-inside.Y);
            var outside=wall+direction;
            var approach=outside+direction;
            if (!session.State.Map.Contains(outside)||!session.State.Map.Contains(approach))continue;
            if (footprint.Contains(outside)||footprint.Contains(approach))continue;
            if (!session.State.Map.Walkable(outside)||!session.State.Map.Walkable(approach))continue;
            var outsideTile=session.State.Map[outside];
            var approachTile=session.State.Map[approach];
            if (outsideTile.Roof!=0||outsideTile.Floor!=0||outsideTile.Door!=0||
                approachTile.Roof!=0||approachTile.Floor!=0||approachTile.Door!=0)continue;
            var score=outside.Distance(actorPosition)-Math.Min(20,outsideTile.Traffic)*.08f;
            candidates.Add((wall,outside,approach,score));
        }

        if (candidates.Count==0)
            return new()
            {
                Interior=interior.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
                Boundary=boundary.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
                Footprint=footprint.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
                Width=chosen.Width, Height=chosen.Height
            };

        var door=candidates.OrderBy(x=>x.Score).ThenBy(x=>x.Wall.Y).ThenBy(x=>x.Wall.X).First();
        var clearance=new HashSet<GridPoint>();
        foreach (var cell in footprint)
        for (var y=-1; y<=1; y++)
        for (var x=-1; x<=1; x++)
            clearance.Add(cell+new GridPoint(x,y));
        clearance.Add(door.Outside);
        clearance.Add(door.Approach);

        return new()
        {
            Interior=interior.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
            Boundary=boundary.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
            Footprint=footprint.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
            Clearance=clearance.OrderBy(p=>p.Y).ThenBy(p=>p.X).ToList(),
            Door=door.Wall,
            DoorOutside=door.Outside,
            HasDoor=true,
            Width=chosen.Width,
            Height=chosen.Height
        };
    }

    private static int ResidentialTargetArea(SimulationSession session, int actor)
    {
        var family=session.State.Entities.Get<FamilyComponent>(actor);
        var members=1+(family.Partner!=0?1:0)+family.Children.Count;
        return 11+Math.Max(0,members-1)*5;
    }

    public static bool CanPlace(SimulationSession session, int actor, BuildingLayout layout)
    {
        var s=session.State;
        var e=s.Entities;
        if (!ValidLayout(layout)||!s.Map.Contains(layout.DoorOutside))return false;

        var footprint=layout.Footprint.ToHashSet();
        foreach (var p in layout.Footprint)
        {
            if (!s.Map.Contains(p))return false;
            var tile=s.Map[p];
            if (tile.Water!=WaterKind.None||tile.Wall!=0||tile.Door!=0||tile.Roof!=0||tile.Floor!=0||
                tile.Biome==Biome.Alpine)return false;
        }

        foreach (var p in layout.Clearance)
        {
            if (!s.Map.Contains(p))return false;
            if (footprint.Contains(p))continue;
            var tile=s.Map[p];
            if (tile.Wall!=0||tile.Door!=0||tile.Roof!=0||tile.Floor!=0)return false;
        }

        foreach (var id in session.Spatial.Query(layout.DoorOutside,Math.Max(layout.Width,layout.Height)+4))
        {
            var position=e.Try<PositionComponent>(id);
            if (position is null||!footprint.Contains(position.Tile))continue;
            if (e.Has<IdentityComponent>(id)&&id!=actor||e.Has<StorageComponent>(id)||e.Has<FireComponent>(id)||
                e.Has<ResourceComponent>(id))return false;
            if (e.Try<PlantComponent>(id) is { } plant&&session.Definitions.Plants[plant.Definition].Kind=="tree")return false;
        }

        foreach (var (_,project) in e.Store<ConstructionComponent>().All)
        {
            var occupied=project.Clearance.Count>0
                ?project.Clearance.ToHashSet()
                :Expand(project.Elements.Select(x=>x.Position),1);
            if (layout.Clearance.Any(occupied.Contains))return false;
        }

        var actorPosition=e.Get<PositionComponent>(actor).Tile;
        return session.Pathfinder.Find(actorPosition,layout.DoorOutside,0,1600) is not null;
    }

    // Compatibility helper for older callers. New construction uses exact generated layouts.
    public static bool CanPlace(SimulationSession session, GridPoint center, int size)
    {
        var radius=size/2;
        for (var dy=-radius; dy<=radius; dy++)
        for (var dx=-radius; dx<=radius; dx++)
        {
            var p=center+new GridPoint(dx,dy);
            if (!session.State.Map.Contains(p))return false;
            var tile=session.State.Map[p];
            if (tile.Water!=WaterKind.None||tile.Wall!=0||tile.Door!=0||tile.Roof!=0||tile.Floor!=0)return false;
        }
        return true;
    }

    public static int Start(SimulationSession session, int actor, GridPoint center, string definition)
    {
        var s=session.State;
        var d=session.Definitions.Buildings[definition];
        var family=s.Entities.Get<FamilyComponent>(actor);
        if (family.HomeProject!=0||!ActionRules.CanWork(s,actor))return 0;

        var layout=CreateLayout(session,actor,center,d);
        if (!CanPlace(session,actor,layout))return 0;

        var project=new ConstructionComponent
        {
            Definition=definition,
            Contributors=[actor],
            Interior=layout.Interior.ToList(),
            Footprint=layout.Footprint.ToList(),
            Clearance=layout.Clearance.ToList(),
            Door=layout.Door,
            DoorOutside=layout.DoorOutside
        };
        foreach (var p in layout.Interior.OrderBy(p=>p.Distance(center)))project.Elements.Add(new(p,"floor"));
        foreach (var p in layout.Interior.OrderBy(p=>p.Distance(center)))project.Elements.Add(new(p,"roof"));
        project.Elements.Add(new(layout.Door,"door"));
        foreach (var p in layout.Boundary.Where(p=>p!=layout.Door).OrderBy(p=>p.Y).ThenBy(p=>p.X))
            project.Elements.Add(new(p,"wall"));

        var id=s.Entities.Create();
        s.Entities.Set(id,new PositionComponent { Tile=center });
        s.Entities.Set(id,project);
        s.Entities.Set(id,new OwnershipComponent { Owner=actor });
        session.Spatial.Add(id,center);
        family.HomeProject=id;
        s.Log(s.Entities.Get<IdentityComponent>(actor).FullName+" начал строить дом.");
        return id;
    }

    public static bool BuildNext(SimulationSession session, int actor, int projectId)
    {
        var s=session.State;
        var e=s.Entities;
        var project=e.Try<ConstructionComponent>(projectId);
        if (project is null||project.Finished||!ActionRules.CanWork(s,actor))return false;
        var next=project.Elements[project.Completed];
        if (e.Get<PositionComponent>(actor).Tile.Distance(next.Position)>1)return false;
        var d=session.Definitions.Buildings[project.Definition];
        if (session.Inventory.Count(actor,d.Resource)<d.UnitsPerElement)return false;
        if (next.Kind=="wall")
        {
            var escape=s.Map.Neighbors(next.Position).FirstOrDefault(s.Map.Walkable,new GridPoint(-1,-1));
            var occupants=e.Store<IdentityComponent>().Ids().Where(id=>e.Get<PositionComponent>(id).Tile==next.Position).ToArray();
            if (occupants.Length>0&&!s.Map.Contains(escape))return false;
            foreach (var id in occupants)
            {
                e.Get<PositionComponent>(id).Tile=escape;
                session.Spatial.Add(id,escape);
                e.Get<MovementComponent>(id).Path.Clear();
            }
        }
        if (!session.Inventory.Consume(actor,d.Resource,d.UnitsPerElement))return false;
        if (next.Kind=="floor")
        {
            foreach (var vegetation in session.Spatial.Query(next.Position,0)
                         .Where(id=>e.Has<PlantComponent>(id)&&e.Get<PositionComponent>(id).Tile==next.Position).ToArray())
            {
                e.Remove(vegetation);
                session.Spatial.Remove(vegetation);
            }
        }
        var element=e.Create();
        e.Set(element,new PositionComponent { Tile=next.Position });
        e.Set(element,new BuildingElementComponent
        {
            Kind=next.Kind,Material=d.Material,Project=projectId,
            Durability=session.Definitions.Materials[d.Material].Strength*150
        });
        var tile=s.Map[next.Position];
        switch (next.Kind)
        {
            case "wall":tile.Wall=element;break;
            case "door":tile.Door=element;break;
            case "roof":tile.Roof=element;break;
            case "floor":tile.Floor=element;break;
        }
        session.Spatial.Add(element,next.Position);
        project.Completed++;
        if (!project.Contributors.Contains(actor))project.Contributors.Add(actor);
        if (e.Get<FamilyComponent>(actor).HomeProject==0)e.Get<FamilyComponent>(actor).HomeProject=projectId;
        s.Map.NavigationRevision++;
        s.Map.MarkVisualDirty(next.Position);
        session.RoomsDirty=true;
        session.Events.Publish(new SkillUsedEvent(actor,"building",5));
        if (project.Finished)
        {
            s.Log("Дом завершен. Участников: "+project.Contributors.Count+".");
            StorageService.Create(session,e.Get<PositionComponent>(projectId).Tile,projectId);
        }
        return true;
    }

    private static bool AllVisible(WorldMap map, GridPoint observer, BuildingLayout layout)
    {
        if (layout.Footprint.Count==0||!map.Contains(layout.DoorOutside))return false;
        foreach (var p in layout.Footprint.Append(layout.DoorOutside))
            if (!map.Contains(p)||p.Distance(observer)>12||!PerceptionSystem.LineOfSight(map,observer,p))return false;
        return true;
    }

    private static bool ValidLayout(BuildingLayout layout)
    {
        if (layout.Interior.Count<6||layout.Footprint.Count==0||layout.Clearance.Count==0||!layout.HasDoor)return false;
        var interior=layout.Interior.ToHashSet();
        if (!Connected(interior))return false;
        if (layout.Boundary.Any(p=>interior.Contains(p)||!GridPoint.Cardinal.Any(d=>interior.Contains(p+d))))return false;
        return layout.Footprint.Count==interior.Count+layout.Boundary.Distinct().Count();
    }

    private static bool Connected(HashSet<GridPoint> cells)
    {
        if (cells.Count==0)return false;
        var seen=new HashSet<GridPoint>();
        var queue=new Queue<GridPoint>();
        var first=cells.First();
        seen.Add(first);
        queue.Enqueue(first);
        while (queue.TryDequeue(out var current))
            foreach (var direction in GridPoint.Cardinal)
            {
                var next=current+direction;
                if (cells.Contains(next)&&seen.Add(next))queue.Enqueue(next);
            }
        return seen.Count==cells.Count;
    }

    private static HashSet<GridPoint> Expand(IEnumerable<GridPoint> cells, int radius)
    {
        var result=new HashSet<GridPoint>();
        foreach (var cell in cells)
        for (var y=-radius; y<=radius; y++)
        for (var x=-radius; x<=radius; x++)
            result.Add(cell+new GridPoint(x,y));
        return result;
    }
}
