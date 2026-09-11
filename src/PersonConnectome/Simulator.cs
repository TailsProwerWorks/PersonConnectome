namespace PersonConnectome;

public sealed class LifSimulator
{
    private const int StateVersion = 2;
    private const int MaximumPendingEvents = 4096;
    private readonly IConnectomeGraph _graph;
    private readonly float[] _potential;
    private readonly int[] _refractory;
    private readonly SortedDictionary<long, List<(int Target, float Weight)>> _pending = [];

    public long Tick { get; private set; }
    public IReadOnlyList<float> Potentials => _potential;
    public IConnectomeGraph Graph => _graph;
    public IReadOnlyList<int> LastFiredCompactIds { get; private set; } = [];

    public LifSimulator(IConnectomeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _potential = new float[graph.Count];
        _refractory = new int[graph.Count];
    }

    public IReadOnlyList<int> Step(IReadOnlyDictionary<int, float>? externalInput = null, float leak = .08f)
    {
        return StepCompact(ConvertInputToCompactIds(externalInput), leak);
    }

    public IReadOnlyList<int> StepCompact(IReadOnlyDictionary<int, float>? compactInput = null, float leak = .08f)
    {
        var current = BuildCurrentInput(compactInput);
        var clampedLeak = Numbers.Clamp(leak, 0f, .99f);
        List<int> fired = [];
        List<int> firedCompact = [];
        for (var i = 0; i < _graph.Count; i++)
        {
            ProcessNeuron(i, current, clampedLeak, fired, firedCompact);
        }

        Tick++;
        LastFiredCompactIds = firedCompact;
        return fired;
    }

    private Dictionary<int, float>? ConvertInputToCompactIds(IReadOnlyDictionary<int, float>? externalInput)
    {
        if (externalInput is null)
        {
            return null;
        }

        Dictionary<int, float> compactInput = [];
        foreach (var input in externalInput)
        {
            if (_graph.TryCompactId(input.Key, out var compactId))
            {
                compactInput[compactId] = input.Value;
            }
        }

        return compactInput;
    }

    private float[] BuildCurrentInput(IReadOnlyDictionary<int, float>? compactInput)
    {
        var current = new float[_graph.Count];
        if (_pending.Remove(Tick, out var events))
        {
            foreach (var pending in events)
            {
                current[pending.Target] += pending.Weight;
            }
        }

        if (compactInput is not null)
        {
            AddCompactInputs(current, compactInput);
        }

        return current;
    }

    private void AddCompactInputs(float[] current, IReadOnlyDictionary<int, float> compactInput)
    {
        foreach (var input in compactInput.OrderBy(item => item.Key))
        {
            if (input.Key >= 0 && input.Key < _graph.Count)
            {
                current[input.Key] += Numbers.Clamp(input.Value, -4f, 4f);
            }
        }
    }

    private void ProcessNeuron(int compactId, float[] current, float clampedLeak, List<int> fired, List<int> firedCompact)
    {
        if (_refractory[compactId] > 0)
        {
            _refractory[compactId]--;
            _potential[compactId] = 0;
            return;
        }

        _potential[compactId] = Numbers.Clamp(
            _potential[compactId] * (1f - clampedLeak) + current[compactId],
            -8f,
            8f);
        var neuron = _graph.GetNeuron(compactId);
        if (_potential[compactId] < neuron.Threshold)
        {
            return;
        }

        _potential[compactId] = 0;
        _refractory[compactId] = neuron.RefractoryTicks;
        fired.Add(neuron.ExternalId);
        firedCompact.Add(compactId);
        ScheduleOutgoing(compactId);
    }

    private void ScheduleOutgoing(int sourceCompactId)
    {
        foreach (var synapse in _graph.Outgoing(sourceCompactId))
        {
            var dueTick = Tick + synapse.DelayTicks;
            if (!_pending.TryGetValue(dueTick, out var events))
            {
                events = [];
                _pending[dueTick] = events;
            }

            events.Add((synapse.TargetCompactId, synapse.Weight));
        }
    }

    public SimulatorState ExportState()
    {
        List<PendingEvent> pendingEvents = [];
        foreach (var pair in _pending)
        {
            foreach (var (Target, Weight) in pair.Value)
            {
                pendingEvents.Add(new(pair.Key, Target, Weight));
            }
        }

        return new(StateVersion, Tick, _potential.ToArray(), _refractory.ToArray(), pendingEvents.ToArray());
    }

    public bool TryImportState(SimulatorState state)
    {
        if (!IsValidState(state))
        {
            return false;
        }

        Array.Copy(state.Potential, _potential, _potential.Length);
        Array.Copy(state.Refractory, _refractory, _refractory.Length);
        Tick = state.Tick;
        _pending.Clear();
        ImportPendingEvents(state.Pending);
        return true;
    }

    private bool IsValidState(SimulatorState state)
    {
        if (state is null || state.Version != StateVersion || state.Tick < 0)
        {
            return false;
        }

        if (state.Potential is null || state.Refractory is null || state.Pending is null)
        {
            return false;
        }

        if (state.Potential.Length != _potential.Length || state.Refractory.Length != _refractory.Length || state.Pending.Length > MaximumPendingEvents)
        {
            return false;
        }

        if (!HasValidPotentials(state.Potential) || !HasValidRefractory(state.Refractory))
        {
            return false;
        }

        return HasValidPendingEvents(state.Pending, state.Tick);
    }

    private static bool HasValidPotentials(IEnumerable<float> values)
    {
        return values.All(float.IsFinite);
    }

    private static bool HasValidRefractory(IEnumerable<int> values)
    {
        return values.All(value => value >= 0 && value <= 1024);
    }

    private bool HasValidPendingEvents(IEnumerable<PendingEvent> events, long tick)
    {
        foreach (var pendingEvent in events)
        {
            if (pendingEvent.DueTick < tick || pendingEvent.TargetCompactId < 0 || pendingEvent.TargetCompactId >= _graph.Count || !float.IsFinite(pendingEvent.Weight))
            {
                return false;
            }
        }

        return true;
    }

    private void ImportPendingEvents(IEnumerable<PendingEvent> events)
    {
        foreach (var pendingEvent in events.OrderBy(item => item.DueTick).ThenBy(item => item.TargetCompactId))
        {
            if (!_pending.TryGetValue(pendingEvent.DueTick, out var pendingList))
            {
                pendingList = [];
                _pending[pendingEvent.DueTick] = pendingList;
            }

            pendingList.Add((pendingEvent.TargetCompactId, Numbers.Clamp(pendingEvent.Weight, -4f, 4f)));
        }
    }
}

public sealed record PendingEvent(long DueTick, int TargetCompactId, float Weight);
public sealed record SimulatorState(int Version, long Tick, float[] Potential, int[] Refractory, PendingEvent[] Pending);
