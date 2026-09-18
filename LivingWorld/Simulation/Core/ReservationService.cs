namespace LivingWorld.Simulation;
public sealed record Reservation(int Actor, long Expires);
public sealed class ReservationService
{
    public Dictionary<int, Reservation> Entries { get; set; } = [];
    public bool Claim(int target, int actor, long now)
    {
        if (target==0)return true;
        if (Entries.TryGetValue(target, out var current)&&current.Actor!=actor&&current.Expires>now)return false;
        Entries[target]=new(actor, now+120);
        return true;
    }
    public void Release(int actor)
    {
        foreach (var target in Entries.Where(x=>x.Value.Actor==actor).Select(x=>x.Key).ToArray())Entries.Remove(target);
    }
    public void Expire(long now)
    {
        foreach (var target in Entries.Where(x=>x.Value.Expires<=now).Select(x=>x.Key).ToArray())Entries.Remove(target);
    }
}
