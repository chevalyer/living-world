using System.Text.Json.Nodes;
using LivingWorld.Definitions;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
namespace LivingWorld.Tests;
public static class TestSuite
{
    private static readonly DefinitionCatalog D=DefinitionLoader.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
    private static int _passed, _failed;
    public static int Run()
    {
        Test("definition references", ()=>D.Validate());
        Test("world generator honors requested population", ()=>
        {
            var state=new WorldGenerator().Generate(D,1847,64,99);
            Equal(99,state.Entities.Store<IdentityComponent>().Count);
        });
        Test("new world population input uses typed text", ()=>
        {
            Equal(99,LivingWorld.Presentation.WorldCreationInput.Population("99",14));
            Equal(500,LivingWorld.Presentation.WorldCreationInput.Population("999",14));
            Equal(14,LivingWorld.Presentation.WorldCreationInput.Population("oops",14));
        });
        Test("reachability rejects disconnected remembered targets", ()=>
        {
            var(s,id)=Fixture();
            for(var y=0;y<20;y++)s.State.Map[new(7,y)].Wall=1;
            s.State.Map.NavigationRevision++;
            var plant=Plant(s,new(9,5),"raspberry_bush",3);
            s.State.Entities.Get<MemoryComponent>(id).Observations.Add(new()
            {
                Kind="plant",Entity=plant,Definition="raspberry_bush",Product="raspberry",
                Position=new(9,5),Quantity=3
            });
            Assert(!s.Pathfinder.CanReach(new(5,5),new(9,5),1),"disconnected target reported reachable");
            Assert(!ContextBuilder.Create(s,id,false).Known.Any(o=>o.Entity==plant),"unreachable memory entered planner context");
        });
        Test("same seed produces same initial state", ()=>
        {
            var a=new SimulationSession(new WorldGenerator().Generate(D, 1847, 48, 2), D); var b=new SimulationSession(new WorldGenerator().Generate(D, 1847, 48, 2), D); Equal(new SaveService().Hash(a), new SaveService().Hash(b));
        });
        Test("independent random streams", ()=>
        {
            var a=new RandomService(8); var b=new RandomService(8); a.Stream("flowers").NextUInt64(); Equal(a.Stream("terrain").NextUInt64(), b.Stream("terrain").NextUInt64());
        });
        Test("random sequence resumes", ()=>
        {
            var a=new RandomService(8); a.Stream("a").NextUInt64(); var b=new RandomService(8); b.Restore(a.Capture()); Equal(a.Stream("a").NextUInt64(), b.Stream("a").NextUInt64());
        });
        Test("drainage is acyclic and non-increasing", ()=>
        {
            var state=new WorldGenerator().Generate(D, 19, 48, 1); for (var i=0; i<state.Map.Tiles.Length; i++)
            {
                var visited=new HashSet<int>(); var current=i; while (state.Map.Tiles[current].Downstream>=0)
                {
                    Assert(visited.Add(current), "drainage cycle"); var next=state.Map.Tiles[current].Downstream; Assert(state.Map.Tiles[current].DrainageHeight+1e-6>=state.Map.Tiles[next].DrainageHeight, "water flows uphill"); current=next;
                }
            }
        });
        Test("river and lake generation", ()=>
        {
            var s=new WorldGenerator().Generate(D, 1847, 96, 1); Assert(s.Map.Tiles.Any(t=>t.Water==WaterKind.Ocean), "no sea"); Assert(s.Map.Tiles.Any(t=>t.Water is WaterKind.River or WaterKind.Lake), "no freshwater");
        });
        Test("names contain no yo", ()=>
        {
            var generator=new NameGenerator(D.Names); var r=new DeterministicRandom(4); for (var i=0; i<200; i++)
            {
                var name=generator.Generate(r, i%2==0?"male":"female"); Assert(!(name.First+name.Last).Contains('ё')&&!(name.First+name.Last).Contains('Ё'), "name restriction");
            }
        });
        Test("age uses the birthday", ()=>
        {
            var clock=new SimulationClock
            {
                Epoch=new DateTime(1372, 4, 1)
            }; Equal(17, clock.Age(new DateTime(1354, 4, 2))); clock.Tick=1440; Equal(18, clock.Age(new DateTime(1354, 4, 2)));
        });
        Test("missing food cannot be eaten", ()=>
        {
            var(s, id)=Fixture(); var before=s.State.Entities.Get<NeedsComponent>(id).Hunger; Assert(!new EatAction().Execute(s, id, new()
            {
                Argument="grain"
            }), "missing food accepted"); Equal(before, s.State.Entities.Get<NeedsComponent>(id).Hunger);
        });
        Test("eating destroys exactly one physical item", ()=>
        {
            var(s, id)=Fixture(); Give(s, id, "grain"); Give(s, id, "grain"); Assert(new EatAction().Execute(s, id, new()
            {
                Argument="grain"
            }), "eat failed"); Equal(1, s.Inventory.Count(id, "grain"));
        });
        Test("inventory capacity is enforced", ()=>
        {
            var(s, id)=Fixture(); s.State.Entities.Get<InventoryComponent>(id).MaxMass=.1f; var item=s.Inventory.Spawn("log", new(5, 5)); Assert(!s.Inventory.PickUp(id, item), "overweight pickup accepted"); Equal(0, s.State.Entities.Get<ItemComponent>(item).Holder);
        });
        Test("ownership and holder are different", ()=>
        {
            var(s, id)=Fixture(); var item=s.Inventory.Spawn("grain", new(5, 5), 999); Assert(!s.Inventory.PickUp(id, item), "stole protected item"); Equal(999, s.State.Entities.Get<OwnershipComponent>(item).Owner); Equal(0, s.State.Entities.Get<ItemComponent>(item).Holder);
        });
        Test("wet clothing insulates less", ()=>
        {
            var d=D.Items["leather_jacket"]; var item=new ItemComponent
            {
                Durability=d.Durability
            }; var dry=ClothingPhysics.Insulation(item, d, D.Materials[d.Material]); item.Wetness=1; Assert(ClothingPhysics.Insulation(item, d, D.Materials[d.Material])<dry, "wetness ignored");
        });
        Test("equipment replaces overlapping slots", ()=>
        {
            var(s, id)=Fixture(); var a=Give(s, id, "leather_jacket"); var b=Give(s, id, "wool_coat"); s.Inventory.Wear(id, a); s.Inventory.Wear(id, b); Equal(1, s.State.Entities.Get<EquipmentComponent>(id).Items.Count); Equal(b, s.State.Entities.Get<EquipmentComponent>(id).Items[0]);
        });
        Test("broken tools have no capability", ()=>
        {
            var(s, id)=Fixture(); var axe=Give(s, id, "stone_axe"); s.State.Entities.Get<ItemComponent>(axe).Durability=0; Equal(0f, s.Inventory.Tool(id, "chop"));
        });
        Test("child cannot execute adult work", ()=>
        {
            var(s, id)=Fixture(8); foreach (var action in s.Actions.All.Where(x=>x.RequiresWork))Assert(!action.CanExecute(s, id, new(), out _), action.Id+" allows child work");
        });
        Test("zero strength blocks construction", ()=>
        {
            var(s, id)=Fixture(); s.State.Entities.Get<BodyComponent>(id).Strength=0; Assert(!new BuildAction().CanExecute(s, id, new(), out _), "zero strength ignored");
        });
        Test("plants stop growing in cold", ()=>Equal(0f, PlantSystem.GrowthRate(D.Plants["raspberry_bush"], -10, .8f, 1)));
        Test("plant species fixes harvest product", ()=>
        {
            var(s, id)=Fixture(); var bush=Plant(s, new(6, 5), "blueberry_bush", 6); Assert(new HarvestAction().Execute(s, id, new()
            {
                Target=bush, Position=new(6, 5)
            }), "harvest failed"); Assert(s.State.Entities.Store<ItemComponent>().All.All(x=>x.Value.Definition=="blueberry"), "mixed fruit on bush");
        });
        Test("harvest cannot duplicate depleted yield", ()=>
        {
            var(s, id)=Fixture(); var bush=Plant(s, new(6, 5), "raspberry_bush", 3); var action=new HarvestAction(); var step=new ActionStep
            {
                Target=bush, Position=new(6, 5)
            }; Assert(action.Execute(s, id, step), "first harvest"); Assert(!action.Execute(s, id, step), "duplicate harvest"); Equal(3, s.State.Entities.Store<ItemComponent>().Count);
        });
        Test("resource reservation is exclusive and expires", ()=>
        {
            var r=new ReservationService(); Assert(r.Claim(4, 1, 0), "claim"); Assert(!r.Claim(4, 2, 1), "double claim"); Assert(r.Claim(4, 2, 121), "expired claim retained");
        });
        Test("planner cannot find unknown food", ()=>
        {
            var(s, id)=Fixture(); Plant(s, new(19, 19), "raspberry_bush", 9); var c=ContextBuilder.Create(s, id); var options=s.Actions.All.SelectMany(a=>a.Options(c)).ToList(); Assert(s.Planner.Find(c, options, new("fed", "test", 10)) is null, "omniscient planner");
        });
        Test("planner composes harvest pickup eat", ()=>
        {
            var(s, id)=Fixture(); var bush=Plant(s, new(9, 5), "raspberry_bush", 3); s.State.Entities.Get<MemoryComponent>(id).Observations.Add(new()
            {
                Kind="plant", Entity=bush, Definition="raspberry_bush", Product="raspberry", Position=new(9, 5), Quantity=3
            }); var c=ContextBuilder.Create(s, id); var plan=s.Planner.Find(c, s.Actions.All.SelectMany(a=>a.Options(c)).ToList(), new("fed", "test", 10)); Assert(plan is not null, "no plan"); Assert(plan!.Steps.Select(x=>x.Action).SequenceEqual(new[]
            {
                "move", "harvest", "pickup", "eat"
            }), "incorrect chain");
        });
        Test("perception does not see through walls", ()=>
        {
            var(s, id)=Fixture(); s.State.Map[new(6, 5)].Wall=99; Plant(s, new(7, 5), "raspberry_bush", 3); PerceptionSystem.Observe(s, id, s.State.Entities.Get<MemoryComponent>(id)); Assert(!s.State.Entities.Get<MemoryComponent>(id).Observations.Any(o=>o.Position==new GridPoint(7, 5)), "wall ignored");
        });
        Test("pathfinding respects blocked tiles", ()=>
        {
            var(s, id)=Fixture(); for (var y=0; y<20; y++)s.State.Map[new(7, y)].Wall=1; Assert(s.Pathfinder.Find(new(5, 5), new(9, 5), 0) is null, "path crosses wall");
        });
        Test("path search obeys node budget", ()=>
        {
            var(s, id)=Fixture(); Assert(s.Pathfinder.Find(new(1, 1), new(18, 18), 0, 1) is null, "budget ignored");
        });
        Test("social events change directed relationships", ()=>
        {
            var(s, id)=Fixture(); var other=Adult(s, new(6, 5)); s.Events.Publish(new SocialEvent(id, other, "gift", .1f)); s.Events.Flush(); Assert(RelationshipSystem.Get(s.State, other, id).Trust>.3f, "gift ignored"); Assert(RelationshipSystem.Get(s.State, id, other).Trust!=RelationshipSystem.Get(s.State, other, id).Trust, "relationships forced symmetric");
        });
        Test("teaching transfers learned knowledge", ()=>
        {
            var(s, id)=Fixture(); var child=Adult(s, new(6, 5)); s.State.Entities.Get<SkillsComponent>(id).Experience["crafting"]=100; s.State.Entities.Get<KnowledgeComponent>(child).Facts.Clear(); Assert(new TeachAction().Execute(s, id, new()
            {
                Target=child, Argument="crafting", Duration=30
            }), "teaching failed"); s.Events.Flush(); Assert(s.State.Entities.Get<SkillsComponent>(child).Experience["crafting"]>0, "no experience"); Assert(s.State.Entities.Get<KnowledgeComponent>(child).Facts.Count>0, "no knowledge");
        });
        Test("minors cannot form reproductive partnerships", ()=>
        {
            var(s, id)=Fixture(12); var other=Adult(s, new(6, 5)); Assert(!FamilyRules.CanPartner(s.State, id, other), "minor partnership"); Assert(!FamilyRules.CanConceive(s.State, id, other), "minor pregnancy");
        });
        Test("relatives cannot partner", ()=>
        {
            var(s, id)=Fixture(); var other=Adult(s, new(6, 5)); s.State.Entities.Get<FamilyComponent>(other).Mother=id; Assert(FamilyRules.Related(s.State, id, other), "ancestry lost"); Assert(!FamilyRules.CanPartner(s.State, id, other), "relative partnership");
        });
        Test("pregnancy produces linked child", ()=>
        {
            var(s, id)=Fixture(); s.State.Entities.Get<IdentityComponent>(id).Sex="female"; var father=Adult(s, new(6, 5)); var family=s.State.Entities.Get<FamilyComponent>(id); family.PregnancyFather=father; family.PregnancyDueTick=0; new FamilySystem().Update(s); Equal(1, family.Children.Count); var child=family.Children[0]; Equal(id, s.State.Entities.Get<FamilyComponent>(child).Mother); Equal(father, s.State.Entities.Get<FamilyComponent>(child).Father); Equal(0, s.State.Clock.Age(s.State.Entities.Get<IdentityComponent>(child).BirthDate)); Assert(!ActionRules.CanWork(s.State, child), "newborn can work");
        });
        Test("closed roofed walls form a room", ()=>
        {
            var(s, id)=Fixture(); var project=BuildHome(s, id); new RoomSystem().Update(s); Assert(s.State.Rooms.Count>0, "no room"); Assert(s.State.Entities.Get<ConstructionComponent>(project).Finished, "unfinished"); Assert(s.State.Map.Walkable(new(5, 7)), "door blocks movement");
        });
        Test("opening wall invalidates room", ()=>
        {
            var(s, id)=Fixture(); BuildHome(s, id); new RoomSystem().Update(s); s.State.Map[new(3, 5)].Wall=0; s.RoomsDirty=true; new RoomSystem().Update(s); Equal(0, s.State.Rooms.Count);
        });
        Test("communal stock retains physical ownership", ()=>
        {
            var(s, id)=Fixture(); var storage=StorageService.Create(s, new(5, 5), 0); var item=Give(s, id, "grain"); Assert(StorageService.Deposit(s, id, storage, item), "deposit failed"); Equal(storage, s.State.Entities.Get<ItemComponent>(item).Holder); Equal(0, s.State.Entities.Get<OwnershipComponent>(item).Owner); Assert(StorageService.Take(s, id, storage, "grain"), "take failed"); Equal(id, s.State.Entities.Get<ItemComponent>(item).Holder);
        });
        Test("settlement analyzer groups nearby homes deterministically", ()=>
        {
            var(s, first)=Fixture();
            var homeA=FinishedProject(s, new(4, 4));
            s.State.Entities.Get<FamilyComponent>(first).HomeProject=homeA;
            var second=Adult(s, new(10, 4));
            var homeB=FinishedProject(s, new(10, 4));
            s.State.Entities.Get<FamilyComponent>(second).HomeProject=homeB;
            var a=SettlementAnalyzer.DescribeAll(s);
            var b=SettlementAnalyzer.DescribeAll(s);
            Equal(1, a.Count);
            Equal(a[0].Name, b[0].Name);
            Equal(2, a[0].Homes);
            Equal(2, a[0].Members);
            Assert(a[0].MinX<=4&&a[0].MaxX>=10, "settlement bounds exclude homes");
        });
        Test("settlement analyzer separates distant homes", ()=>
        {
            var(s, first)=Fixture();
            var homeA=FinishedProject(s, new(2, 2));
            s.State.Entities.Get<FamilyComponent>(first).HomeProject=homeA;
            var second=Adult(s, new(18, 18));
            var homeB=FinishedProject(s, new(18, 18));
            s.State.Entities.Get<FamilyComponent>(second).HomeProject=homeB;
            var settlements=SettlementAnalyzer.DescribeAll(s);
            Equal(2, settlements.Count);
            Assert(settlements.Select(x=>x.Name).Distinct().Count()==2, "settlement names collided");
            Assert(settlements.All(x=>x.Members==1), "resident assigned to multiple settlements");
        });
        Test("unhomed resident belongs to only one nearby settlement", ()=>
        {
            var(s, first)=Fixture();
            var homeA=FinishedProject(s,new(1,1));
            s.State.Entities.Get<FamilyComponent>(first).HomeProject=homeA;
            var second=Adult(s,new(19,19));
            var homeB=FinishedProject(s,new(19,19));
            s.State.Entities.Get<FamilyComponent>(second).HomeProject=homeB;
            _=Adult(s,new(10,10));
            var settlements=SettlementAnalyzer.DescribeAll(s);
            Equal(2,settlements.Count);
            Equal(3,settlements.Sum(x=>x.Members));
        });
        Test("render snapshot exposes immutable settlement data", ()=>
        {
            var(s, first)=Fixture();
            var home=FinishedProject(s, new(5, 5));
            s.State.Entities.Get<FamilyComponent>(first).HomeProject=home;
            var snapshot=new LivingWorld.Presentation.RenderSnapshotBuilder().Capture(s, 1, true, 1, 0, new(), force:true);
            Equal(1, snapshot.Settlements.Count);
            Equal(1, snapshot.Settlements[0].Homes);
            Assert(snapshot.People.Any(x=>x.Id==first&&!string.IsNullOrWhiteSpace(x.Name)), "person name missing from render snapshot");
        });
        Test("fatal dehydration remains the recorded cause after drinking", ()=>
        {
            var(s,id)=Fixture();
            s.State.Map[new(6,5)].Water=WaterKind.River;
            var health=s.State.Entities.Get<HealthComponent>(id);
            var needs=s.State.Entities.Get<NeedsComponent>(id);
            health.Value=.05f; needs.Thirst=1;
            new NeedsSystem().Update(s);
            Assert(health.Value<=0,"dehydration was not fatal");
            Assert(new DrinkAction().Execute(s,id,new(){Position=new(6,5)}),"last drink failed");
            new LifeSystem().Update(s);
            Equal("обезвоживание",health.DeathReason);
        });
        Test("critical child can still receive care interaction", ()=>
        {
            var(s,parent)=Fixture();
            var child=new NpcFactory(D).Spawn(s.State,new(6,5),"female",s.State.Clock.Now);
            s.Spatial.Add(child,new(6,5));
            s.State.Entities.Get<FamilyComponent>(parent).Children.Add(child);
            s.State.Entities.Get<NeedsComponent>(child).Thirst=.96f;
            Assert(s.Interactions.Begin(parent,child,10,"care"),"critical thirst blocked care");
            s.Interactions.End(parent);
        });
        Test("perception records child thirst fatigue and health", ()=>
        {
            var(s,parent)=Fixture();
            var child=new NpcFactory(D).Spawn(s.State,new(6,5),"female",s.State.Clock.Now);
            s.Spatial.Add(child,new(6,5));
            s.State.Entities.Get<NeedsComponent>(child).Thirst=.83f;
            s.State.Entities.Get<NeedsComponent>(child).Fatigue=.71f;
            s.State.Entities.Get<HealthComponent>(child).Value=64;
            PerceptionSystem.Observe(s,parent,s.State.Entities.Get<MemoryComponent>(parent));
            var observation=s.State.Entities.Get<MemoryComponent>(parent).Observations.Single(o=>o.Entity==child);
            Equal(.83f,observation.Thirst);
            Equal(.71f,observation.Fatigue);
            Equal(64f,observation.Health);
        });
        Test("water memory keeps accessible shoreline samples", ()=>
        {
            var(s,id)=Fixture();
            PerceptionSystem.Observe(s,id,s.State.Entities.Get<MemoryComponent>(id));
            var water=s.State.Entities.Get<MemoryComponent>(id).Observations.Where(o=>o.Kind=="water").ToArray();
            Assert(water.Length>0,"freshwater not perceived");
            Assert(water.All(o=>s.State.Map[o.Position].Ice>=.15f||s.State.Map.Neighbors(o.Position).Any(s.State.Map.Walkable)),"unreachable interior water remembered");
            Assert(water.Length<10,"water cells still flood memory");
        });
        Test("thirsty child produces a targeted care plan", ()=>
        {
            var(s,parent)=Fixture();
            var child=new NpcFactory(D).Spawn(s.State,new(6,5),"female",s.State.Clock.Now);
            s.Spatial.Add(child,new(6,5));
            s.State.Entities.Get<FamilyComponent>(parent).Children.Add(child);
            s.State.Entities.Get<FamilyComponent>(child).Mother=parent;
            s.State.Entities.Get<NeedsComponent>(child).Thirst=.82f;
            Give(s,parent,"raspberry");
            PerceptionSystem.Observe(s,parent,s.State.Entities.Get<MemoryComponent>(parent));
            var context=ContextBuilder.Create(s,parent);
            var desire=new SocialEvaluator().Evaluate(context).First(x=>x.Fact==$"cared:{child}");
            var plan=s.Planner.Find(context,s.Actions.All.Where(a=>!a.RequiresWork||context.CanWork).SelectMany(a=>a.Options(context)).ToList(),desire);
            Assert(plan is not null&&plan.Steps.Any(x=>x.Action=="care"&&x.Target==child),"parent cannot plan care for thirsty child");
        });
        Test("planner uses an ice hole for frozen freshwater", ()=>
        {
            var(s,id)=Fixture();
            var water=new GridPoint(6,5);
            s.State.Map[water].Water=WaterKind.River;
            s.State.Map[water].Ice=.2f;
            Give(s,id,"stone_pick");
            var memory=s.State.Entities.Get<MemoryComponent>(id);
            memory.Observations.Add(new(){Kind="water",Position=water,Quantity=0,SeenTick=s.State.Clock.Tick});
            var context=ContextBuilder.Create(s,id);
            var plan=s.Planner.Find(context,s.Actions.All.Where(a=>!a.RequiresWork||context.CanWork).SelectMany(a=>a.Options(context)).ToList(),new("hydrated","test",10));
            Assert(plan is not null&&plan.Steps.Any(x=>x.Action=="break_ice"),"frozen water did not produce break ice plan");
        });
        Test("fatal overheating records overheating", ()=>
        {
            var(s,id)=Fixture();
            var health=s.State.Entities.Get<HealthComponent>(id);
            health.Value=.01f;
            s.State.Entities.Get<ThermalComponent>(id).Temperature=41;
            new TemperatureSystem().Update(s);
            new LifeSystem().Update(s);
            Equal("перегрев",health.DeathReason);
        });
        Test("critical unknown thirst triggers emergency exploration", ()=>
        {
            var(s,id)=Fixture();
            s.State.Entities.Get<MemoryComponent>(id).Observations.Clear();
            s.State.Entities.Get<NeedsComponent>(id).Thirst=.9f;
            var decision=s.State.Entities.Get<DecisionComponent>(id);
            decision.Plan=[new(){Action="sleep",Position=new(5,5),Duration=120,Local=true}];
            decision.DesiredFact="rested";
            decision.ChosenScore=1;
            while((s.State.Clock.Tick+id)%3!=0)s.State.Clock.Tick++;
            new DecisionSystem().Update(s);
            Equal("explore",decision.DesiredFact);
            Assert(decision.Motive.StartsWith("срочно ищет",StringComparison.Ordinal),"survival need did not force exploration");
        });
        Test("planner eats enough food instead of one token item", ()=>
        {
            var(s,id)=Fixture();
            Give(s,id,"grain"); Give(s,id,"grain");
            s.State.Entities.Get<NeedsComponent>(id).Hunger=.7f;
            var context=ContextBuilder.Create(s,id);
            var desire=new PhysiologyEvaluator().Evaluate(context).First(x=>x.Fact=="fed");
            Assert(desire.Minimum>650,"test hunger does not require multiple food items");
            var plan=s.Planner.Find(context,s.Actions.All.SelectMany(a=>a.Options(context)).ToList(),desire);
            Assert(plan is not null,"no feeding plan");
            Assert(plan!.Steps.Count(x=>x.Action=="eat")>=2,"planner treated one food item as fully fed");
        });
        Test("ice can be broken while standing on frozen water", ()=>
        {
            var(s,id)=Fixture();
            s.State.Map[new(5,5)].Water=WaterKind.River;
            s.State.Map[new(5,5)].Ice=.2f;
            Give(s,id,"stone_pick");
            Assert(new BreakIceAction().Execute(s,id,new(){Position=new(5,5)}),"ice under actor could not be broken");
            Equal(0f,s.State.Map[new(5,5)].Ice);
        });
        Test("sowing selects a visible valid tile instead of blocked ground", ()=>
        {
            var(s,id)=Fixture();
            s.State.Map[new(5,5)].Floor=123;
            Give(s,id,"grain");
            var context=ContextBuilder.Create(s,id);
            var option=new SowAction().Options(context).FirstOrDefault();
            Assert(option is not null,"no sow option on nearby valid soil");
            Assert(option!.Step.Position!=new GridPoint(5,5),"sow stayed on blocked current tile");
            Assert(s.State.Map[option.Step.Position].Floor==0&&s.State.Map[option.Step.Position].Water==WaterKind.None,"invalid sow tile selected");
        });
        Test("storage serves fresh food and cleanup destroys spoiled food", ()=>
        {
            var(s,id)=Fixture();
            var storage=StorageService.Create(s,new(5,5),0);
            var rotten=Give(s,id,"grain"); Assert(StorageService.Deposit(s,id,storage,rotten),"deposit rotten source");
            s.State.Entities.Get<ItemComponent>(rotten).Freshness=0;
            var fresh=Give(s,id,"grain"); Assert(StorageService.Deposit(s,id,storage,fresh),"deposit fresh source");
            Assert(StorageService.Take(s,id,storage,"grain"),"fresh grain not taken");
            Equal(id,s.State.Entities.Get<ItemComponent>(fresh).Holder);
            Equal(storage,s.State.Entities.Get<ItemComponent>(rotten).Holder);
            PerceptionSystem.Observe(s,id,s.State.Entities.Get<MemoryComponent>(id));
            var context=ContextBuilder.Create(s,id);
            var cleanup=new DiscardSpoiledAction().Options(context).First(o=>o.Step.Target==storage);
            Assert(new DiscardSpoiledAction().Execute(s,id,cleanup.Step),"storage cleanup failed");
            Assert(!s.State.Entities.Exists(rotten),"spoiled storage item survived cleanup");
        });
        Test("distant descendants may partner while first cousins remain related", ()=>
        {
            var(s,root)=Fixture();
            var childA=Adult(s,new(6,5)); var childB=Adult(s,new(7,5));
            s.State.Entities.Get<FamilyComponent>(childA).Mother=root;
            s.State.Entities.Get<FamilyComponent>(childB).Mother=root;
            var grandA=Adult(s,new(8,5)); var grandB=Adult(s,new(9,5));
            s.State.Entities.Get<FamilyComponent>(grandA).Mother=childA;
            s.State.Entities.Get<FamilyComponent>(grandB).Mother=childB;
            Assert(FamilyRules.Related(s.State,grandA,grandB),"first cousins were allowed");
            var greatA=Adult(s,new(10,5)); var greatB=Adult(s,new(11,5));
            s.State.Entities.Get<FamilyComponent>(greatA).Mother=grandA;
            s.State.Entities.Get<FamilyComponent>(greatB).Mother=grandB;
            Assert(!FamilyRules.Related(s.State,greatA,greatB),"distant descendants remain permanently blocked");
        });
        Test("teaching transfers the requested knowledge only", ()=>
        {
            var(s,teacher)=Fixture();
            var student=Adult(s,new(6,5));
            var knowledge=s.State.Entities.Get<KnowledgeComponent>(teacher);
            knowledge.Facts.Clear(); knowledge.Facts.UnionWith(["crafting","building"]);
            s.State.Entities.Get<KnowledgeComponent>(student).Facts.Clear();
            s.State.Entities.Get<SkillsComponent>(teacher).Experience["crafting"]=100;
            Assert(new TeachAction().Execute(s,teacher,new(){Target=student,Argument="crafting",Duration=30}),"teaching failed");
            s.Events.Flush();
            var learned=s.State.Entities.Get<KnowledgeComponent>(student).Facts;
            Assert(learned.Contains("crafting"),"requested knowledge missing");
            Assert(!learned.Contains("building"),"unrelated knowledge leaked");
        });
        Test("iron tools can use iron ingots for repairs", ()=>
        {
            var(s,id)=Fixture();
            var pick=Give(s,id,"iron_pick");
            s.State.Entities.Get<ItemComponent>(pick).Durability=10;
            Give(s,id,"iron_ingot");
            var option=new RepairAction().Options(ContextBuilder.Create(s,id)).FirstOrDefault(o=>o.Step.Target==pick);
            Assert(option is not null,"iron repair option missing");
            Equal("iron_ingot",option!.Step.Argument);
        });
        Test("harvested ground resources are not permanently owned", ()=>
        {
            var(s,id)=Fixture();
            var bush=Plant(s,new(6,5),"raspberry_bush",3);
            Assert(new HarvestAction().Execute(s,id,new(){Target=bush,Position=new(6,5)}),"harvest failed");
            Assert(s.State.Entities.Store<ItemComponent>().All.All(x=>s.State.Entities.Get<OwnershipComponent>(x.Key).Owner==0),"harvest leftovers stayed private");
        });
        Test("settlement names use the phonetic name generator", ()=>
        {
            var(s,id)=Fixture();
            var home=FinishedProject(s,new(4,4));
            s.State.Entities.Get<FamilyComponent>(id).HomeProject=home;
            var settlement=SettlementAnalyzer.DescribeAll(s).Single();
            var expected=new NameGenerator(D.Names).GenerateWord(new DeterministicRandom(RandomService.Hash(s.State.Seed,$"settlement:{home}:0")),2,4);
            Equal(expected,settlement.Name);
            Assert(settlement.Name.ToLowerInvariant().All(ch=>(D.Names.Vowels+D.Names.Consonants).Contains(ch)),"settlement name uses preset fragments");
        });
        Test("chop and mine consume planned tool uses", ()=>
        {
            var(s,id)=Fixture();
            Give(s,id,"stone_axe"); Give(s,id,"stone_pick");
            var tree=Plant(s,new(6,5),"oak",6);
            var resource=s.State.Entities.Create();
            s.State.Entities.Set(resource,new PositionComponent { Tile=new(6,6) });
            s.State.Entities.Set(resource,new ResourceComponent { Product="granite",Units=6 });
            s.Spatial.Add(resource,new(6,6));
            var memory=s.State.Entities.Get<MemoryComponent>(id);
            memory.Observations.Add(new(){Kind="plant",Entity=tree,Definition="oak",Product="log",Position=new(6,5),Quantity=6});
            memory.Observations.Add(new(){Kind="resource",Entity=resource,Product="granite",Position=new(6,6),Quantity=6});
            var context=ContextBuilder.Create(s,id);
            var chop=new ChopAction().Options(context).Single();
            var mine=new MineAction().Options(context).Single();
            Equal(-1,chop.Effects.Single(x=>x.Fact=="tool:chop").Amount);
            Equal(-1,mine.Effects.Single(x=>x.Fact=="tool:mine").Amount);
        });
        Test("ground item planning uses observed condition", ()=>
        {
            var(s,id)=Fixture();
            var axe=s.Inventory.Spawn("stone_axe",new(6,5));
            s.State.Entities.Get<ItemComponent>(axe).Durability=6;
            PerceptionSystem.Observe(s,id,s.State.Entities.Get<MemoryComponent>(id));
            var context=ContextBuilder.Create(s,id);
            var option=new PickUpAction().Options(context).Single(o=>o.Step.Target==axe);
            Equal(3,option.Effects.Single(x=>x.Fact=="tool:chop").Amount);
        });
        Test("planner tracks remaining tool uses", ()=>
        {
            var(s,id)=Fixture();
            var pick=Give(s,id,"stone_pick");
            s.State.Entities.Get<ItemComponent>(pick).Durability=1;
            Equal(1,ContextBuilder.Create(s,id).InitialState().Get("tool:mine"));
            s.State.Entities.Get<ItemComponent>(pick).Durability=10;
            Equal(5,ContextBuilder.Create(s,id).InitialState().Get("tool:mine"));
        });
        Test("planning temperature includes nearby fire heat", ()=>
        {
            var(s,id)=Fixture();
            var fire=s.State.Entities.Create();
            s.State.Entities.Set(fire,new PositionComponent { Tile=new(5,5) });
            s.State.Entities.Set(fire,new FireComponent { FuelMinutes=200,Heat=20 });
            s.Spatial.Add(fire,new(5,5));
            var context=ContextBuilder.Create(s,id,false);
            Assert(context.Air>context.OutdoorAir,"planner ignored fire heat");
        });
        Test("planner can compose fire before a heated recipe", ()=>
        {
            var(s,id)=Fixture();
            Give(s,id,"iron_ore"); Give(s,id,"iron_ore");
            Give(s,id,"log"); Give(s,id,"log"); Give(s,id,"log");
            s.State.Entities.Get<KnowledgeComponent>(id).Facts.Add("smithing");
            var context=ContextBuilder.Create(s,id);
            var plan=s.Planner.Find(context,s.Actions.All.SelectMany(a=>a.Options(context)).ToList(),new(PlanningContext.ItemFact("iron_ingot"),"test",1));
            Assert(plan is not null,"heated craft plan missing");
            var actions=plan!.Steps.Select(x=>x.Action).ToArray();
            Assert(actions.Contains("light_fire")&&actions.Contains("craft"),"fire and craft were not composed");
            Assert(Array.IndexOf(actions,"light_fire")<Array.IndexOf(actions,"craft"),"craft planned before fire");
        });
        Test("ordinary action failure does not poison location memory", ()=>
        {
            var(s,id)=Fixture();
            var bush=Plant(s,new(6,5),"raspberry_bush",3);
            var memory=new Observation{Kind="plant",Entity=bush,Definition="raspberry_bush",Product="raspberry",Position=new(6,5),Quantity=3};
            s.State.Entities.Get<MemoryComponent>(id).Observations.Add(memory);
            s.State.Entities.Get<DecisionComponent>(id).Plan=[new(){Action="harvest",Target=bush,Position=new(6,5)}];
            s.FailPlan(id,"ресурс уже занят");
            Equal(0L,memory.UnreachableUntil);
        });
        Test("separate save paths do not overwrite each other", ()=>
        {
            var(a, _)=Fixture();
            var(b, _)=Fixture();
            b.State.Seed=91;
            var directory=Path.Combine(Path.GetTempPath(),"living-world-slots-"+Guid.NewGuid().ToString("N"));
            var first=Path.Combine(directory,"world.save.json");
            var second=Path.Combine(directory,"world-slot-2.save.json");
            try
            {
                var saves=new SaveService();
                saves.Save(a,first);
                saves.Save(b,second);
                Equal(a.State.Seed,saves.Load(first,D).State.Seed);
                Equal(b.State.Seed,saves.Load(second,D).State.Seed);
                Assert(File.Exists(first)&&File.Exists(second),"save slot file missing");
            }
            finally
            {
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        });
        Test("save resumes exact future state", ()=>
        {
            var(s, id)=Fixture(); Give(s, id, "grain"); Plant(s, new(6, 5), "raspberry_bush", 9); s.Step(47); var saves=new SaveService(); var restored=saves.Deserialize(saves.Serialize(s), D); Equal(saves.Hash(s), saves.Hash(restored)); s.Step(70); restored.Step(70); Equal(saves.Hash(s), saves.Hash(restored));
        });
        Test("future save version rejected", ()=>
        {
            var(s, id)=Fixture(); var saves=new SaveService(); var json=JsonNode.Parse(saves.Serialize(s))!; json["FormatVersion"]=999; Throws(()=>saves.Deserialize(json.ToJsonString(), D));
        });
        Test("unknown component does not instantiate types", ()=>
        {
            var(s, id)=Fixture(); var saves=new SaveService(); var json=JsonNode.Parse(saves.Serialize(s))!; json["Components"]!["System.Process"]=new JsonObject(); Throws(()=>saves.Deserialize(json.ToJsonString(), D));
        });
        Test("changed definitions are rejected for saves", ()=>
        {
            var(s, id)=Fixture(); var saves=new SaveService(); var json=JsonNode.Parse(saves.Serialize(s))!; json["DefinitionsFingerprint"]="changed"; Throws(()=>saves.Deserialize(json.ToJsonString(), D));
        });
        Test("death cancels actions and releases inventory", ()=>
        {
            var(s, id)=Fixture(); var item=Give(s, id, "grain"); s.State.Entities.Get<HealthComponent>(id).Value=0; new LifeSystem().Update(s); Assert(!s.State.Entities.Get<HealthComponent>(id).Alive, "still alive"); Equal(0, s.State.Entities.Get<ItemComponent>(item).Holder);
        });
        Test("stored items update without equipment component", () =>
        {
            var (s, id) = Fixture(); var storage = StorageService.Create(s, new(5, 5), 0);
            var item = Give(s, id, "grain"); Assert(StorageService.Deposit(s, id, storage, item), "deposit");
            new ItemConditionSystem().Update(s);
            Assert(s.State.Entities.Get<ItemComponent>(item).Freshness < 1, "stored food does not age");
        });
        Test("spoiled food is not consumed", () =>
        {
            var (s, id) = Fixture(); var item = Give(s, id, "grain");
            s.State.Entities.Get<ItemComponent>(item).Freshness = 0;
            Assert(!new EatAction().Execute(s, id, new() { Argument = "grain" }), "spoiled food eaten");
            Equal(1, s.Inventory.Count(id, "grain"));
        });
        Test("additive content keeps existing saves loadable", () =>
        {
            var (s, id) = Fixture(); var saves = new SaveService(); var saved = saves.Serialize(s);
            var directory=CopyDefinitions();
            try
            {
                var source=Path.Combine(directory, "Items", "raspberry.json");
                var item=JsonNode.Parse(File.ReadAllText(source))!.AsObject();
                item["id"]="new_berry";
                File.WriteAllText(Path.Combine(directory, "Items", "new_berry.json"), item.ToJsonString());
                var extended=DefinitionLoader.Load(directory);
                var loaded=saves.Deserialize(saved, extended);
                Equal(s.State.Entities.Count, loaded.State.Entities.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
        Test("phonetic names are deterministic and varied", () =>
        {
            var generator = new NameGenerator(D.Names);
            var a = new DeterministicRandom(198);
            var b = new DeterministicRandom(198);
            var distinct = new HashSet<string>();
            for (var i = 0; i < 500; i++)
            {
                var name = generator.Generate(a, "female");
                Equal(name, generator.Generate(b, "female"));
                distinct.Add(name.First + " " + name.Last);
                foreach (var word in new[] { name.First, name.Last })
                {
                    Assert(char.IsUpper(word[0]), "missing uppercase initial");
                    var lower = word.ToLowerInvariant();
                    Assert(lower.All(c => (D.Names.Vowels + D.Names.Consonants).Contains(c)), "letter outside alphabet");
                    for (var j = 1; j < lower.Length; j++) Assert(lower[j] != lower[j - 1], "doubled letter");
                    for (var j = 2; j < lower.Length; j++)
                    {
                        var kinds = lower.Substring(j - 2, 3).Select(c => D.Names.Vowels.Contains(c)).ToArray();
                        Assert(!(kinds[0] == kinds[1] && kinds[1] == kinds[2]), "unpronounceable letter run");
                    }
                }
            }
            Assert(distinct.Count > 490, "insufficient variety");
        });
        Test("name randomness does not change NPC traits", () =>
        {
            var (a, _) = Fixture(); var (b, _) = Fixture();
            for (var i = 0; i < 25; i++) b.State.Random.Stream("names").NextUInt64();
            var left = Adult(a, new(5, 6)); var right = Adult(b, new(5, 6));
            Equal(a.State.Entities.Get<BodyComponent>(left).Strength, b.State.Entities.Get<BodyComponent>(right).Strength);
            Equal(a.State.Entities.Get<PersonalityComponent>(left).Caution, b.State.Entities.Get<PersonalityComponent>(right).Caution);
            Equal(a.State.Random.Stream("npc").State, b.State.Random.Stream("npc").State);
        });
        Test("children inherit surname without Russian suffix changes", () =>
        {
            var (s, mother) = Fixture();
            s.State.Entities.Get<IdentityComponent>(mother).Surname = "Лемара";
            var father = Adult(s, new(6, 5));
            var family = s.State.Entities.Get<FamilyComponent>(mother);
            family.PregnancyFather = father; family.PregnancyDueTick = 0;
            new FamilySystem().Update(s);
            Equal("Лемара", s.State.Entities.Get<IdentityComponent>(family.Children.Single()).Surname);
        });
        Test("legacy name rules do not invalidate existing identities", () =>
        {
            var (s, id) = Fixture(); var saves = new SaveService();
            s.State.Entities.Get<IdentityComponent>(id).FirstName = "Иван";
            s.State.Entities.Get<IdentityComponent>(id).Surname = "Соколов";
            var json = JsonNode.Parse(saves.Serialize(s))!;
            json["DefinitionManifest"]!["names"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("legacy name lists")));
            var manifest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json["DefinitionManifest"]!.ToJsonString())!;
            json["DefinitionsFingerprint"] = DefinitionFingerprint.OfManifest(manifest);
            var loaded = saves.Deserialize(json.ToJsonString(), D);
            Equal("Иван", loaded.State.Entities.Get<IdentityComponent>(id).FirstName);
            Equal("Соколов", loaded.State.Entities.Get<IdentityComponent>(id).Surname);
            loaded.Step(5);
        });
        Test("name migration does not allow changed gameplay definitions", () =>
        {
            var (s, _) = Fixture(); var saves = new SaveService();
            var directory=CopyDefinitions();
            try
            {
                var path=Path.Combine(directory, "Items", "raspberry.json");
                var item=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                item["mass"]=123;
                File.WriteAllText(path, item.ToJsonString());
                var changed=DefinitionLoader.Load(directory);
                Throws(() => saves.Deserialize(saves.Serialize(s), changed));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
        Test("visual dirtiness is local to a chunk", () =>
        {
            var map = new WorldMap(48, 48);
            map.MarkVisualDirty(new(2, 2));
            Assert(map.ChunkVisualRevision(0) > 0, "dirty chunk not marked");
            for (var key = 1; key < 9; key++) Equal(0, map.ChunkVisualRevision(key));
        });
        Test("unchanged water appearance does not invalidate terrain", () =>
        {
            var (s, _) = Fixture();
            s.State.Clock.Tick = 60;
            var before = s.State.Map.VisualRevision;
            new WaterSystem().Update(s);
            Equal(before, s.State.Map.VisualRevision);
        });
        Test("visibility cache refreshes after walls change", () =>
        {
            var (s, id) = Fixture(); var plant = Plant(s, new(7, 5), "raspberry_bush", 3);
            var memory = s.State.Entities.Get<MemoryComponent>(id);
            PerceptionSystem.Observe(s, id, memory);
            Assert(memory.Observations.Any(o => o.Entity == plant), "initial plant invisible");
            s.State.Map[new(6, 5)].Wall = 99; s.State.Map.NavigationRevision++; s.State.Clock.Tick = 6;
            PerceptionSystem.Observe(s, id, memory);
            Equal(0L, memory.Observations.First(o => o.Entity == plant).SeenTick);
            s.State.Map[new(6, 5)].Wall = 0; s.State.Map.NavigationRevision++; s.State.Clock.Tick = 12;
            PerceptionSystem.Observe(s, id, memory);
            Equal(12L, memory.Observations.First(o => o.Entity == plant).SeenTick);
        });
        Test("render snapshots never share live mutable components", () =>
        {
            var (s, id) = Fixture(); var builder = new LivingWorld.Presentation.RenderSnapshotBuilder();
            var saves = new SaveService(); var before = saves.Hash(s);
            var a = builder.Capture(s, 1, true, 1, 0, new(id), force: true);
            Equal(before, saves.Hash(s));
            s.State.Entities.Get<PositionComponent>(id).Tile = new(9, 9);
            s.State.Map[new(5, 5)].Snow = 1; s.State.Map.MarkVisualDirty(new(5, 5));
            var b = builder.Capture(s, 1, true, 1, 0, new(id), force: true);
            Equal(new GridPoint(5, 5), a.People.Single().Tile);
            Equal(new GridPoint(9, 9), b.People.Single().Tile);
            Equal(0f, a.Chunks[0].Tiles[5 * 16 + 5].Snow);
            Equal(1f, b.Chunks[0].Tiles[5 * 16 + 5].Snow);
            Assert(ReferenceEquals(a.Chunks[1], b.Chunks[1]), "unchanged chunk copied");
        });
        Test("camera requests and speed do not alter equal tick outcomes", () =>
        {
            var (s, id) = Fixture(); Give(s, id, "grain"); Plant(s, new(6, 5), "raspberry_bush", 9);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                var saves = new SaveService(); saves.Save(s, path);
                using var runner = new LivingWorld.Presentation.SimulationRunner(D);
                runner.LoadAsync(path).GetAwaiter().GetResult();
                runner.SetView(new(id, new(19, 19), true));
                runner.SetControlsAsync(true, 32).GetAwaiter().GetResult();
                runner.AdvanceAsync(47).GetAwaiter().GetResult();
                runner.SetView(new(0, new(0, 0), false));
                runner.SetControlsAsync(true, 1).GetAwaiter().GetResult();
                runner.AdvanceAsync(70).GetAwaiter().GetResult();
                s.Step(117);
                Equal(saves.Hash(s), runner.StateHashAsync().GetAwaiter().GetResult());
                runner.SaveAsync(path).GetAwaiter().GetResult();
                Equal(saves.Hash(s), saves.Hash(saves.Load(path, D)));
                runner.LoadAsync(path + ".missing").ContinueWith(_ => { }).GetAwaiter().GetResult();
                Equal(saves.Hash(s), runner.StateHashAsync().GetAwaiter().GetResult());
            }
            finally { File.Delete(path); }
        });
        Test("worker progresses without frames and pauses at tick boundary", () =>
        {
            var (s, _) = Fixture(); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                new SaveService().Save(s, path);
                using var runner = new LivingWorld.Presentation.SimulationRunner(D);
                runner.LoadAsync(path).GetAwaiter().GetResult();
                var tick = runner.Latest!.Tick;
                runner.SetControlsAsync(false, 32).GetAwaiter().GetResult();
                Assert(System.Threading.SpinWait.SpinUntil(() => runner.Latest!.Tick > tick, 5000), "simulation depends on graphics frames");
                runner.SetControlsAsync(true, 32).GetAwaiter().GetResult();
                var paused = runner.Latest!.Tick;
                Task.Delay(80).GetAwaiter().GetResult();
                Equal(paused, runner.Latest!.Tick);
            }
            finally { File.Delete(path); }
        });
        Test("compact planner preserves reference plans and costs", () =>
        {
            foreach (var seed in new[] { 7, 19 })
            {
                var state = new WorldGenerator().Generate(D, seed, 48, 3);
                var session = new SimulationSession(state, D);
                foreach (var actor in state.Entities.Store<IdentityComponent>().Ids())
                {
                    PerceptionSystem.Observe(session, actor, state.Entities.Get<MemoryComponent>(actor));
                    var context = ContextBuilder.Create(session, actor);
                    var options = session.Actions.All.SelectMany(action => action.Options(context)).ToList();
                    foreach (var fact in new[] { "fed", "hydrated", "warm", "housing_progress" })
                    {
                        var desire = new DesiredState(fact, "test", 1);
                        var expected = new ReferenceForwardPlanner().Find(context, options, desire);
                        var actual = session.Planner.Find(context, options, desire);
                        Equal(expected is null, actual is null);
                        if (expected is null) continue;
                        Equal(expected.Cost, actual!.Cost);
                        Equal(string.Join(";", expected.Steps), string.Join(";", actual.Steps));
                    }
                }
            }
        });
        Console.WriteLine($"\n{_passed} passed; {_failed} failed.");
        return _failed==0?0:1;
    }
    private static string CopyDefinitions()
    {
        var source=Path.Combine(AppContext.BaseDirectory, "Data");
        var target=Path.Combine(Path.GetTempPath(), "living-world-definitions-"+Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var destination=Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        return target;
    }

    private static (SimulationSession Session, int Actor) Fixture(int age=25)
    {
        var state=new WorldState
        {
            Seed=7, Map=new(20, 20), Random=new(7), Start=new(5, 5)
        };
        foreach (var t in state.Map.Tiles)
        {
            t.Height=.5f;
            t.DrainageHeight=.5f;
            t.Moisture=.7f;
            t.Fertility=.7f;
            t.BaseTemperature=18;
            t.Biome=Biome.Meadow;
        }
        for (var y=0; y<20; y++)state.Map[new(0, y)].Water=WaterKind.River;
        var id=new NpcFactory(D).Spawn(state, new(5, 5), "male", state.Clock.Now.AddYears(-age));
        state.Entities.Get<PersonalityComponent>(id).Generosity=.4f;
        return(new SimulationSession(state, D), id);
    }
    private static int Adult(SimulationSession s, GridPoint p)
    {
        var id=new NpcFactory(D).Spawn(s.State, p, "male", s.State.Clock.Now.AddYears(-25));
        s.State.Entities.Get<PersonalityComponent>(id).Generosity=.8f;
        s.Spatial.Add(id, p);
        return id;
    }
    private static int Give(SimulationSession s, int id, string definition)
    {
        var item=s.Inventory.Spawn(definition, s.State.Entities.Get<PositionComponent>(id).Tile, id);
        Assert(s.Inventory.PickUp(id, item), "fixture pickup failed");
        return item;
    }
    private static int Plant(SimulationSession s, GridPoint p, string definition, int amount)
    {
        var id=s.State.Entities.Create();
        s.State.Entities.Set(id, new PositionComponent
        {
            Tile=p
        });
        s.State.Entities.Set(id, new PlantComponent
        {
            Definition=definition, Yield=amount, Growth=1
        });
        s.Spatial.Add(id, p);
        return id;
    }
    private static int FinishedProject(SimulationSession s, GridPoint position)
    {
        var id=s.State.Entities.Create();
        s.State.Entities.Set(id, new PositionComponent { Tile=position });
        s.State.Entities.Set(id, new ConstructionComponent
        {
            Definition="wooden_cabin",
            Elements=[],
            Completed=0
        });
        s.Spatial.Add(id, position);
        return id;
    }

    private static int BuildHome(SimulationSession s, int actor)
    {
        s.State.Entities.Get<InventoryComponent>(actor).MaxMass=500;
        s.State.Entities.Get<InventoryComponent>(actor).MaxVolume=500;
        var project=BuildingService.Start(s, actor, new(5, 5), "wooden_cabin");
        Assert(project!=0, "start construction failed");
        var c=s.State.Entities.Get<ConstructionComponent>(project);
        for (var i=0; i<c.Elements.Count; i++)Give(s, actor, "log");
        while (!c.Finished)
        {
            var next=c.Elements[c.Completed];
            s.State.Entities.Get<PositionComponent>(actor).Tile=next.Position;
            s.Spatial.Add(actor, next.Position);
            Assert(BuildingService.BuildNext(s, actor, project), "build failed");
        }
        return project;
    }
    private static void Test(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS "+name);
            _passed++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL "+name+": "+ex.Message);
            _failed++;
        }
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition)throw new Exception(message);
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))throw new Exception($"Expected {expected}; got {actual}");
    }
    private static void Throws(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new Exception("Expected InvalidDataException");
    }
}

// Slow original dictionary-based planner is retained only as a regression oracle.
internal sealed class ReferenceForwardPlanner
{
    private sealed record SearchNode(PlanningState State, List<ActionOption> Path, float Cost);
    public int NodeBudget { get; init; } = 180;
    public int MaxDepth { get; init; } = 8;
    public PlanResult? Find(PlanningContext context, IReadOnlyList<ActionOption> options, DesiredState desired)
    {
        foreach (var option in options)
        {
            var items=option.Effects.Where(e=>e.Fact.StartsWith("item:", StringComparison.Ordinal)&&!e.Set).ToArray();
            option.MassDelta=(int)MathF.Ceiling(items.Sum(e=>context.Definitions.Items[e.Fact[5..]].Mass*e.Amount)*1000);
            option.VolumeDelta=(int)MathF.Ceiling(items.Sum(e=>context.Definitions.Items[e.Fact[5..]].Volume*e.Amount)*1000);
        }
        // Backward relevance pruning keeps unrelated production and social branches out of this search.
        var facts=new HashSet<string>(StringComparer.Ordinal)
        {
            desired.Fact
        };
        var relevant=new HashSet<ActionOption>();
        for (var pass=0; pass<MaxDepth; pass++)
        {
            var changed=false;
            foreach (var option in options) if (option.Effects.Any(e=>e.Amount>0&&facts.Contains(e.Fact))&&relevant.Add(option))
            {
                foreach (var need in option.Requires)facts.Add(need.Fact);
                changed=true;
            }
            if (!changed)break;
        }
        var selected=options.Where(relevant.Contains).ToArray();
        if (selected.Length==0)return null;
        var initial=context.InitialState();
        var queue=new PriorityQueue<SearchNode, (float, int)>();
        var serial=0;
        queue.Enqueue(new(initial, [], 0), (0, serial++));
        var visited=new Dictionary<string, float>(StringComparer.Ordinal);
        var expanded=0;
        while (queue.TryDequeue(out var node, out _)&&expanded++<NodeBudget)
        {
            if (node.State.Get(desired.Fact)>=desired.Minimum)return new(Compile(context, node.Path), node.Cost, expanded);
            if (node.Path.Count>=MaxDepth)continue;
            foreach (var option in selected)
            {
                if (!option.Applies(node.State))continue;
                var next=option.Apply(node.State);
                var travel=option.GroundedAtActor?0:Math.Max(0, node.State.Position.Distance(option.Step.Position)-option.Step.Range);
                var cost=node.Cost+option.Cost+travel+option.Risk*(1+context.Personality.Caution);
                var key=next.Key();
                if (visited.TryGetValue(key, out var previous)&&previous<=cost)continue;
                visited[key]=cost;
                var path=new List<ActionOption>(node.Path)
                {
                    option
                };
                queue.Enqueue(new(next, path, cost), (cost, serial++));
            }
        }
        return null;
    }
    private static List<ActionStep> Compile(PlanningContext context, List<ActionOption> path)
    {
        var steps=new List<ActionStep>();
        var position=context.Position;
        foreach (var option in path)
        {
            var step=option.Step with
            {
            };
            if (option.GroundedAtActor)step.Position=position;
            if (position.Distance(step.Position)>step.Range) steps.Add(new()
            {
                Action="move", Position=step.Position, Range=step.Range, Target=step.Target, Argument=step.Action, Duration=1
            });
            steps.Add(step);
            if (!option.GroundedAtActor)position=step.Position;
        }
        return steps;
    }
}
