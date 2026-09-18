namespace LivingWorld.Simulation;
public sealed class ResourcePass : IGenerationPass
{
    public string Name => "природные ресурсы";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var random=state.Random.Stream("resources");
        for (var i=0; i<state.Map.Tiles.Length; i++)
        {
            var p=state.Map.Point(i);
            var t=state.Map.Tiles[i];
            if (!state.Map.Walkable(p))continue;
            if (random.Chance(t.Biome==Biome.Mountain?.19:.014))
            {
                var id=state.Entities.Create();
                state.Entities.Set(id, new PositionComponent
                {
                    Tile=p
                });
                state.Entities.Set(id, new ResourceComponent
                {
                    Product=t.Ore>.64f?"iron_ore":"granite", Units=random.Range(7, 23)
                });
            }
            if (random.Chance(.035)) SpawnItem(state, definitions, random.Chance(.6)?"log":"granite", p);
        }
    }
    public static int SpawnItem(WorldState state, DefinitionCatalog defs, string definition, GridPoint p, int owner=0)
    {
        var id=state.Entities.Create();
        var d=defs.Items[definition];
        state.Entities.Set(id, new ItemComponent
        {
            Definition=definition, Durability=d.Durability, CreatedTick=state.Clock.Tick
        });
        state.Entities.Set(id, new OwnershipComponent
        {
            Owner=owner
        });
        state.Entities.Set(id, new PositionComponent
        {
            Tile=p
        });
        return id;
    }
}
