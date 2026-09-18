namespace LivingWorld.Simulation;
public static class FacilityService
{
    public static bool HasCapability(SimulationSession session,int entity,string capability)
    {
        var facility=session.State.Entities.Try<FacilityComponent>(entity);
        return facility is not null&&session.Definitions.Facilities.TryGetValue(facility.Definition,out var definition)&&
            definition.Capabilities.Contains(capability,StringComparer.Ordinal);
    }

    public static bool CanUse(SimulationSession session,int actor,int facility)
    {
        var component=session.State.Entities.Try<FacilityComponent>(facility);
        if(component is null||!session.Definitions.Facilities.TryGetValue(component.Definition,out var definition))return false;
        if(definition.Access=="community"||component.Project==0)return true;
        return session.State.Entities.Get<FamilyComponent>(actor).HomeProject==component.Project;
    }

    public static GridPoint? FindSite(SimulationSession session,int actor,FacilityDefinition definition)
    {
        var s=session.State;
        var e=s.Entities;
        var family=e.Get<FamilyComponent>(actor);
        if(family.HomeProject==0||e.Try<ConstructionComponent>(family.HomeProject) is not { Finished:true } home)return null;
        if(e.Store<FacilityComponent>().All.Any(x=>x.Value.Project==family.HomeProject&&x.Value.Definition==definition.Id))return null;
        var center=e.Get<PositionComponent>(family.HomeProject).Tile;
        var actorPosition=e.Get<PositionComponent>(actor).Tile;
        var candidates=new List<GridPoint>();
        if(definition.Placement=="indoor")
        {
            var radius=Math.Max(1,session.Definitions.Buildings[home.Definition].Size/2-1);
            for(var dy=-radius;dy<=radius;dy++)
            for(var dx=-radius;dx<=radius;dx++)
            {
                var p=center+new GridPoint(dx,dy);
                if(!s.Map.Contains(p))continue;
                var tile=s.Map[p];
                if(!s.Map.Walkable(p)||tile.Floor==0||tile.Roof==0||tile.Room==0)continue;
                if(Occupied(session,p))continue;
                if(!session.Pathfinder.CanReach(actorPosition,p,0))continue;
                candidates.Add(p);
            }
        }
        else
        {
            for(var radius=3;radius<=7;radius++)
            for(var dy=-radius;dy<=radius;dy++)
            for(var dx=-radius;dx<=radius;dx++)
            {
                if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                var p=center+new GridPoint(dx,dy);
                if(!s.Map.Contains(p))continue;
                var tile=s.Map[p];
                if(!s.Map.Walkable(p)||tile.Water!=WaterKind.None||tile.Roof!=0||tile.Floor!=0||
                   tile.Moisture<definition.MinMoisture||Occupied(session,p))continue;
                if(!session.Pathfinder.CanReach(actorPosition,p,0))continue;
                candidates.Add(p);
            }
        }
        return candidates.OrderBy(p=>p.Distance(actorPosition)).ThenBy(p=>p.Distance(center))
            .ThenBy(p=>p.Y).ThenBy(p=>p.X).Select(p=>(GridPoint?)p).FirstOrDefault();
    }

    public static bool CanPlace(SimulationSession session,int actor,FacilityDefinition definition,GridPoint position)
    {
        var s=session.State;
        var e=s.Entities;
        var family=e.Get<FamilyComponent>(actor);
        if(family.HomeProject==0||e.Try<ConstructionComponent>(family.HomeProject) is not { Finished:true } home)return false;
        if(!s.Map.Contains(position)||Occupied(session,position))return false;
        var tile=s.Map[position];
        if(!s.Map.Walkable(position)||tile.Water!=WaterKind.None||tile.Moisture<definition.MinMoisture)return false;
        var center=e.Get<PositionComponent>(family.HomeProject).Tile;
        if(definition.Placement=="indoor")
        {
            var radius=Math.Max(1,session.Definitions.Buildings[home.Definition].Size/2-1);
            if(Math.Abs(position.X-center.X)>radius||Math.Abs(position.Y-center.Y)>radius||
               tile.Floor==0||tile.Roof==0||tile.Room==0)return false;
        }
        else if(tile.Roof!=0||tile.Floor!=0||position.Distance(center)>14)return false;
        return session.Pathfinder.CanReach(e.Get<PositionComponent>(actor).Tile,position,0);
    }

    public static int Build(SimulationSession session,int actor,string definitionId,GridPoint position)
    {
        if(!session.Definitions.Facilities.TryGetValue(definitionId,out var definition)||
           !ActionRules.CanWork(session.State,actor)||!CanPlace(session,actor,definition,position))return 0;
        if(!session.State.Entities.Get<KnowledgeComponent>(actor).Facts.Contains(definition.Knowledge)||
           definition.Inputs.Any(x=>session.Inventory.Count(actor,x.Key)<x.Value))return 0;
        foreach(var input in definition.Inputs)
            if(!session.Inventory.Consume(actor,input.Key,input.Value))throw new InvalidOperationException("Facility build preflight failed.");

        var e=session.State.Entities;
        var id=e.Create();
        var project=e.Get<FamilyComponent>(actor).HomeProject;
        e.Set(id,new PositionComponent { Tile=position });
        e.Set(id,new FacilityComponent { Definition=definition.Id,Project=project,Builder=actor });
        e.Set(id,new OwnershipComponent { Owner=actor });
        if(definition.StorageMass>0&&definition.StorageVolume>0)
        {
            e.Set(id,new StorageComponent { Name=definition.Name,Project=project });
            e.Set(id,new InventoryComponent { MaxMass=definition.StorageMass,MaxVolume=definition.StorageVolume });
        }
        session.Spatial.Add(id,position);
        session.Events.Publish(new SkillUsedEvent(actor,definition.Skill,definition.WorkMinutes*.08f));
        session.State.Log(e.Get<IdentityComponent>(actor).FullName+" изготовил: "+definition.Name+".");
        return id;
    }

    private static bool Occupied(SimulationSession session,GridPoint p)=>session.Spatial.Query(p,0).Any(id=>
        session.State.Entities.Get<PositionComponent>(id).Tile==p&&
        (session.State.Entities.Has<FacilityComponent>(id)||session.State.Entities.Has<StorageComponent>(id)));
}
