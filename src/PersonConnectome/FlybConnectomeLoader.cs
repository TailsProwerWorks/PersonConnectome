using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PersonConnectome;

public static class FlybConnectomeLoader
{
    private const int CurrentVersion = 1;
    private static ReadOnlySpan<byte> Magic => "FLYB"u8;

    public static bool TryLoad(Stream compressedAsset, ConnectomeLoadOptions? options, out FlybConnectomeGraph? graph, out ConnectomeLoadFailure? failure)
    {
        ArgumentNullException.ThrowIfNull(compressedAsset);
        graph = null;
        failure = null;
        options ??= new ConnectomeLoadOptions();
        if (!HasValidOptions(options, out var optionsFailure))
        {
            failure = new ConnectomeLoadFailure("options", optionsFailure);
            return false;
        }

        if (!compressedAsset.CanRead)
        {
            failure = new ConnectomeLoadFailure("stream", "The connectome asset stream is not readable.");
            return false;
        }

        try
        {
            ValidateChecksum(compressedAsset, options.ExpectedSha256);
            using var gzip = new GZipStream(compressedAsset, CompressionMode.Decompress, leaveOpen: false);
            using var limited = new CountingReadStream(gzip, options.MaximumUncompressedBytes);
            using var reader = new BinaryReader(limited, Encoding.UTF8, leaveOpen: false);
            graph = ReadGraph(reader, options);
            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException or OverflowException or ArgumentException)
        {
            failure = new ConnectomeLoadFailure("invalid-asset", ex.Message);
            return false;
        }
    }

    private static void ValidateChecksum(Stream compressedAsset, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return;
        if (!compressedAsset.CanSeek) throw new InvalidDataException("checksum validation requires a seekable asset stream");
        var originalPosition = compressedAsset.Position;
        string actual;
        try
        {
            actual = Convert.ToHexString(SHA256.HashData(compressedAsset));
        }
        finally
        {
            compressedAsset.Position = originalPosition;
        }

        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("asset checksum does not match the expected SHA-256");
    }

