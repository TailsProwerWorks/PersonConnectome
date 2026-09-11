using System.Collections.Frozen;

namespace PersonConnectome;

public sealed record ConnectomeLoadOptions(
    int MaximumNeurons = 1_000_000,
    int MaximumEdges = 50_000_000,
    long MaximumUncompressedBytes = 1_000_000_000,
    float SynapseMillivolts = .275f,
    float GlobalGain = .65f,
    float ThresholdDeltaMillivolts = 7f,
    string? ExpectedSha256 = null);

public sealed class FlybConnectomeGraph : IConnectomeGraph
{
    private readonly Neuron[] _neurons;
    private readonly NeuronMetadata[] _metadata;
    private readonly int[] _rowPointers;
    private readonly GraphEdge[] _edges;
    private readonly FrozenDictionary<int, int> _compactByExternalId;
    private readonly FrozenDictionary<string, int[]> _populations;

    internal FlybConnectomeGraph(
        string dataset,
        string metadataJson,
        Neuron[] neurons,
        NeuronMetadata[] metadata,
        int[] rowPointers,
        GraphEdge[] edges,
        IReadOnlyList<RetinaEntry> retina)
    {
        Dataset = dataset;
        MetadataJson = metadataJson;
        _neurons = neurons;
        _metadata = metadata;
        _rowPointers = rowPointers;
        _edges = edges;
        Retina = retina;
        var compactByExternalId = new Dictionary<int, int>(neurons.Length);
        for (var i = 0; i < neurons.Length; i++)
        {
            compactByExternalId.Add(neurons[i].ExternalId, i);
        }
        _compactByExternalId = compactByExternalId.ToFrozenDictionary();
        _populations = BuildPopulations(metadata, retina);
    }

    public string Dataset { get; }
    public string MetadataJson { get; }
    public IReadOnlyList<RetinaEntry> Retina { get; }
    public int Count => _neurons.Length;
    public int EdgeCount => _edges.Length;
    public IReadOnlyList<Neuron> Neurons => _neurons;
    public IReadOnlyList<NeuronMetadata> Metadata => _metadata;

    public Neuron GetNeuron(int compactId) => _neurons[compactId];
    public NeuronMetadata GetMetadata(int compactId) => _metadata[compactId];
    public bool TryCompactId(int externalId, out int compactId) => _compactByExternalId.TryGetValue(externalId, out compactId);
    public ReadOnlySpan<GraphEdge> Outgoing(int compactId)
    {
        var start = _rowPointers[compactId];
        return _edges.AsSpan(start, _rowPointers[compactId + 1] - start);
    }
    public IReadOnlyList<int> FindPopulation(string name) => _populations.TryGetValue(name, out var ids) ? ids : [];

    private static FrozenDictionary<string, int[]> BuildPopulations(NeuronMetadata[] metadata, IReadOnlyList<RetinaEntry> retina)
    {
        var buckets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, int compactId)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!buckets.TryGetValue(key, out var ids)) buckets[key] = ids = [];
            ids.Add(compactId);
        }

        for (var i = 0; i < metadata.Length; i++)
        {
            var item = metadata[i];
            Add("type:" + item.Type, i);
            Add("superclass:" + item.Superclass, i);
            Add("class:" + item.Class, i);
            Add("subclass:" + item.Subclass, i);
            Add("nt:" + item.Neurotransmitter, i);
            Add("side:" + item.Side, i);
            if (string.Equals(item.Superclass, "ol_sensory", StringComparison.OrdinalIgnoreCase)) Add("sensory", i);
            if (string.Equals(item.Superclass, "descending_neuron", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Superclass, "vnc_motor", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Superclass, "cb_motor", StringComparison.OrdinalIgnoreCase)) Add("motor", i);
        }
        foreach (var entry in retina) Add("retina:" + entry.Kind, entry.CompactNeuronId);
        return buckets.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record RetinaEntry(int CompactNeuronId, int SideIndex, int Hex1, int Hex2, int Kind);

public sealed record ConnectomeLoadFailure(string Code, string Message);
