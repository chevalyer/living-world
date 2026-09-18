namespace LivingWorld.Simulation;
public static class ContextBuilder
{
    public static PlanningContext Create(SimulationSession session, int actor, bool findBuildSite=true)
    {
        var s=session.State;
        var e=s.Entities;
        var p=e.Get<PositionComponent>(actor).Tile;
        var decision=e.Get<DecisionComponent>(actor);
        return new PlanningContext
        {
            Actor=actor,
            MaxMass=e.Get<InventoryComponent>(actor).MaxMass,
            MaxVolume=e.Get<InventoryComponent>(actor).MaxVolume,
            Position=p,
            Tick=s.Clock.Tick,
            Definitions=session.Definitions,
            Age=s.Clock.Age(e.Get<IdentityComponent>(actor).BirthDate),
            CanWork=ActionRules.CanWork(s, actor),
            Known=e.Get<MemoryComponent>(actor).Observations,
            Inventory=e.Get<InventoryComponent>(actor).Items.Select(id=>(id, e.Get<ItemComponent>(id))).ToList(),
            Worn=e.Get<EquipmentComponent>(actor).Items,
            Knowledge=e.Get<KnowledgeComponent>(actor),
            Needs=e.Get<NeedsComponent>(actor),
            Personality=e.Get<PersonalityComponent>(actor),
            Family=e.Get<FamilyComponent>(actor),
            Relationships=e.Get<RelationshipComponent>(actor),
            Skills=e.Get<SkillsComponent>(actor),
            Air=EnvironmentQueries.Local(s,p)+EnvironmentQueries.FireHeat(session,p),
            OutdoorAir=EnvironmentQueries.Air(s, p),
            Temperature=e.Get<ThermalComponent>(actor).Temperature,
            Insulation=ClothingPhysics.Total(s, session.Definitions, actor),
            Sheltered=EnvironmentQueries.Sheltered(s, p),
            Room=s.Map[p].Room,
            MapWidth=s.Map.Width,
            BuildSite=findBuildSite?BuildingService.FindVisibleSite(session, actor):null,
            SowSite=findBuildSite?FindSowSite(session,actor):null,
            SocialReady=s.Clock.Tick-decision.LastSocialTick>120
        };
    }

    private static GridPoint? FindSowSite(SimulationSession session, int actor)
    {
        var s=session.State;
        var e=s.Entities;
        if (!ActionRules.CanWork(s,actor)||!e.Get<KnowledgeComponent>(actor).Facts.Contains("farming"))return null;
        var center=e.Get<PositionComponent>(actor).Tile;
        var minimum=session.Definitions.Plants.Values.Where(x=>x.Kind=="crop").Select(x=>x.MinTemperature).DefaultIfEmpty(float.MaxValue).Min();
        var candidates=new List<GridPoint>();
        for(var radius=0;radius<=5;radius++)
        for(var dy=-radius;dy<=radius;dy++)
        for(var dx=-radius;dx<=radius;dx++)
        {
            if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
            var p=center+new GridPoint(dx,dy);
            if(!s.Map.Contains(p)||center.Distance(p)>7||!PerceptionSystem.LineOfSight(s.Map,center,p))continue;
            var tile=s.Map[p];
            if(!s.Map.Walkable(p)||tile.Roof>0||tile.Floor>0||tile.Water!=WaterKind.None||EnvironmentQueries.Air(s,p)<minimum)continue;
            if(session.Spatial.Query(p,0).Any(id=>e.Has<PlantComponent>(id)&&e.Get<PositionComponent>(id).Tile==p))continue;
            candidates.Add(p);
        }
        foreach(var candidate in candidates
            .OrderByDescending(p=>s.Map[p].Fertility)
            .ThenBy(p=>p.Distance(center))
            .ThenBy(p=>p.Y)
            .ThenBy(p=>p.X))
        {
            if(center.Distance(candidate)<=1||session.Pathfinder.Find(center,candidate,1,256) is not null)return candidate;
        }
        return null;
    }
}
