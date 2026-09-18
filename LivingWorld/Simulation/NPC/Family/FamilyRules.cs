namespace LivingWorld.Simulation;
public static class FamilyRules
{
    public static bool Related(WorldState s,int a,int b)
    {
        if(a==b)return true;
        var aa=AncestorDepths(s,a);
        var bb=AncestorDepths(s,b);
        // Direct ancestors are always excluded, regardless of generation distance.
        if(aa.ContainsKey(b)||bb.ContainsKey(a))return true;
        // Shared parents/grandparents exclude siblings, aunt/uncle relations and first cousins,
        // while distant descendants of an old founder may form new families.
        foreach(var (ancestor,depthA) in aa)
            if(depthA<=2&&bb.TryGetValue(ancestor,out var depthB)&&depthB<=2)return true;
        return false;
    }

    private static Dictionary<int,int> AncestorDepths(WorldState s,int person)
    {
        var result=new Dictionary<int,int>();
        var queue=new Queue<(int Id,int Depth)>();
        queue.Enqueue((person,0));
        while(queue.TryDequeue(out var current))
        {
            var family=s.Entities.Try<FamilyComponent>(current.Id);
            if(family is null)continue;
            foreach(var parent in new[]{family.Mother,family.Father}.Where(id=>id!=0))
            {
                var depth=current.Depth+1;
                if(result.TryGetValue(parent,out var previous)&&previous<=depth)continue;
                result[parent]=depth;
                queue.Enqueue((parent,depth));
            }
        }
        return result;
    }

    public static bool CanPartner(WorldState s,int a,int b)
    {
        if(!ActionRules.Adult(s,a)||!ActionRules.Adult(s,b)||Related(s,a,b))return false;
        if(!s.Entities.Get<HealthComponent>(a).Alive||!s.Entities.Get<HealthComponent>(b).Alive)return false;
        if(s.Entities.Get<FamilyComponent>(a).Partner!=0||s.Entities.Get<FamilyComponent>(b).Partner!=0)return false;
        var ab=RelationshipSystem.Get(s,a,b);
        var ba=RelationshipSystem.Get(s,b,a);
        return ab.Trust>.55f&&ba.Trust>.55f&&ab.Affection>.5f&&ba.Affection>.5f&&ab.Irritation<.2f&&ba.Irritation<.2f;
    }

    public static bool CanConceive(WorldState s,int a,int b)
    {
        if(!ActionRules.Adult(s,a)||!ActionRules.Adult(s,b)||Related(s,a,b))return false;
        var ea=s.Entities.Get<IdentityComponent>(a);
        var eb=s.Entities.Get<IdentityComponent>(b);
        if(ea.Sex==eb.Sex)return false;
        var mother=ea.Sex=="female"?a:b;
        var father=mother==a?b:a;
        var f=s.Entities.Get<FamilyComponent>(mother);
        if(f.Partner!=father||s.Entities.Get<FamilyComponent>(father).Partner!=mother||f.PregnancyDueTick.HasValue)return false;
        if(s.Clock.Age(s.Entities.Get<IdentityComponent>(mother).BirthDate)>45||s.Clock.Tick-f.LastBirthTick<1440*500)return false;
        foreach(var id in new[]{mother,father})
        {
            var needs=s.Entities.Get<NeedsComponent>(id);
            if(needs.Hunger>.45f||needs.Thirst>.45f||needs.Fatigue>.7f||s.Entities.Get<HealthComponent>(id).Value<65)return false;
            var position=s.Entities.Get<PositionComponent>(id).Tile;
            if(!EnvironmentQueries.Sheltered(s,position))return false;
            var other=id==mother?father:mother;
            var r=RelationshipSystem.Get(s,id,other);
            if(r.Trust<.6f||r.Affection<.55f||r.Irritation>.2f)return false;
        }
        var p=s.Entities.Get<PositionComponent>(mother).Tile;
        var q=s.Entities.Get<PositionComponent>(father).Tile;
        return s.Map[p].Room>0&&s.Map[p].Room==s.Map[q].Room&&p.Distance(q)<=1;
    }
}
