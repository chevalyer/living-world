namespace LivingWorld.Simulation;
public sealed class InventoryService(WorldState state, DefinitionCatalog definitions, SpatialIndex spatial, EventBus events)
{
    private EntityRegistry E=>state.Entities;
    public IEnumerable<int> Items(int actor)=>E.Get<InventoryComponent>(actor).Items;
    public float Mass(int actor)=>Items(actor).Sum(id=>definitions.Items[E.Get<ItemComponent>(id).Definition].Mass);
    public float Volume(int actor)=>Items(actor).Sum(id=>definitions.Items[E.Get<ItemComponent>(id).Definition].Volume);
    public int Count(int actor, string definition)=>Items(actor).Count(id=>E.Get<ItemComponent>(id).Definition==definition);
    public bool CanCarry(int actor, string definition, int count=1)
    {
        var inv=E.Get<InventoryComponent>(actor);
        var d=definitions.Items[definition];
        return Mass(actor)+d.Mass*count<=inv.MaxMass && Volume(actor)+d.Volume*count<=inv.MaxVolume;
    }
    public int Spawn(string definition, GridPoint p, int owner=0)
    {
        var id=ResourcePass.SpawnItem(state, definitions, definition, p, owner);
        spatial.Add(id, p);
        return id;
    }
    public bool PickUp(int actor, int id)
    {
        var item=E.Try<ItemComponent>(id);
        var position=E.Try<PositionComponent>(id);
        if (item is null||item.Holder!=0||position is null||position.Tile.Distance(E.Get<PositionComponent>(actor).Tile)>1||!CanCarry(actor, item.Definition))return false;
        var owner=E.Get<OwnershipComponent>(id);
        if (owner.Owner!=0 && owner.Owner!=actor)return false;
        item.Holder=actor;
        owner.Owner=actor;
        E.Get<InventoryComponent>(actor).Items.Add(id);
        E.Store<PositionComponent>().Remove(id);
        spatial.Remove(id);
        return true;
    }
    public bool Drop(int actor, int id)
    {
        var item=E.Try<ItemComponent>(id);
        if (item is null||item.Holder!=actor)return false;
        E.Get<InventoryComponent>(actor).Items.Remove(id);
        E.Try<EquipmentComponent>(actor)?.Items.Remove(id);
        item.Holder=0;
        var p=E.Get<PositionComponent>(actor).Tile;
        E.Set(id, new PositionComponent
        {
            Tile=p
        });
        spatial.Add(id, p);
        return true;
    }
    public bool Transfer(int actor, int target, int id, string reason="gift")
    {
        var item=E.Try<ItemComponent>(id);
        if (item is null||item.Holder!=actor||!E.Has<InventoryComponent>(target)||!CanCarry(target, item.Definition))return false;
        if (E.Get<PositionComponent>(actor).Tile.Distance(E.Get<PositionComponent>(target).Tile)>1)return false;
        E.Get<InventoryComponent>(actor).Items.Remove(id);
        E.Try<EquipmentComponent>(actor)?.Items.Remove(id);
        E.Get<InventoryComponent>(target).Items.Add(id);
        item.Holder=target;
        E.Get<OwnershipComponent>(id).Owner=target;
        events.Publish(new ItemTransferredEvent(id, actor, target, reason));
        return true;
    }
    public bool Consume(int actor, string definition, int count)
    {
        if (count<=0)return false;
        var ids=Items(actor).Where(id=>E.Get<ItemComponent>(id).Definition==definition).Take(count).ToArray();
        if (ids.Length<count)return false;
        foreach (var id in ids)Destroy(id);
        return true;
    }
    public void Destroy(int id)
    {
        var item=E.Try<ItemComponent>(id);
        if (item is not null&&item.Holder!=0)
        {
            E.Try<InventoryComponent>(item.Holder)?.Items.Remove(id);
            E.Try<EquipmentComponent>(item.Holder)?.Items.Remove(id);
        }
        spatial.Remove(id);
        E.Remove(id);
    }
    public bool Wear(int actor, int id)
    {
        var item=E.Try<ItemComponent>(id);
        if (item is null||item.Holder!=actor||item.Durability<=0)return false;
        var slots=definitions.Items[item.Definition].Slots;
        if (slots.Length==0)return false;
        var equipment=E.Get<EquipmentComponent>(actor);
        equipment.Items.RemoveAll(other=>definitions.Items[E.Get<ItemComponent>(other).Definition].Slots.Intersect(slots, StringComparer.Ordinal).Any());
        equipment.Items.Add(id);
        return true;
    }
    public float Tool(int actor, string capability)
    {
        return Items(actor).Select(id=>E.Get<ItemComponent>(id)).Where(x=>x.Durability>0).Select(x=>definitions.Items[x.Definition].Tools.GetValueOrDefault(capability)*x.Quality*x.Sharpness).DefaultIfEmpty(0).Max();
    }
    public void WearTool(int actor, string capability, float amount)
    {
        var id=Items(actor).Where(id=>E.Get<ItemComponent>(id).Durability>0&&definitions.Items[E.Get<ItemComponent>(id).Definition].Tools.ContainsKey(capability)).OrderByDescending(id=>definitions.Items[E.Get<ItemComponent>(id).Definition].Tools[capability]).FirstOrDefault();
        if (id==0)return;
        var item=E.Get<ItemComponent>(id);
        item.Durability=Math.Max(0, item.Durability-amount);
        item.Sharpness=Math.Max(.2f, item.Sharpness-.003f*amount);
    }
}
