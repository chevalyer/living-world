namespace LivingWorld.Simulation;

public sealed record PlanResult(List<ActionStep> Steps, float Cost, int Expanded);
public sealed class ForwardPlanner
{
    private sealed record IndexedAction(ActionOption Option, (int Index, int Minimum)[] Requires,
        (int Index, int Amount, bool Set)[] Effects);
    private sealed record SearchNode(int[] Values, GridPoint Position, SearchNode? Parent,
        IndexedAction? Via, int Depth, float Cost);
    private sealed class StateComparer : IEqualityComparer<SearchNode>
    {
        public bool Equals(SearchNode? a, SearchNode? b) => ReferenceEquals(a,b) ||
            (a is not null && b is not null && a.Position == b.Position && a.Values.AsSpan().SequenceEqual(b.Values));
        public int GetHashCode(SearchNode node)
        {
            unchecked
            {
                var hash = (node.Position.X * 397) ^ node.Position.Y;
                foreach (var value in node.Values) hash = hash * 31 + value;
                return hash;
            }
        }
    }
    public int NodeBudget { get; init; } = 180;
    public int MaxDepth { get; init; } = 8;

    public PlanResult? Find(PlanningContext context, IReadOnlyList<ActionOption> options, DesiredState desired)
    {
        foreach (var option in options)
        {
            double mass = 0, volume = 0;
            foreach (var effect in option.Effects)
            {
                if (effect.Set || !effect.Fact.StartsWith("item:", StringComparison.Ordinal)) continue;
                var item = context.Definitions.Items[effect.Fact[5..]];
                mass += item.Mass * effect.Amount;
                volume += item.Volume * effect.Amount;
            }
            option.MassDelta = (int)MathF.Ceiling((float)mass * 1000);
            option.VolumeDelta = (int)MathF.Ceiling((float)volume * 1000);
        }
        var facts = new HashSet<string>(StringComparer.Ordinal) { desired.Fact };
        var relevant = new HashSet<ActionOption>();
        for (var pass = 0; pass < MaxDepth; pass++)
        {
            var changed = false;
            foreach (var option in options)
            {
                if (!option.Effects.Any(e => e.Amount > 0 && facts.Contains(e.Fact)) || !relevant.Add(option)) continue;
                foreach (var need in option.Requires) facts.Add(need.Fact);
                changed = true;
            }
            if (!changed) break;
        }
        var selected = options.Where(relevant.Contains).ToArray();
        if (selected.Length == 0) return null;
        // Intern fact names once per search. Search states are compact immutable integer arrays.
        var indices = new Dictionary<string,int>(StringComparer.Ordinal);
        int Index(string fact)
        {
            if (!indices.TryGetValue(fact, out var index)) indices[fact] = index = indices.Count;
            return index;
        }
        var massIndex = Index("capacity.mass");
        var volumeIndex = Index("capacity.volume");
        var goalIndex = Index(desired.Fact);
        var actions = selected.Select(o => new IndexedAction(o,
            o.Requires.Select(r => (Index(r.Fact), r.Minimum)).ToArray(),
            o.Effects.Select(e => (Index(e.Fact), e.Amount, e.Set)).ToArray())).ToArray();
        var initial = context.InitialState();
        var values = new int[indices.Count];
        foreach (var (fact, index) in indices) values[index] = initial.Get(fact);
        var queue = new PriorityQueue<SearchNode,(float,int)>();
        var serial = 0;
        queue.Enqueue(new(values, context.Position, null, null, 0, 0), (0, serial++));
        var visited = new Dictionary<SearchNode,float>(new StateComparer());
        var expanded = 0;
        while (queue.TryDequeue(out var node, out _) && expanded++ < NodeBudget)
        {
            if (node.Values[goalIndex] > 0)
            {
                var path = new List<ActionOption>();
                for (var cursor = node; cursor.Via is not null; cursor = cursor.Parent!) path.Add(cursor.Via.Option);
                path.Reverse();
                return new(Compile(context,path),node.Cost,expanded);
            }
            if (node.Depth >= MaxDepth) continue;
            foreach (var action in actions)
            {
                var option = action.Option;
                if (node.Values[massIndex] < Math.Max(0,option.MassDelta) || node.Values[volumeIndex] < Math.Max(0,option.VolumeDelta)) continue;
                var applies = true;
                foreach (var need in action.Requires)
                    if (node.Values[need.Index] < need.Minimum) { applies = false; break; }
                if (!applies) continue;
                var next = (int[])node.Values.Clone();
                next[massIndex] -= option.MassDelta;
                next[volumeIndex] -= option.VolumeDelta;
                foreach (var effect in action.Effects) next[effect.Index] = effect.Set ? effect.Amount : next[effect.Index] + effect.Amount;
                var travel = option.GroundedAtActor ? 0 : Math.Max(0,node.Position.Distance(option.Step.Position)-option.Step.Range);
                var cost = node.Cost + option.Cost + travel + option.Risk * (1+context.Personality.Caution);
                var child = new SearchNode(next, option.GroundedAtActor ? node.Position : option.Step.Position,
                    node, action, node.Depth+1,cost);
                if (visited.TryGetValue(child,out var previous) && previous <= cost) continue;
                visited[child] = cost;
                queue.Enqueue(child,(cost,serial++));
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
