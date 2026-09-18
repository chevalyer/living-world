namespace LivingWorld.Simulation;
public static class StorageService
{
    public static int Create(SimulationSession session,GridPoint p,int project)
    {
        var e=session.State.Entities;
        var id=e.Create();
        e.Set(id,new PositionComponent { Tile=p });
        e.Set(id,new StorageComponent { Project=project });
        e.Set(id,new InventoryComponent { MaxMass=500,MaxVolume=800 });
        e.Set(id,new OwnershipComponent());
        session.Spatial.Add(id,p);
        return id;
    }
    public static bool Deposit(SimulationSession session,int actor,int storage,int item)
    {
        var e=session.State.Entities;
        if(!e.Has<StorageComponent>(storage))return false;
        if(!session.Inventory.Transfer(actor,storage,item,"deposit"))return false;
        e.Get<OwnershipComponent>(item).Owner=0;
        return true;
    }
    public static bool Take(SimulationSession session,int actor,int storage,string definition)
    {
        var e=session.State.Entities;
        if(!e.Has<StorageComponent>(storage))return false;
        var item=session.Inventory.Items(storage)
            .Where(id=>e.Get<ItemComponent>(id).Definition==definition&&(e.Get<OwnershipComponent>(id).Owner==0||e.Get<OwnershipComponent>(id).Owner==actor))
            .Where(id=>session.Definitions.Items[definition].Calories<=0||e.Get<ItemComponent>(id).Freshness>=.1f)
            .OrderByDescending(id=>e.Get<ItemComponent>(id).Freshness)
            .ThenByDescending(id=>e.Get<ItemComponent>(id).Durability)
            .FirstOrDefault();
        return item!=0&&session.Inventory.Transfer(storage,actor,item,"take");
    }
}
