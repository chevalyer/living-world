namespace LivingWorld.Simulation;
public static class ContextBuilder
{
    public static PlanningContext Create(SimulationSession session, int actor, bool findBuildSite=true, bool findFarmSite=false)
    {
        var s=session.State;
        var e=s.Entities;
        var p=e.Get<PositionComponent>(actor).Tile;
        var decision=e.Get<DecisionComponent>(actor);
        var knowledge=e.Get<KnowledgeComponent>(actor);
        var facilitySites=new Dictionary<string,GridPoint>(StringComparer.Ordinal);
        if(findBuildSite&&ActionRules.CanWork(s,actor))
            foreach(var definition in session.Definitions.Facilities.Values)
                if(FacilityService.FindSite(session,actor,definition) is { } site)facilitySites[definition.Id]=site;
        var known=e.Get<MemoryComponent>(actor).Observations
            .Where(o=>session.Pathfinder.CanReach(p,o.Kind=="project"&&o.WorkPosition.HasValue?o.WorkPosition.Value:o.Position,
                o.Kind=="shelter"?0:1))
            .ToList();
        var farmSite=findFarmSite&&knowledge.Facts.Contains("farming")
            ?FarmService.FindVisibleSite(session,actor):null;
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
            Known=known,
            Inventory=e.Get<InventoryComponent>(actor).Items.Select(id=>(id, e.Get<ItemComponent>(id))).ToList(),
            Worn=e.Get<EquipmentComponent>(actor).Items,
            Knowledge=knowledge,
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
            FarmSite=farmSite,
            FacilitySites=facilitySites,
            SocialReady=s.Clock.Tick-decision.LastSocialTick>120
        };
    }

}
