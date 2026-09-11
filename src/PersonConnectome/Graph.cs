using System.Collections.Frozen;

namespace PersonConnectome;

public sealed record Neuron(int ExternalId, float Threshold = 1f, int RefractoryTicks = 2);
public sealed record Synapse(int SourceExternalId, int TargetExternalId, float Weight, int DelayTicks = 1);
public sealed record NeuronMetadata(
    int ExternalId,
    string Type = "",
    string Superclass = "",
    string Class = "",
    string Subclass = "",
    string Neurotransmitter = "",
    string Side = "",
    string Region = "");

public enum PopulationRole : byte
{
    General = 0,
    Sensory = 1,
    Motor = 2
}

public sealed record PopulationMetadata(string Name, PopulationRole Role, int[] CompactNeuronIds);

public readonly record struct GraphEdge(int TargetCompactId, float Weight, int DelayTicks);

public interface IConnectomeGraph
{
    int Count { get; }
    Neuron GetNeuron(int compactId);
    NeuronMetadata GetMetadata(int compactId);
    bool TryCompactId(int externalId, out int compactId);
    ReadOnlySpan<GraphEdge> Outgoing(int compactId);
    IReadOnlyList<int> FindPopulation(string name);
}

public sealed class SparseGraph : IConnectomeGraph
{
    private readonly Dictionary<int, int> _compact = [];
    private readonly List<Neuron> _neurons = [];
    private readonly List<NeuronMetadata> _metadata = [];
    private readonly GraphEdge[][] _outgoing;
    private readonly FrozenDictionary<string, int[]> _populations;
    public IReadOnlyList<Neuron> Neurons => _neurons;
    public IReadOnlyList<NeuronMetadata> Metadata => _metadata;
    public int Count => _neurons.Count;
    public SparseGraph(IEnumerable<Neuron> neurons, IEnumerable<Synapse> synapses, int maximumNeurons = 512, int maximumSynapses = 4096, IEnumerable<NeuronMetadata>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(neurons);
        ArgumentNullException.ThrowIfNull(synapses);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumNeurons, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSynapses);
        AddNeurons(neurons, maximumNeurons);
        ApplyMetadata(metadata);
        _outgoing = BuildOutgoing(synapses, maximumSynapses);
        _populations = BuildPopulations();
    }

    private void AddNeurons(IEnumerable<Neuron> neurons, int maximumNeurons)
    {
        foreach (var neuron in neurons.OrderBy(item => item.ExternalId))
        {
            if (_neurons.Count >= maximumNeurons)
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumNeurons, _neurons.Count, nameof(maximumNeurons));
            }

            if (!_compact.TryAdd(neuron.ExternalId, _neurons.Count))
            {
                throw new ArgumentException("External neuron identifiers must be unique.");
            }

            _neurons.Add(NormalizeNeuron(neuron));
            _metadata.Add(new NeuronMetadata(neuron.ExternalId));
        }
    }

    private static Neuron NormalizeNeuron(Neuron neuron)
    {
        return neuron with
        {
            Threshold = Math.Max(.001f, Numbers.Finite(neuron.Threshold, 1f)),
            RefractoryTicks = Math.Max(0, neuron.RefractoryTicks)
        };
    }

    private void ApplyMetadata(IEnumerable<NeuronMetadata>? metadata)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (var item in metadata)
        {
            if (_compact.TryGetValue(item.ExternalId, out var compactId))
            {
                _metadata[compactId] = NormalizeMetadata(item);
            }
        }
    }

    private static NeuronMetadata NormalizeMetadata(NeuronMetadata metadata)
    {
        return metadata with
        {
            Type = metadata.Type ?? "",
            Superclass = metadata.Superclass ?? "",
            Class = metadata.Class ?? "",
            Subclass = metadata.Subclass ?? "",
            Neurotransmitter = metadata.Neurotransmitter ?? "",
            Side = metadata.Side ?? "",
            Region = metadata.Region ?? ""
        };
    }

    private GraphEdge[][] BuildOutgoing(IEnumerable<Synapse> synapses, int maximumSynapses)
    {
        var outgoing = Enumerable.Range(0, _neurons.Count).Select(static _ => new List<GraphEdge>()).ToArray();
        var accepted = 0;
        foreach (var synapse in synapses.OrderBy(item => item.SourceExternalId).ThenBy(item => item.TargetExternalId))
        {
            if (!_compact.TryGetValue(synapse.SourceExternalId, out var source) || !_compact.TryGetValue(synapse.TargetExternalId, out var target))
            {
                continue;
            }

            if (accepted >= maximumSynapses)
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumSynapses, accepted, nameof(maximumSynapses));
            }

            accepted++;
            outgoing[source].Add(new GraphEdge(target, Numbers.Clamp(synapse.Weight, -4f, 4f), Math.Clamp(synapse.DelayTicks, 1, 32)));
        }

        return outgoing.Select(edges => edges.ToArray()).ToArray();
    }
    public bool TryCompactId(int externalId, out int compactId) => _compact.TryGetValue(externalId, out compactId);
    public Neuron GetNeuron(int compactId) => _neurons[compactId];
    public NeuronMetadata GetMetadata(int compactId) => _metadata[compactId];
    public ReadOnlySpan<GraphEdge> Outgoing(int compactId) => _outgoing[compactId];
    public IReadOnlyList<int> FindPopulation(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && _populations.TryGetValue(name, out var ids) ? ids : [];
    }

    private FrozenDictionary<string, int[]> BuildPopulations()
    {
        var buckets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var compactId = 0; compactId < _metadata.Count; compactId++)
        {
            var metadata = _metadata[compactId];
            AddPopulation(buckets, "type:" + metadata.Type, compactId);
            AddPopulation(buckets, "superclass:" + metadata.Superclass, compactId);
            AddPopulation(buckets, "class:" + metadata.Class, compactId);
            AddPopulation(buckets, "subclass:" + metadata.Subclass, compactId);
            AddPopulation(buckets, "nt:" + metadata.Neurotransmitter, compactId);
            AddPopulation(buckets, "side:" + metadata.Side, compactId);
            if (string.Equals(metadata.Superclass, "ol_sensory", StringComparison.OrdinalIgnoreCase))
            {
                AddPopulation(buckets, "sensory", compactId);
            }

            if (metadata.Superclass.Contains("motor", StringComparison.OrdinalIgnoreCase) || string.Equals(metadata.Superclass, "descending_neuron", StringComparison.OrdinalIgnoreCase))
            {
                AddPopulation(buckets, "motor", compactId);
            }
        }

        return buckets.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static void AddPopulation(Dictionary<string, List<int>> buckets, string name, int compactId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!buckets.TryGetValue(name, out var ids))
        {
            ids = [];
            buckets[name] = ids;
        }

        ids.Add(compactId);
    }
}
