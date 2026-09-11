using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mod
{
    internal sealed partial class ConnectomeBrain
    {
        private sealed partial class RuntimeAsset
        {
            internal static RuntimeAsset Read(ByteReader reader)
            {
                ValidateHeader(reader);
                var counts = ReadCounts(reader);
                var dataset = ReadString16(reader);
                if (!string.Equals(dataset, "male-cns:v1.0", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("MaleCNS dataset identity is invalid.");
                }
                _ = ReadString32(reader);
                var tables = ReadTables(reader);
                var columns = ReadNeuronColumns(reader, counts.NeuronCount);
                var rows = ReadInt32Array(reader, checked(counts.NeuronCount + 1));
                ValidateRows(rows, counts.EdgeCount);
                var posts = ReadInt32Array(reader, counts.EdgeCount);
                var rawWeights = ReadUInt16Array(reader, counts.EdgeCount);
                var result = CreateAsset(counts, rows, posts, columns);
                PopulateWeights(result, rawWeights);
                PopulateMetadata(result, tables, columns);
                ReadRetina(reader, counts.RetinaCount, counts.NeuronCount);
                return result;
            }

            private static void ValidateHeader(ByteReader reader)
            {
                if (reader.ReadByte() != (byte)'F' || reader.ReadByte() != (byte)'L' || reader.ReadByte() != (byte)'Y' || reader.ReadByte() != (byte)'B')
                {
                    throw new InvalidDataException();
                }

                if (reader.ReadUInt32() != 1)
                {
                    throw new InvalidDataException();
                }
            }

            private static RuntimeCounts ReadCounts(ByteReader reader)
            {
                var neuronCount = reader.ReadUInt32();
                var edgeCount = reader.ReadUInt32();
                var retinaCount = reader.ReadUInt32();
                if (neuronCount != 176422 || edgeCount != 6287749 || retinaCount != 9619)
                {
                    throw new InvalidDataException("MaleCNS asset counts are invalid.");
                }

                return new RuntimeCounts(checked((int)neuronCount), checked((int)edgeCount), checked((int)retinaCount));
            }

            private static string[][] ReadTables(ByteReader reader)
            {
                var tables = new string[10][];
                for (var i = 0; i < tables.Length; i++)
                {
                    tables[i] = ReadTable(reader);
                }

                return tables;
            }

            private static RuntimeColumns ReadNeuronColumns(ByteReader reader, int count)
            {
                SkipInt64Array(reader, count);
                var types = ReadInt32Array(reader, count);
                var superclasses = ReadByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipUInt16Array(reader, count);
                SkipByteArray(reader, count);
                var signs = ReadSByteArray(reader, count);
                var sides = ReadByteArray(reader, count);
                SkipSByteArray(reader, count);
                SkipSByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipSingleArray(reader, checked(count * 3));
                SkipInt32Array(reader, count);
                SkipInt32Array(reader, count);
                return new RuntimeColumns(types, superclasses, signs, sides);
            }

            private static RuntimeAsset CreateAsset(RuntimeCounts counts, int[] rows, int[] posts, RuntimeColumns columns)
            {
                return new RuntimeAsset
                {
                    NeuronCount = counts.NeuronCount,
                    EdgeCount = counts.EdgeCount,
                    RowPointers = rows,
                    PostIndexes = posts,
                    Weights = new float[counts.EdgeCount],
                    NtSigns = columns.Signs,
                    Superclasses = new string[counts.NeuronCount],
                    Sides = new string[counts.NeuronCount]
                };
            }

            private static void PopulateWeights(RuntimeAsset asset, ushort[] rawWeights)
            {
                for (var i = 0; i < asset.EdgeCount; i++)
                {
                    var target = asset.PostIndexes[i];
                    if (target < 0 || target >= asset.NeuronCount)
                    {
                        throw new InvalidDataException();
                    }

                    asset.Weights[i] = Mathf.Clamp(rawWeights[i] * .275f * .65f / 7f, 0f, 4f);
                }
            }

            private static void PopulateMetadata(RuntimeAsset asset, string[][] tables, RuntimeColumns columns)
            {
                var buckets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < asset.NeuronCount; i++)
                {
                    var superclass = GetTableValue(tables[1], columns.SuperclassIndexes[i]);
                    asset.Superclasses[i] = superclass;
                    asset.Sides[i] = GetTableValue(tables[5], columns.SideIndexes[i]);
                    Add(buckets, "type:" + GetTableValue(tables[0], columns.TypeIndexes[i]), i);
                    Add(buckets, "superclass:" + superclass, i);
                    if (superclass == "ol_sensory")
                    {
                        Add(buckets, "sensory", i);
                    }

                    if (IsMotorSuperclass(superclass))
                    {
                        Add(buckets, "motor", i);
                    }
                }

                foreach (var pair in buckets)
                {
                    asset.populations[pair.Key] = pair.Value.ToArray();
                }
            }

            private static bool IsMotorSuperclass(string superclass)
            {
                return superclass.IndexOf("motor", StringComparison.OrdinalIgnoreCase) >= 0 || superclass == "descending_neuron";
            }

            private static void ReadRetina(ByteReader reader, int count, int neuronCount)
            {
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadInt32();
                    _ = reader.ReadByte();
                    _ = reader.ReadSByte();
                    _ = reader.ReadSByte();
                    _ = reader.ReadByte();
                    if (id < 0 || id >= neuronCount)
                    {
                        throw new InvalidDataException();
                    }
                }
            }

            private static void ValidateRows(int[] rows, int edgeCount)
            {
                if (rows.Length == 0 || rows[0] != 0 || rows[rows.Length - 1] != edgeCount)
                {
                    throw new InvalidDataException();
                }

                for (var i = 1; i < rows.Length; i++)
                {
                    if (rows[i] < rows[i - 1])
                    {
                        throw new InvalidDataException();
                    }
                }
            }

            private static void Add(Dictionary<string, List<int>> buckets, string key, int id)
            {
                if (String.IsNullOrEmpty(key))
                {
                    return;
                }

                if (!buckets.TryGetValue(key, out var list))
                {
                    list = [];
                    buckets[key] = list;
                }

                list.Add(id);
            }

            private static string GetTableValue(string[] values, int index)
            {
                if (index < 0 || index >= values.Length)
                {
                    throw new InvalidDataException();
                }

                return values[index];
            }

            private static string ReadString16(ByteReader reader)
            {
                return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
            }

            private static string ReadString32(ByteReader reader)
            {
                var length = reader.ReadUInt32();
                if (length > 8388608)
                {
                    throw new InvalidDataException();
                }

                return Encoding.UTF8.GetString(reader.ReadBytes((int)length));
            }

            private static string[] ReadTable(ByteReader reader)
            {
                var count = reader.ReadUInt16();
                var values = new string[count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = ReadString16(reader);
                }

                if (values.Length == 0 || values[0] != "")
                {
                    throw new InvalidDataException();
                }

                return values;
            }

            private static int[] ReadInt32Array(ByteReader reader, int count)
            {
                var values = new int[count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.ReadInt32();
                }

                return values;
            }

            private static byte[] ReadByteArray(ByteReader reader, int count)
            {
                return reader.ReadBytes(count);
            }

            private static sbyte[] ReadSByteArray(ByteReader reader, int count)
            {
                var values = new sbyte[count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.ReadSByte();
                }

                return values;
            }

            private static ushort[] ReadUInt16Array(ByteReader reader, int count)
            {
                var values = new ushort[count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.ReadUInt16();
                }

                return values;
            }

            private static void SkipInt64Array(ByteReader reader, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadInt64();
                }
            }

            private static void SkipInt32Array(ByteReader reader, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadInt32();
                }
            }

            private static void SkipUInt16Array(ByteReader reader, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadUInt16();
                }
            }

            private static void SkipByteArray(ByteReader reader, int count)
            {
                _ = reader.ReadBytes(count);
            }

            private static void SkipSByteArray(ByteReader reader, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadSByte();
                }
            }

            private static void SkipSingleArray(ByteReader reader, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadSingle();
                }
            }

            private sealed class RuntimeCounts
            {
                public RuntimeCounts(int neuronCount, int edgeCount, int retinaCount)
                {
                    NeuronCount = neuronCount;
                    EdgeCount = edgeCount;
                    RetinaCount = retinaCount;
                }

                public int NeuronCount { get; }
                public int EdgeCount { get; }
                public int RetinaCount { get; }
            }

            private sealed class RuntimeColumns
            {
                public RuntimeColumns(int[] typeIndexes, byte[] superclassIndexes, sbyte[] signs, byte[] sideIndexes)
                {
                    TypeIndexes = typeIndexes;
                    SuperclassIndexes = superclassIndexes;
                    Signs = signs;
                    SideIndexes = sideIndexes;
                }

                public int[] TypeIndexes { get; }
                public byte[] SuperclassIndexes { get; }
                public sbyte[] Signs { get; }
                public byte[] SideIndexes { get; }
            }

            internal sealed class ByteReader : IDisposable
            {
                private readonly Stream stream;
                public ByteReader(Stream stream) { this.stream = stream; }
                public byte ReadByte() { return ReadBytes(1)[0]; }
                public sbyte ReadSByte() { return unchecked((sbyte)ReadByte()); }
                public ushort ReadUInt16() { return BitConverter.ToUInt16(ReadBytes(2), 0); }
                public uint ReadUInt32() { return BitConverter.ToUInt32(ReadBytes(4), 0); }
                public int ReadInt32() { return BitConverter.ToInt32(ReadBytes(4), 0); }
                public long ReadInt64() { return BitConverter.ToInt64(ReadBytes(8), 0); }
                public float ReadSingle() { return BitConverter.ToSingle(ReadBytes(4), 0); }
                public byte[] ReadBytes(int count)
                {
                    var bytes = new byte[count];
                    var offset = 0;
                    while (offset < count)
                    {
                        var read = stream.Read(bytes, offset, count - offset);
                        if (read == 0) throw new EndOfStreamException();
                        offset += read;
                    }
                    return bytes;
                }
                public void Dispose()
                {
                    stream.Dispose();
                }
            }
        }
    }
}
