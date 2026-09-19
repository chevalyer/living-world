namespace LivingWorld.Simulation;
public static class ContextBuilder
{
    public static PlanningContext Create(SimulationSession session, int actor, bool findBuildSite=true, bool findFarmSite=true)
    {
        var s=session.State;
        var e=s.Entities;
        var p=e.Get<PositionComponent>(actor).Tile;
        var decision=e.Get<DecisionComponent>(actor);
        var knowledge=e.Get<KnowledgeComponent>(actor);
        var known=e.Get<MemoryComponent>(actor).Observations;
        var farmSite=findFarmSite&&knowledge.Facts.Contains("farming")&&!known.Any(o=>o.Kind=="farm_cell")
            ?FarmService.FindVisibleSite(session,actor):null;
        return new PlanningContext
        {
            Actor=actor, MaxMass=e.Get<InventoryComponent>(actor).MaxMass, MaxVolume=e.Get<InventoryComponent>(actor).MaxVolume, Position=p, Tick=s.Clock.Tick, Definitions=session.Definitions, Age=s.Clock.Age(e.Get<IdentityComponent>(actor).BirthDate), CanWork=ActionRules.CanWork(s, actor), Known=known, Inventory=e.Get<InventoryComponent>(actor).Items.Select(id=>(id, e.Get<ItemComponent>(id))).ToList(), Worn=e.Get<EquipmentComponent>(actor).Items, Knowledge=knowledge, Needs=e.Get<NeedsComponent>(actor), Personality=e.Get<PersonalityComponent>(actor), Family=e.Get<FamilyComponent>(actor), Relationships=e.Get<RelationshipComponent>(actor), Skills=e.Get<SkillsComponent>(actor), Air=EnvironmentQueries.Air(s, p), Temperature=e.Get<ThermalComponent>(actor).Temperature, Insulation=ClothingPhysics.Total(s, session.Definitions, actor), Sheltered=EnvironmentQueries.Sheltered(s, p), BuildSite=findBuildSite?BuildingService.FindVisibleSite(session, actor):null, FarmSite=farmSite, SocialReady=s.Clock.Tick-decision.LastSocialTick>120
        };
    }
}
