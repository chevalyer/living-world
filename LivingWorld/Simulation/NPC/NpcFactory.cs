namespace LivingWorld.Simulation;
public sealed class NpcFactory(DefinitionCatalog definitions)
{
    public int SpawnAdult(WorldState state, GridPoint p)
    {
        var random=state.Random.Stream("npc");
        var sex=random.Chance(.5)?"male":"female";
        var id=Spawn(state, p, sex, state.Clock.Now.AddYears(-random.Range(19, 43)).AddDays(-random.Range(0, 300)));
        var inventory=state.Entities.Get<InventoryComponent>(id);
        var equipment=state.Entities.Get<EquipmentComponent>(id);
        void Give(string definition, bool wear=false)
        {
            var item=ResourcePass.SpawnItem(state, definitions, definition, p, id);
            state.Entities.Get<ItemComponent>(item).Holder=id;
            state.Entities.Store<PositionComponent>().Remove(item);
            inventory.Items.Add(item);
            if (wear)equipment.Items.Add(item);
        }
        Give("linen_shirt", true);
        Give("linen_trousers", true);
        Give("boots", true);
        Give(random.Chance(.5)?"leather_jacket":"wool_coat");
        Give("stone_axe");
        Give("stone_pick");
        for (var i=0; i<4; i++)Give("grain");
        var seeds=definitions.Plants.Values
            .Where(x=>x.Kind=="crop"&&x.Seed.Length>0&&definitions.Items.ContainsKey(x.Seed))
            .Select(x=>x.Seed).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        for (var i=0;i<2&&seeds.Length>0;i++)Give(seeds[random.Range(0,seeds.Length)]);
        state.Entities.Get<SkillsComponent>(id).Experience["building"]=random.Range(20f, 90f);
        return id;
    }
    public int Spawn(WorldState state, GridPoint p, string sex, DateTime birth)
    {
        var r=state.Random.Stream("npc");
        var names=new NameGenerator(definitions.Names).Generate(state.Random.Stream("names"), sex);
        var e=state.Entities;
        var id=e.Create();
        e.Set(id, new IdentityComponent
        {
            FirstName=names.First, Surname=names.Last, Sex=sex, BirthDate=birth, LifespanYears=r.Range(72, 98), Appearance=r.Range(0, 8)
        });
        e.Set(id, new PositionComponent
        {
            Tile=p
        });
        e.Set(id, new BodyComponent
        {
            Strength=r.Range(.45f, 1f), Mass=r.Range(52f, 90f)
        });
        e.Set(id, new HealthComponent());
        e.Set(id, new NeedsComponent
        {
            Hunger=r.Range(.10f, .35f), Thirst=r.Range(.1f, .25f), Fatigue=r.Range(.05f, .25f)
        });
        e.Set(id, new ThermalComponent());
        e.Set(id, new InventoryComponent());
        e.Set(id, new EquipmentComponent());
        e.Set(id, new MovementComponent
        {
            Previous=p
        });
        e.Set(id, new DecisionComponent());
        e.Set(id, new MemoryComponent());
        e.Set(id, new SkillsComponent());
        e.Set(id, new RelationshipComponent());
        e.Set(id, new FamilyComponent());
        e.Set(id, new PersonalityComponent
        {
            Sociability=r.Range(.2f, .9f), Generosity=r.Range(.2f, .95f), Caution=r.Range(.2f, .9f), Curiosity=r.Range(.3f, .9f), Aggression=r.Range(.05f, .65f), FamilyDesire=r.Range(.25f, .95f)
        });
        var knowledge=new KnowledgeComponent();
        if (state.Clock.Age(birth)>=18)
        {
            knowledge.Facts.UnionWith(["forage", "crafting", "building", "fire", "farming"]);
            if (r.Chance(.5))knowledge.Facts.Add("tailoring");
            if (r.Chance(.2))knowledge.Facts.Add("smithing");
            knowledge.EdiblePlants.UnionWith(definitions.Plants.Values.Where(x=>definitions.Items[x.Product].Calories>0 && definitions.Items[x.Product].Toxicity==0).Select(x=>x.Id));
        }
        e.Set(id, knowledge);
        return id;
    }
}
