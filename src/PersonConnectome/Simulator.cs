namespace PersonConnectome;

public sealed class LifSimulator
{
    private readonly SparseGraph _graph;
    private readonly float[] _potential;
    private readonly int[] _refractory;
    private readonly SortedDictionary<long, List<(int Target, float Weight)>> _pending = new();
    public long Tick { get; private set; }
    public IReadOnlyList<float> Potentials => _potential;
    public LifSimulator(SparseGraph graph) { _graph = graph; _potential = new float[graph.Count]; _refractory = new int[graph.Count]; }
    public IReadOnlyList<int> Step(IReadOnlyDictionary<int, float>? externalInput = null, float leak = .08f)
    {
        var current = new float[_graph.Count];
        if (_pending.Remove(Tick, out var events)) foreach (var e in events) current[e.Target] += e.Weight;
        if (externalInput is not null) foreach (var input in externalInput.OrderBy(x => x.Key)) if (_graph.TryCompactId(input.Key, out var id)) current[id] += Numbers.Clamp(input.Value, -4f, 4f);
        var fired = new List<int>();
        for (var i = 0; i < _graph.Count; i++)
        {
            if (_refractory[i] > 0) { _refractory[i]--; _potential[i] = 0; continue; }
            _potential[i] = Numbers.Clamp(_potential[i] * (1f - Numbers.Clamp(leak, 0f, .99f)) + current[i], -8f, 8f);
            if (_potential[i] < _graph.Neurons[i].Threshold) continue;
            _potential[i] = 0; _refractory[i] = _graph.Neurons[i].RefractoryTicks; fired.Add(_graph.Neurons[i].ExternalId);
            foreach (var s in _graph.Outgoing(i))
            {
                _graph.TryCompactId(s.TargetExternalId, out var target);
                var due = Tick + s.DelayTicks;
                if (!_pending.TryGetValue(due, out var list)) _pending[due] = list = new();
                list.Add((target, s.Weight));
            }
        }
        Tick++; return fired;
    }
    public SimulatorState ExportState() => new(2, Tick, _potential.ToArray(), _refractory.ToArray(), _pending.SelectMany(pair => pair.Value.Select(item => new PendingEvent(pair.Key, item.Target, item.Weight))).ToArray());
    public bool TryImportState(SimulatorState state)
    {
        if (state.Version != 2 || state.Tick < 0 || state.Potential.Length != _potential.Length || state.Refractory.Length != _refractory.Length || state.Pending is null || state.Potential.Any(x => !float.IsFinite(x)) || state.Refractory.Any(x => x < 0 || x > 1024) || state.Pending.Length > 4096) return false;
        if (state.Pending.Any(item => item.DueTick < state.Tick || item.TargetCompactId < 0 || item.TargetCompactId >= _graph.Count || !float.IsFinite(item.Weight))) return false;
        Array.Copy(state.Potential, _potential, _potential.Length); Array.Copy(state.Refractory, _refractory, _refractory.Length); Tick = state.Tick; _pending.Clear();
        foreach (var item in state.Pending.OrderBy(item => item.DueTick).ThenBy(item => item.TargetCompactId)) { if (!_pending.TryGetValue(item.DueTick, out var list)) _pending[item.DueTick] = list = new(); list.Add((item.TargetCompactId, Numbers.Clamp(item.Weight, -4f, 4f))); }
        return true;
    }
}
public sealed record PendingEvent(long DueTick, int TargetCompactId, float Weight);
public sealed record SimulatorState(int Version, long Tick, float[] Potential, int[] Refractory, PendingEvent[] Pending);

public static class DemoCircuit
{
    // Sensory pain -> avoid -> left/right motor; a deliberately tiny illustrative circuit, not a biological connectome.
    public static SparseGraph Create() => new(new[] { new Neuron(10), new Neuron(20), new Neuron(30), new Neuron(40), new Neuron(50) }, new[] { new Synapse(10, 20, 1.2f), new Synapse(20, 30, 1.1f), new Synapse(20, 40, .8f), new Synapse(50, 20, -1f) });
}
