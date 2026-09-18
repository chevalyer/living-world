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
            Actor=actor, MaxMass=e.Get<InventoryComponent>(actor).MaxMass, MaxVolume=e.Get<InventoryComponent>(actor).MaxVolume, Position=p, Tick=s.Clock.Tick, Definitions=session.Definitions, Age=s.Clock.Age(e.Get<IdentityComponent>(actor).BirthDate), CanWork=ActionRules.CanWork(s, actor), Known=e.Get<MemoryComponent>(actor).Observations, Inventory=e.Get<InventoryComponent>(actor).Items.Select(id=>(id, e.Get<ItemComponent>(id))).ToList(), Worn=e.Get<EquipmentComponent>(actor).Items, Knowledge=e.Get<KnowledgeComponent>(actor), Needs=e.Get<NeedsComponent>(actor), Personality=e.Get<PersonalityComponent>(actor), Family=e.Get<FamilyComponent>(actor), Relationships=e.Get<RelationshipComponent>(actor), Skills=e.Get<SkillsComponent>(actor), Air=EnvironmentQueries.Air(s, p), Temperature=e.Get<ThermalComponent>(actor).Temperature, Insulation=ClothingPhysics.Total(s, session.Definitions, actor), Sheltered=EnvironmentQueries.Sheltered(s, p), BuildSite=findBuildSite?BuildingService.FindVisibleSite(session, actor):null, SocialReady=s.Clock.Tick-decision.LastSocialTick>120
        };
    }
}
