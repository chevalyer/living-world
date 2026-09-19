namespace LivingWorld.Simulation;
public sealed class PerceptionSystem : ISimulationSystem
{
    private sealed class VisibilityCache
    {
        public WorldMap? Map;
        public GridPoint Center;
        public int Radius = -1;
        public int Revision = -1;
        public byte[] Cells = [];
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MemoryComponent, VisibilityCache> Visibility = new();
    public string Name=>"perception and memory";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var processed=0;
        foreach (var (actor, memory) in e.Store<MemoryComponent>().All)
        {
            if ((s.Clock.Tick+actor)%6!=0||!e.Get<HealthComponent>(actor).Alive)continue;
            processed++;
            Observe(session, actor, memory);
        }
        return processed;
    }
    public static void Observe(SimulationSession session, int actor, MemoryComponent memory)
    {
        var s=session.State;
        var e=s.Entities;
        var center=e.Get<PositionComponent>(actor).Tile;
        var radius=s.Weather.Sunlight<.1f?7:12;
        var cache=Visibility.GetValue(memory, _=>new VisibilityCache());
        var diameter=radius*2+1;
        if (!ReferenceEquals(cache.Map,s.Map)||cache.Center!=center||cache.Radius!=radius||cache.Revision!=s.Map.NavigationRevision)
        {
            cache.Map=s.Map;
            cache.Center=center;
            cache.Radius=radius;
            cache.Revision=s.Map.NavigationRevision;
            if(cache.Cells.Length!=diameter*diameter)cache.Cells=new byte[diameter*diameter];
            else Array.Clear(cache.Cells);
        }
        bool Visible(GridPoint p)
        {
            var x=p.X-center.X+radius;
            var y=p.Y-center.Y+radius;
            if(x<0||y<0||x>=diameter||y>=diameter||!s.Map.Contains(p)||center.Distance(p)>radius)return false;
            var index=y*diameter+x;
            if(cache.Cells[index]==0)cache.Cells[index]=LineOfSight(s.Map,center,p)?(byte)2:(byte)1;
            return cache.Cells[index]==2;
        }
        var cooldowns=new Dictionary<(string,int,GridPoint),long>();
        foreach(var observation in memory.Observations)
        {
            var key=(observation.Kind,observation.Entity,observation.Position);
            if(!cooldowns.TryGetValue(key,out var previous)||observation.UnreachableUntil>previous)cooldowns[key]=observation.UnreachableUntil;
        }
        // Refresh visible observations, including absence; distant memories retain the last known values.
        var family=e.Get<FamilyComponent>(actor);
        var home=family.HomeProject;
        var dependentChildren=family.Children.Where(id=>e.Try<IdentityComponent>(id) is { } identity&&s.Clock.Age(identity.BirthDate)<3).ToHashSet();
        memory.Observations.RemoveAll(o=>Visible(o.Position)||(o.Entity!=home&&!dependentChildren.Contains(o.Entity)&&s.Clock.Tick-o.SeenTick>1440*20));
        void Add(Observation observation)
        {
            observation.SeenTick=s.Clock.Tick;
            observation.UnreachableUntil=cooldowns.GetValueOrDefault((observation.Kind, observation.Entity, observation.Position));
            memory.Observations.Add(observation);
        }
        foreach (var id in session.Spatial.Query(center, radius))
        {
            if (id==actor)continue;
            var pos=e.Try<PositionComponent>(id);
            if (pos is null||!Visible(pos.Tile))continue;
            var o=new Observation
            {
                Entity=id, Position=pos.Tile, Owner=e.Try<OwnershipComponent>(id)?.Owner??0
            };
            if (e.Try<PlantComponent>(id) is { } plant)
            {
                var d=session.Definitions.Plants[plant.Definition];
                o.Kind="plant";
                o.Definition=plant.Definition;
                o.Product=d.Product;
                o.Quantity=(int)plant.Yield;
            }
            else if (e.Try<ResourceComponent>(id) is { } resource)
            {
                o.Kind="resource";
                o.Product=resource.Product;
                o.Quantity=resource.Units;
            }
            else if (e.Try<ItemComponent>(id) is
            {
                Holder:0
            }
            item)
            {
                o.Kind="item";
                o.Definition=item.Definition;
                o.Product=item.Definition;
                o.Freshness=item.Freshness;
                o.Durability=item.Durability;
                o.Quality=item.Quality;
                o.Quantity=session.Definitions.Items[item.Definition].Calories>0&&item.Freshness<.1f?0:1;
            }
            else if (e.Try<IdentityComponent>(id) is { } identity&&e.Get<HealthComponent>(id).Alive)
            {
                o.Kind="npc";
                var needs=e.Get<NeedsComponent>(id);
                o.Need=needs.Hunger;
                o.Thirst=needs.Thirst;
                o.Fatigue=needs.Fatigue;
                o.Health=e.Get<HealthComponent>(id).Value;
                o.Age=s.Clock.Age(identity.BirthDate);
                o.Sex=identity.Sex;
                o.Partner=e.Get<FamilyComponent>(id).Partner;
                o.Pregnant=e.Get<FamilyComponent>(id).PregnancyDueTick.HasValue;
                o.Room=s.Map[pos.Tile].Room;
                o.Sheltered=EnvironmentQueries.Sheltered(s,pos.Tile);
                var targetDecision=e.Get<DecisionComponent>(id);
                var targetInteraction=e.Try<InteractionComponent>(id);
                var sleeping=targetDecision.Plan.FirstOrDefault()?.Action=="sleep"&&targetDecision.RemainingMinutes>0;
                o.SocialAvailable=needs.Hunger<=.9f&&needs.Thirst<=.9f&&!sleeping&&
                    !(targetInteraction is not null&&targetInteraction.Until>s.Clock.Tick);
                var back=e.Get<RelationshipComponent>(id).People.GetValueOrDefault(actor);
                o.TrustBack=back?.Trust??.3f;
                o.AffectionBack=back?.Affection??.3f;
                o.Skills=new(e.Get<SkillsComponent>(id).Experience);
                o.Items=e.Get<InventoryComponent>(id).Items
                    .Select(i=>e.Get<ItemComponent>(i))
                    .Where(item=>session.Definitions.Items[item.Definition].Calories<=0||item.Freshness>=.1f)
                    .GroupBy(item=>item.Definition).ToDictionary(g=>g.Key,g=>g.Count());
            }
            else if(e.Try<FacilityComponent>(id) is { } facility)
            {
                var definition=session.Definitions.Facilities[facility.Definition];
                o.Kind="facility";
                o.Definition=facility.Definition;
                o.Project=facility.Project;
                o.Capabilities=definition.Capabilities.ToArray();
                if(e.Has<StorageComponent>(id))
                {
                    var stored=e.Get<InventoryComponent>(id).Items.Select(i=>(Id:i,Item:e.Get<ItemComponent>(i))).ToArray();
                    o.Spoiled=stored.Count(x=>session.Definitions.Items[x.Item.Definition].Calories>0&&x.Item.Freshness<.1f);
                    o.Items=stored.Where(x=>
                    {
                        var itemDefinition=session.Definitions.Items[x.Item.Definition];
                        if(itemDefinition.Calories>0&&x.Item.Freshness<.1f)return false;
                        if(itemDefinition.Tools.Count>0&&x.Item.Durability<=0)return false;
                        return true;
                    }).GroupBy(x=>x.Item.Definition).ToDictionary(g=>g.Key,g=>g.Count());
                    o.Quantity=o.Items.Values.Sum();
                }
                else o.Quantity=1;
            }
            else if (e.Has<StorageComponent>(id))
            {
                var storage=e.Get<StorageComponent>(id);
                o.Kind="storage";
                o.Project=storage.Project;
                var stored=e.Get<InventoryComponent>(id).Items.Select(i=>(Id:i,Item:e.Get<ItemComponent>(i))).ToArray();
                o.Spoiled=stored.Count(x=>session.Definitions.Items[x.Item.Definition].Calories>0&&x.Item.Freshness<.1f);
                o.Items=stored.Where(x=>
                {
                    var definition=session.Definitions.Items[x.Item.Definition];
                    if(definition.Calories>0&&x.Item.Freshness<.1f)return false;
                    if(definition.Tools.Count>0&&x.Item.Durability<=0)return false;
                    return true;
                }).GroupBy(x=>x.Item.Definition).ToDictionary(g=>g.Key,g=>g.Count());
                o.Quantity=o.Items.Values.Sum();
            }
            else if (e.Try<FireComponent>(id) is { } fire)
            {
                o.Kind="fire";
                o.Quantity=(int)fire.FuelMinutes;
            }
            else if (e.Try<ConstructionComponent>(id) is { } project)
            {
                o.Kind="project";
                o.Definition=project.Definition;
                o.Quantity=project.Elements.Count-project.Completed;
                o.WorkPosition=project.Finished?null:project.Elements[project.Completed].Position;
            }
            else if (e.Has<FarmCellComponent>(id))
            {
                o.Kind="farm_cell";
                o.Definition=FarmService.CellState(session,id);
                o.Quantity=1;
            }
            else if (e.Has<FarmPlotComponent>(id))continue;
            else continue;
            Add(o);
        }
        var waterSamples=new List<GridPoint>();
        var roomsSeen=new HashSet<int>();
        for (var dy=-radius; dy<=radius; dy++)for (var dx=-radius; dx<=radius; dx++)
        {
            var p=center+new GridPoint(dx,dy);
            if (!s.Map.Contains(p))continue;
            var tile=s.Map[p];
            if(tile.Water is not (WaterKind.River or WaterKind.Lake)&&!(tile.Room>0&&tile.Roof>0))continue;
            if(!Visible(p))continue;
            if(tile.Water is WaterKind.River or WaterKind.Lake)
            {
                var accessible=tile.Ice>=.15f||s.Map.Neighbors(p).Any(s.Map.Walkable);
                if(accessible&&!waterSamples.Any(sample=>sample.Distance(p)<=3))
                {
                    waterSamples.Add(p);
                    Add(new Observation { Kind="water",Position=p,Quantity=tile.Ice<.15f?1:0 });
                }
            }
            if(tile.Room>0&&tile.Roof>0&&roomsSeen.Add(tile.Room))
                Add(new Observation { Kind="shelter",Position=p,Quantity=1,Entity=tile.Room,Temperature=EnvironmentQueries.Local(s,p) });
        }
        memory.Visited.Add(center);
        if (memory.Visited.Count>128)memory.Visited.RemoveAt(0);
        foreach (var o in memory.Observations)o.Confidence=Math.Max(.1f, 1-(s.Clock.Tick-o.SeenTick)/(1440f*20));
        if (memory.Observations.Count>240)memory.Observations=memory.Observations.OrderByDescending(o=>o.Entity==home&&home!=0).ThenByDescending(o=>o.SeenTick).ThenBy(o=>o.Position.Distance(center)).Take(240).ToList();
        if (memory.Events.Count>32)memory.Events.RemoveRange(0, memory.Events.Count-32);
    }
    public static bool LineOfSight(WorldMap map, GridPoint from, GridPoint to)
    {
        var x=from.X;
        var y=from.Y;
        var dx=Math.Abs(to.X-x);
        var dy=Math.Abs(to.Y-y);
        var sx=x<to.X?1:-1;
        var sy=y<to.Y?1:-1;
        var err=dx-dy;
        while (x!=to.X||y!=to.Y)
        {
            var e2=2*err;
            if (e2>-dy)
            {
                err-=dy;
                x+=sx;
            }
            if (e2<dx)
            {
                err+=dx;
                y+=sy;
            }
            if (x==to.X&&y==to.Y)return true;
            if (map[new(x, y)].Wall>0)return false;
        }
        return true;
    }
}
