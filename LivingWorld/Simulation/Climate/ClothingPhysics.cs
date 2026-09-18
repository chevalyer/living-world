namespace LivingWorld.Simulation;
public static class ClothingPhysics
{
    public static float Insulation(ItemComponent item, ItemDefinition definition, MaterialDefinition material)
    {
        var condition=Math.Clamp(item.Durability/definition.Durability, 0, 1);
        return material.Insulation*definition.Coverage*definition.Thickness*item.Quality*(.2f+.8f*condition)*(1-.7f*item.Wetness);
    }
    public static float Total(WorldState state, DefinitionCatalog defs, int actor)
    {
        var outfit=state.Entities.Try<EquipmentComponent>(actor);
        if (outfit is null)return 0;
        return outfit.Items.Sum(id=>
        {
            var item=state.Entities.Get<ItemComponent>(id); var d=defs.Items[item.Definition]; return Insulation(item, d, defs.Materials[d.Material]);
        });
    }
}