    private static bool HasValidOptions(ConnectomeLoadOptions options, out string failure)
    {
        if (options.MaximumNeurons <= 0 || options.MaximumEdges < 0 || options.MaximumUncompressedBytes <= 0)
        {
            failure = "loader limits must be positive, with zero allowed only for the edge limit";
            return false;
        }

        if (!float.IsFinite(options.SynapseMillivolts) || !float.IsFinite(options.GlobalGain) || !float.IsFinite(options.ThresholdDeltaMillivolts) || options.ThresholdDeltaMillivolts <= 0f)
        {
            failure = "synapse normalization values must be finite and the threshold delta must be positive";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static FlybConnectomeGraph ReadGraph(BinaryReader reader, ConnectomeLoadOptions options)
    {
        ValidateHeader(reader);
        var counts = ReadCounts(reader, options);
        var dataset = ReadString16(reader);
        var metadataJson = ReadString32(reader, 8 * 1024 * 1024);
        var tables = ReadTables(reader);
        var columns = ReadNeuronColumns(reader, counts.NeuronCount);
        var rowPointers = ReadInt32Array(reader, checked(counts.NeuronCount + 1));
        ValidateRowPointers(rowPointers, counts.EdgeCount);
        var edges = ReadEdges(reader, rowPointers, columns.NtSign, counts.EdgeCount, counts.NeuronCount, options);
        var retina = ReadRetina(reader, counts.RetinaCount, counts.NeuronCount);
        var (neurons, metadata) = BuildNeurons(columns, tables);
        return new FlybConnectomeGraph(dataset, metadataJson, neurons, metadata, rowPointers, edges, retina);
    }

    private static void ValidateHeader(BinaryReader reader)
    {
        var magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("FLYB magic is invalid");
        if (reader.ReadUInt32() != CurrentVersion) throw new InvalidDataException("FLYB version is unsupported");
    }

    private static AssetCounts ReadCounts(BinaryReader reader, ConnectomeLoadOptions options) => new(
        CheckedCount(reader.ReadUInt32(), options.MaximumNeurons, "neurons"),
        CheckedCount(reader.ReadUInt32(), options.MaximumEdges, "edges"),
        CheckedCount(reader.ReadUInt32(), Math.Max(1, options.MaximumNeurons), "retina entries"));

    private static string[][] ReadTables(BinaryReader reader)
    {
        var tables = new string[10][];
        for (var i = 0; i < tables.Length; i++)
        {
            tables[i] = ReadTable(reader);
        }
        return tables;
    }

    private static NeuronColumns ReadNeuronColumns(BinaryReader reader, int neuronCount)
    {
        var bodyIds = ReadInt64Array(reader, neuronCount);
        var typeIdx = ReadInt32Array(reader, neuronCount);
        var superclassIdx = ReadByteArray(reader, neuronCount);
        var classIdx = ReadByteArray(reader, neuronCount);
        var subclassIdx = ReadUInt16Array(reader, neuronCount);
        var ntIdx = ReadByteArray(reader, neuronCount);
        var ntSign = ReadSByteArray(reader, neuronCount);
        var sideIdx = ReadByteArray(reader, neuronCount);
        _ = ReadSByteArray(reader, neuronCount);
        _ = ReadSByteArray(reader, neuronCount);
        _ = ReadByteArray(reader, neuronCount);
        _ = ReadByteArray(reader, neuronCount);
        var neuromereIdx = ReadByteArray(reader, neuronCount);
        _ = ReadByteArray(reader, neuronCount);
        _ = ReadSingleArray(reader, checked(neuronCount * 3));
        _ = ReadInt32Array(reader, neuronCount);
        _ = ReadInt32Array(reader, neuronCount);
        return new NeuronColumns(bodyIds, typeIdx, superclassIdx, classIdx, subclassIdx, ntIdx, ntSign, sideIdx, neuromereIdx);
    }

    private static GraphEdge[] ReadEdges(BinaryReader reader, int[] rowPointers, sbyte[] ntSign, int edgeCount, int neuronCount, ConnectomeLoadOptions options)
    {
        var postIdx = ReadInt32Array(reader, edgeCount);
        var weights = ReadUInt16Array(reader, edgeCount);
        var edges = new GraphEdge[edgeCount];
        for (var i = 0; i < edges.Length; i++)
        {
            if ((uint)postIdx[i] >= (uint)neuronCount) throw new InvalidDataException("edge target is outside the neuron table");
            var sign = ntSign[FindSource(rowPointers, i)];
            var weight = weights[i] * options.SynapseMillivolts * options.GlobalGain / Math.Max(.001f, options.ThresholdDeltaMillivolts) * (sign == 0 ? 1 : sign);
            edges[i] = new GraphEdge(postIdx[i], Numbers.Clamp(weight, -4f, 4f), 1);
        }
        return edges;
    }

    private static RetinaEntry[] ReadRetina(BinaryReader reader, int retinaCount, int neuronCount)
    {
        var retina = new RetinaEntry[retinaCount];
        for (var i = 0; i < retina.Length; i++)
        {
            var compactId = reader.ReadInt32();
            var side = reader.ReadByte();
            var hex1 = reader.ReadSByte();
            var hex2 = reader.ReadSByte();
            var kind = reader.ReadByte();
            if ((uint)compactId >= (uint)neuronCount) throw new InvalidDataException("retina neuron is outside the neuron table");
            retina[i] = new RetinaEntry(compactId, side, hex1, hex2, kind);
        }
        return retina;
    }

    private static (Neuron[] Neurons, NeuronMetadata[] Metadata) BuildNeurons(NeuronColumns columns, string[][] tables)
    {
        var neurons = new Neuron[columns.BodyIds.Length];
        var metadata = new NeuronMetadata[columns.BodyIds.Length];
        for (var i = 0; i < columns.BodyIds.Length; i++)
        {
            var bodyId = columns.BodyIds[i];
            if (bodyId < int.MinValue || bodyId > int.MaxValue) throw new InvalidDataException("body ID does not fit the runtime external-ID type");
            if (i > 0 && bodyId <= columns.BodyIds[i - 1]) throw new InvalidDataException("body IDs are not strictly sorted");
            var type = TableValue(tables[0], columns.TypeIdx[i]);
            var superclass = TableValue(tables[1], columns.SuperclassIdx[i]);
            var @class = TableValue(tables[2], columns.ClassIdx[i]);
            var subclass = TableValue(tables[3], columns.SubclassIdx[i]);
            var neurotransmitter = TableValue(tables[4], columns.NtIdx[i]);
            var side = TableValue(tables[5], columns.SideIdx[i]);
            var region = TableValue(tables[8], columns.NeuromereIdx[i]);
            neurons[i] = new Neuron((int)bodyId, 1f, 5);
            metadata[i] = new NeuronMetadata((int)bodyId, type, superclass, @class, subclass, neurotransmitter, side, region);
        }
        return (neurons, metadata);
    }

    private static int FindSource(int[] rowPointers, int edgeIndex)
    {
        var low = 0;
        var high = rowPointers.Length - 1;
        while (low + 1 < high)
        {
            var middle = low + ((high - low) / 2);
            if (rowPointers[middle] <= edgeIndex) low = middle; else high = middle;
        }
        return low;
    }

    private static int CheckedCount(uint value, int maximum, string label)
    {
        if (value > int.MaxValue || value > maximum) throw new InvalidDataException($"{label} exceed the configured limit");
        return (int)value;
    }
    private static string TableValue(string[] table, int index) => (uint)index < (uint)table.Length ? table[index] : throw new InvalidDataException("metadata table index is invalid");
    private static string ReadString16(BinaryReader reader) => ReadString(reader, reader.ReadUInt16(), 64 * 1024);
    private static string ReadString32(BinaryReader reader, int maximum) => ReadString(reader, checked((int)reader.ReadUInt32()), maximum);
    private static string ReadString(BinaryReader reader, int bytes, int maximum)
    {
        if (bytes < 0 || bytes > maximum) throw new InvalidDataException("string is too large");
        return Encoding.UTF8.GetString(ReadExact(reader, bytes));
    }
    private static string[] ReadTable(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        var table = new string[count];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = ReadString16(reader);
        }
        if (table.Length == 0 || table[0].Length != 0) throw new InvalidDataException("string table index zero must be empty");
        return table;
    }
    private static byte[] ReadExact(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException();
        return bytes;
    }
    private static long[] ReadInt64Array(BinaryReader reader, int count)
    {
        var values = new long[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadInt64();
        }
        return values;
    }
    private static int[] ReadInt32Array(BinaryReader reader, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadInt32();
        }
        return values;
    }
    private static ushort[] ReadUInt16Array(BinaryReader reader, int count)
    {
        var values = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadUInt16();
        }
        return values;
    }
    private static byte[] ReadByteArray(BinaryReader reader, int count) => ReadExact(reader, count);
    private static sbyte[] ReadSByteArray(BinaryReader reader, int count)
    {
        var values = new sbyte[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadSByte();
        }
        return values;
    }
    private static float[] ReadSingleArray(BinaryReader reader, int count)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = reader.ReadSingle();
        }
        return values;
    }
    private static void ValidateRowPointers(int[] rowPointers, int edgeCount)
    {
        if (rowPointers.Length == 0 || rowPointers[0] != 0 || rowPointers[^1] != edgeCount) throw new InvalidDataException("CSR row pointers do not cover the edge table");
        for (var i = 1; i < rowPointers.Length; i++)
        {
            if (rowPointers[i] < rowPointers[i - 1]) throw new InvalidDataException("CSR row pointers are not monotonic");
        }
    }
}

internal sealed record AssetCounts(int NeuronCount, int EdgeCount, int RetinaCount);

internal sealed record NeuronColumns(
    long[] BodyIds,
    int[] TypeIdx,
    byte[] SuperclassIdx,
    byte[] ClassIdx,
    ushort[] SubclassIdx,
    byte[] NtIdx,
    sbyte[] NtSign,
    byte[] SideIdx,
    byte[] NeuromereIdx);

internal sealed class CountingReadStream(Stream inner, long maximum) : Stream
{
    private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public long BytesRead { get; private set; }
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Count(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        Count(read);
        return read;
    }

    private void Count(int read)
    {
        BytesRead += read;
        if (BytesRead > maximum)
        {
            throw new InvalidDataException("uncompressed asset exceeds the configured limit");
        }
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
