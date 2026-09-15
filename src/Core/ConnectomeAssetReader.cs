using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Mod.UI;

namespace Mod.Core
{
    internal sealed partial class LifBrain
    {
        private sealed partial class ModAsset
        {
            internal static ModAsset Read(ByteReader reader)
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

            private static ModCounts ReadCounts(ByteReader reader)
            {
                var neuronCount = reader.ReadUInt32();
                var edgeCount = reader.ReadUInt32();
                var retinaCount = reader.ReadUInt32();
                if (neuronCount != 176422 || edgeCount != 6287749 || retinaCount != 9619)
                {
                    throw new InvalidDataException("MaleCNS asset counts are invalid.");
                }

                return new ModCounts(checked((int)neuronCount), checked((int)edgeCount), checked((int)retinaCount));
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

            private static ModColumns ReadNeuronColumns(ByteReader reader, int count)
            {
                SkipInt64Array(reader, count);
                var types = ReadInt32Array(reader, count);
                var superclasses = ReadByteArray(reader, count);
                var classes = ReadByteArray(reader, count);
                var subclasses = ReadUInt16Array(reader, count);
                SkipByteArray(reader, count);
                var signs = ReadSByteArray(reader, count);
                var sides = ReadByteArray(reader, count);
                SkipSByteArray(reader, count);
                SkipSByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipByteArray(reader, count);
                SkipByteArray(reader, count);
                var nerves = ReadByteArray(reader, count);
                var soma = new float[checked(count * 3)];
                for (var i = 0; i < soma.Length; i++) soma[i] = reader.ReadSingle();
                SkipInt32Array(reader, count);
                SkipInt32Array(reader, count);
                return new ModColumns(types, new ModNeuronIndexes(superclasses, classes, subclasses, sides, nerves), signs, soma);
            }

            private static ModAsset CreateAsset(ModCounts counts, int[] rows, int[] posts, ModColumns columns)
            {
                return new ModAsset
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

            private static void PopulateWeights(ModAsset asset, ushort[] rawWeights)
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

            private static void PopulateMetadata(ModAsset asset, string[][] tables, ModColumns columns)
            {
                var buckets = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < asset.NeuronCount; i++)
                {
                    PopulateNeuronMetadata(asset, tables, columns, buckets, i);
                }

                foreach (var pair in buckets)
                {
                    asset.populations[pair.Key] = pair.Value.ToArray();
                }
                asset.BrainMap = BrainMapSample.Create(columns.Soma, asset.Superclasses);
            }

            private static void PopulateNeuronMetadata(ModAsset asset, string[][] tables, ModColumns columns,
                Dictionary<string, List<int>> buckets, int index)
            {
                var superclass = GetTableValue(tables[1], columns.Indexes.Superclass[index]);
                var neuronClass = GetTableValue(tables[2], columns.Indexes.Class[index]);
                var subclass = GetTableValue(tables[3], columns.Indexes.Subclass[index]);
                var type = GetTableValue(tables[0], columns.TypeIndexes[index]);
                var nerve = GetTableValue(tables[9], columns.Indexes.Nerve[index]);
                asset.Superclasses[index] = superclass;
                asset.Sides[index] = GetTableValue(tables[5], columns.Indexes.Side[index]);
                Add(buckets, "type:" + type, index);
                if (type.StartsWith("pC1_", StringComparison.Ordinal)) Add(buckets, "prefix:pC1_", index);
                Add(buckets, "class:" + neuronClass, index);
                Add(buckets, "subclass:" + subclass, index);
                AddSensoryMetadata(buckets, index, superclass, neuronClass, subclass, type, nerve);
                if (type.StartsWith("VS", StringComparison.Ordinal) && superclass == "visual_projection") Add(buckets, "input:optic-roll", index);
                if (superclass == "vnc_motor" && (subclass == "fl" || subclass == "ml" || subclass == "hl")) Add(buckets, "motor:leg", index);
                Add(buckets, "superclass:" + superclass, index);
                if (superclass == "ol_sensory" && neuronClass == "visual") Add(buckets, "input:light", index);
                if (superclass == "ol_sensory") Add(buckets, "sensory", index);
                if (IsMotorSuperclass(superclass)) Add(buckets, "motor", index);
            }

            private static void AddSensoryMetadata(Dictionary<string, List<int>> buckets, int index,
                string superclass, string neuronClass, string subclass, string type, string nerve)
            {
                if (!IsSensorySuperclass(superclass)) return;
                if (neuronClass == "mechanosensory" && subclass == "auditory") Add(buckets, "input:auditory", index);
                if (neuronClass == "mechanosensory" && subclass == "wind_gravity") Add(buckets, "input:gravity", index);
                if (type.StartsWith("BM_", StringComparison.Ordinal)) Add(buckets, "input:touch-head", index);
                if (neuronClass == "mechanosensory_tactile") AddTactileMetadata(buckets, index, nerve);
                if (neuronClass == "mechanosensory_proprioceptive") AddProprioceptiveMetadata(buckets, index, subclass);
                if (neuronClass == "thermosensory") AddThermalMetadata(buckets, index, type);
            }

            private static bool IsSensorySuperclass(string superclass) => superclass == "cb_sensory" || superclass == "vnc_sensory" || superclass == "sensory_ascending" || superclass == "sensory_descending";

            private static void AddTactileMetadata(Dictionary<string, List<int>> buckets, int index, string nerve)
            {
                Add(buckets, "input:tactile", index);
                if (nerve == "ProLN") Add(buckets, "input:touch-arms", index);
                if (nerve == "MesoLN" || nerve == "MetaLN") Add(buckets, "input:touch-legs", index);
                if (nerve == "PDMN" || nerve == "DMetaN" || nerve == "AbN3" || nerve == "AbN4") Add(buckets, "input:touch-core", index);
                if (nerve != "ProLN" && nerve != "MesoLN" && nerve != "MetaLN" && nerve != "PDMN" && nerve != "DMetaN" && nerve != "AbN3" && nerve != "AbN4") Add(buckets, "input:touch-other", index);
            }

            private static void AddProprioceptiveMetadata(Dictionary<string, List<int>> buckets, int index, string subclass)
            {
                if (subclass == "hair plate") Add(buckets, "input:joint-position", index);
                if (subclass == "chordotonal organ") Add(buckets, "input:joint-motion", index);
                if (subclass == "campaniform sensilla") Add(buckets, "input:joint-load", index);
            }

            private static void AddThermalMetadata(Dictionary<string, List<int>> buckets, int index, string type)
            {
                if (type == "TRN_VP2") Add(buckets, "input:hot", index);
                if (type == "TRN_VP3a" || type == "TRN_VP3b") Add(buckets, "input:cold", index);
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

            private sealed class ModCounts
            {
                public ModCounts(int neuronCount, int edgeCount, int retinaCount)
                {
                    NeuronCount = neuronCount;
                    EdgeCount = edgeCount;
                    RetinaCount = retinaCount;
                }

                public int NeuronCount { get; }
                public int EdgeCount { get; }
                public int RetinaCount { get; }
            }

            private sealed class ModColumns
            {
                public ModColumns(int[] typeIndexes, ModNeuronIndexes indexes, sbyte[] signs, float[] soma)
                {
                    TypeIndexes = typeIndexes;
                    Indexes = indexes;
                    Signs = signs;
                    Soma = soma;
                }

                public int[] TypeIndexes { get; }
                public ModNeuronIndexes Indexes { get; }
                public sbyte[] Signs { get; }
                public float[] Soma { get; }
            }

            private sealed class ModNeuronIndexes
            {
                public ModNeuronIndexes(byte[] superclass, byte[] @class, ushort[] subclass, byte[] side, byte[] nerve)
                {
                    Superclass = superclass;
                    Class = @class;
                    Subclass = subclass;
                    Side = side;
                    Nerve = nerve;
                }

                public byte[] Superclass { get; }
                public byte[] Class { get; }
                public ushort[] Subclass { get; }
                public byte[] Side { get; }
                public byte[] Nerve { get; }
            }

            internal sealed class ByteReader : IDisposable
            {
                private readonly Stream stream;
                private readonly byte[] scalar = new byte[8];
                public ByteReader(Stream stream) { this.stream = stream; }
                public byte ReadByte() { ReadInto(scalar, 1); return scalar[0]; }
                public sbyte ReadSByte() { return unchecked((sbyte)ReadByte()); }
                public ushort ReadUInt16() { ReadInto(scalar, 2); return BitConverter.ToUInt16(scalar, 0); }
                public uint ReadUInt32() { ReadInto(scalar, 4); return BitConverter.ToUInt32(scalar, 0); }
                public int ReadInt32() { ReadInto(scalar, 4); return BitConverter.ToInt32(scalar, 0); }
                public long ReadInt64() { ReadInto(scalar, 8); return BitConverter.ToInt64(scalar, 0); }
                public float ReadSingle() { ReadInto(scalar, 4); return BitConverter.ToSingle(scalar, 0); }
                public byte[] ReadBytes(int count)
                {
                    var bytes = new byte[count];
                    ReadInto(bytes, count);
                    return bytes;
                }
                private void ReadInto(byte[] bytes, int count)
                {
                    var offset = 0;
                    while (offset < count)
                    {
                        var read = stream.Read(bytes, offset, count - offset);
                        if (read == 0) throw new EndOfStreamException();
                        offset += read;
                    }
                }
                public void Dispose()
                {
                    stream.Dispose();
                }
            }
        }
    }
}
