namespace PersonConnectome;

public sealed record Neuron(int ExternalId, float Threshold = 1f, int RefractoryTicks = 2);
public sealed record Synapse(int SourceExternalId, int TargetExternalId, float Weight, int DelayTicks = 1);

public sealed class SparseGraph
{
    private readonly Dictionary<int, int> _compact = new();
    private readonly List<Neuron> _neurons = new();
    private readonly List<Synapse>[] _outgoing;
    public IReadOnlyList<Neuron> Neurons => _neurons;
    public int Count => _neurons.Count;
    public SparseGraph(IEnumerable<Neuron> neurons, IEnumerable<Synapse> synapses, int maximumNeurons = 512, int maximumSynapses = 4096)
    {
        foreach (var neuron in neurons.OrderBy(n => n.ExternalId))
        {
            if (_neurons.Count >= maximumNeurons) throw new ArgumentOutOfRangeException(nameof(neurons));
            if (!_compact.TryAdd(neuron.ExternalId, _neurons.Count)) throw new ArgumentException("External neuron identifiers must be unique.");
            _neurons.Add(neuron with { Threshold = Math.Max(.001f, Numbers.Finite(neuron.Threshold, 1f)), RefractoryTicks = Math.Max(0, neuron.RefractoryTicks) });
        }
        _outgoing = Enumerable.Range(0, _neurons.Count).Select(_ => new List<Synapse>()).ToArray();
        var accepted = 0;
        foreach (var synapse in synapses.OrderBy(s => s.SourceExternalId).ThenBy(s => s.TargetExternalId))
        {
            if (accepted++ >= maximumSynapses) throw new ArgumentOutOfRangeException(nameof(synapses));
            if (!_compact.TryGetValue(synapse.SourceExternalId, out var source) || !_compact.ContainsKey(synapse.TargetExternalId)) continue;
            _outgoing[source].Add(synapse with { Weight = Numbers.Clamp(synapse.Weight, -4f, 4f), DelayTicks = Math.Clamp(synapse.DelayTicks, 1, 32) });
        }
    }
    public bool TryCompactId(int externalId, out int compactId) => _compact.TryGetValue(externalId, out compactId);
    public IReadOnlyList<Synapse> Outgoing(int compactId) => _outgoing[compactId];
}
