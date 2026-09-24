namespace LivingWorld.Infrastructure;

public static class SaveValidator
{
    public static void Validate(WorldState state, DefinitionCatalog definitions)
    {
        var e = state.Entities;
        foreach (var (id, position) in e.Store<PositionComponent>().All)
            if (!state.Map.Contains(position.Tile)) throw new InvalidDataException("Entity is outside the map: " + id);
        foreach (var (id, inventory) in e.Store<InventoryComponent>().All)
        {
            if (inventory.Items.Count != inventory.Items.Distinct().Count()) throw new InvalidDataException("Duplicate inventory item.");
            foreach (var itemId in inventory.Items)
                if (e.Try<ItemComponent>(itemId) is not { } item || item.Holder != id)
                    throw new InvalidDataException("Inventory and holder disagree.");
        }
        foreach (var (id, item) in e.Store<ItemComponent>().All)
        {
            if (!definitions.Items.ContainsKey(item.Definition)) throw new InvalidDataException("Unknown item definition.");
            if (item.Holder == 0 && !e.Has<PositionComponent>(id)) throw new InvalidDataException("Ground item has no position.");
            if (item.Holder != 0 && (e.Try<InventoryComponent>(item.Holder) is not { } inventory || !inventory.Items.Contains(id) || e.Has<PositionComponent>(id)))
                throw new InvalidDataException("Held item has invalid location.");
        }
        foreach (var (_, plant) in e.Store<PlantComponent>().All)
            if (!definitions.Plants.ContainsKey(plant.Definition)) throw new InvalidDataException("Unknown plant definition.");
        foreach (var (id, plot) in e.Store<FarmPlotComponent>().All)
        {
            if (plot.Cells.Count==0||plot.Cells.Count!=plot.Cells.Distinct().Count())throw new InvalidDataException("Invalid farm plot.");
            foreach (var cell in plot.Cells)
                if (!state.Map.Contains(cell)||state.Map[cell].FarmPlot!=id)throw new InvalidDataException("Farm plot and map disagree.");
            var physical=e.Store<FarmCellComponent>().All
                .Where(x=>x.Value.Plot==id)
                .Select(x=>e.Try<PositionComponent>(x.Key)?.Tile)
                .Where(x=>x.HasValue)
                .Select(x=>x!.Value)
                .ToArray();
            if (physical.Length!=plot.Cells.Count||!physical.ToHashSet().SetEquals(plot.Cells))
                throw new InvalidDataException("Farm plot cells are incomplete.");
        }
        foreach (var (id, cell) in e.Store<FarmCellComponent>().All)
        {
            var position=e.Try<PositionComponent>(id)?.Tile;
            if (position is null||e.Try<FarmPlotComponent>(cell.Plot) is not { } plot||
                !plot.Cells.Contains(position.Value)||state.Map[position.Value].FarmPlot!=cell.Plot)
                throw new InvalidDataException("Invalid farm cell.");
        }
        for (var i=0;i<state.Map.Tiles.Length;i++)
        {
            var plot=state.Map.Tiles[i].FarmPlot;
            if (plot!=0&&(e.Try<FarmPlotComponent>(plot) is not { } farm||!farm.Cells.Contains(state.Map.Point(i))))
                throw new InvalidDataException("Map references an invalid farm plot.");
        }
        foreach(var (id,facility) in e.Store<FacilityComponent>().All)
        {
            if(!definitions.Facilities.ContainsKey(facility.Definition))throw new InvalidDataException("Unknown facility definition.");
            if(!e.Has<PositionComponent>(id))throw new InvalidDataException("Facility has no position.");
            if(facility.Project!=0&&e.Try<ConstructionComponent>(facility.Project) is null)
                throw new InvalidDataException("Facility references missing home project.");
        }
        foreach(var (_,storage) in e.Store<StorageComponent>().All)
            if(storage.Project!=0&&e.Try<ConstructionComponent>(storage.Project) is null)
                throw new InvalidDataException("Storage references missing home project.");
        foreach (var (_, equipment) in e.Store<EquipmentComponent>().All)
            foreach (var item in equipment.Items)
                if (!e.Has<ItemComponent>(item)) throw new InvalidDataException("Missing equipped item.");
        Type[] required = [typeof(BodyComponent), typeof(HealthComponent), typeof(NeedsComponent), typeof(ThermalComponent),
            typeof(InventoryComponent), typeof(EquipmentComponent), typeof(MemoryComponent), typeof(KnowledgeComponent),
            typeof(SkillsComponent), typeof(PersonalityComponent), typeof(FamilyComponent), typeof(RelationshipComponent),
            typeof(MovementComponent), typeof(DecisionComponent), typeof(PositionComponent)];
        foreach (var id in e.Store<IdentityComponent>().Ids())
            foreach (var component in required)
                if (!e.Store(component).Has(id)) throw new InvalidDataException("NPC is missing " + component.Name);
        foreach (var (_, family) in e.Store<FamilyComponent>().All)
            foreach (var relative in family.Children.Concat(new[] { family.Mother, family.Father, family.Partner, family.PregnancyFather }))
                if (relative != 0 && !e.Has<IdentityComponent>(relative)) throw new InvalidDataException("Missing family member.");
    }
}
