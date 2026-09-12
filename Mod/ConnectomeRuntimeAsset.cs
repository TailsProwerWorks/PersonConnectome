using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Mod
{
    internal sealed partial class ConnectomeBrain
    {
        private sealed partial class RuntimeAsset : IRuntimeConnectomeAsset
        {
            public int NeuronCount, EdgeCount;
            public int[] RowPointers, PostIndexes;
            public float[] Weights;
            public sbyte[] NtSigns;
            public string[] Superclasses, Sides;
            public BrainMapSample BrainMap;
            internal readonly Dictionary<string, int[]> populations = new(StringComparer.OrdinalIgnoreCase);
            public IReadOnlyList<int> Population(string name)
            {
                return populations.TryGetValue(name, out var ids) ? ids : [];
            }

            int IRuntimeConnectomeAsset.NeuronCount => NeuronCount;
            int IRuntimeConnectomeAsset.EdgeCount => EdgeCount;
            int IRuntimeConnectomeAsset.OutgoingStart(int neuronId) => RowPointers[neuronId];
            int IRuntimeConnectomeAsset.OutgoingEnd(int neuronId) => RowPointers[neuronId + 1];
            int IRuntimeConnectomeAsset.TargetAt(int edgeIndex) => PostIndexes[edgeIndex];
            float IRuntimeConnectomeAsset.WeightAt(int edgeIndex) => Weights[edgeIndex];
            sbyte IRuntimeConnectomeAsset.SignAt(int neuronId) => NtSigns[neuronId];
            string IRuntimeConnectomeAsset.SuperclassAt(int neuronId) => Superclasses[neuronId];
            string IRuntimeConnectomeAsset.SideAt(int neuronId) => Sides[neuronId];
            void IRuntimeConnectomeAsset.SetPopulation(string name, int[] ids) => populations[name] = ids ?? [];

            public static bool TryLoad(out RuntimeAsset asset, out string status)
            {
                asset = null;
                try
                {
                    var texture = ModAPI.LoadTexture("connectome/malecns-v1.0.png");
                    if (texture == null)
                    {
                        status = "MaleCNS v1.0 asset missing";
                        return false;
                    }

                    var compressed = ReadTexturePayload(texture);
                    using var compressedStream = new MemoryStream(compressed);
                    using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
                    using var reader = new ByteReader(gzip);
                    asset = Read(reader);
                    status = "MaleCNS v1.0 loaded";
                    return true;
                }
                catch (Exception ex)
                {
                    status = "MaleCNS v1.0 asset rejected: " + ex.GetType().Name + " - " + ex.Message;
                    return false;
                }
            }

            private const int CompressedPayloadBytes = 22964094;
            private const string ExpectedPayloadSha256 = "E33DF182BED7A6F3EA279DAF4790A82B05706D3D41E819A6A80C0473E8C559F3";

            internal static byte[] ReadTexturePayload(Texture2D texture)
            {
                var pixels = texture.GetPixels32();
                if (texture.width <= 0 || texture.height <= 0 || (long)texture.width * texture.height * 3 < CompressedPayloadBytes)
                {
                    throw new InvalidDataException();
                }

                var payload = new byte[CompressedPayloadBytes];
                for (var i = 0; i < payload.Length; i++)
                {
                    var sourcePixel = i / 3;
                    var sourceRow = sourcePixel / texture.width;
                    var sourceColumn = sourcePixel % texture.width;
                    var unityRow = texture.height - 1 - sourceRow;
                    var pixel = pixels[unityRow * texture.width + sourceColumn];
                    payload[i] = ReadCarrierByte(pixel, i % 3);
                }

                var actual = ComputePayloadSha256(payload);
                if (!string.Equals(actual, ExpectedPayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("MaleCNS payload checksum does not match the expected SHA-256.");
                }

                return payload;
            }

            // Pure SHA-256 byte/word arithmetic: integrity checking needs no platform
            // cryptography namespace or privileged API. Tests compare this routine
            // with independent known vectors and the offline framework implementation.
            private static readonly uint[] Sha256RoundConstants =
            [
                0x428a2f98u, 0x71374491u, 0xb5c0fbcfu, 0xe9b5dba5u, 0x3956c25bu, 0x59f111f1u, 0x923f82a4u, 0xab1c5ed5u,
                0xd807aa98u, 0x12835b01u, 0x243185beu, 0x550c7dc3u, 0x72be5d74u, 0x80deb1feu, 0x9bdc06a7u, 0xc19bf174u,
                0xe49b69c1u, 0xefbe4786u, 0x0fc19dc6u, 0x240ca1ccu, 0x2de92c6fu, 0x4a7484aau, 0x5cb0a9dcu, 0x76f988dau,
                0x983e5152u, 0xa831c66du, 0xb00327c8u, 0xbf597fc7u, 0xc6e00bf3u, 0xd5a79147u, 0x06ca6351u, 0x14292967u,
                0x27b70a85u, 0x2e1b2138u, 0x4d2c6dfcu, 0x53380d13u, 0x650a7354u, 0x766a0abbu, 0x81c2c92eu, 0x92722c85u,
                0xa2bfe8a1u, 0xa81a664bu, 0xc24b8b70u, 0xc76c51a3u, 0xd192e819u, 0xd6990624u, 0xf40e3585u, 0x106aa070u,
                0x19a4c116u, 0x1e376c08u, 0x2748774cu, 0x34b0bcb5u, 0x391c0cb3u, 0x4ed8aa4au, 0x5b9cca4fu, 0x682e6ff3u,
                0x748f82eeu, 0x78a5636fu, 0x84c87814u, 0x8cc70208u, 0x90befffau, 0xa4506cebu, 0xbef9a3f7u, 0xc67178f2u
            ];

            internal static string ComputePayloadSha256(byte[] data)
            {
                if (data == null) throw new ArgumentNullException(nameof(data));
                uint[] state = [0x6a09e667u, 0xbb67ae85u, 0x3c6ef372u, 0xa54ff53au, 0x510e527fu, 0x9b05688cu, 0x1f83d9abu, 0x5be0cd19u];
                var words = new uint[64];
                var paddedLength = ((long)data.Length + 9 + 63) / 64 * 64;
                var bitLength = (ulong)data.Length * 8;
                unchecked
                {
                    for (long offset = 0; offset < paddedLength; offset += 64)
                    {
                        for (var i = 0; i < 16; i++)
                        {
                            uint word = 0;
                            for (var j = 0; j < 4; j++)
                            {
                                var position = offset + i * 4 + j;
                                byte value = 0;
                                if (position < data.Length) value = data[(int)position];
                                else if (position == data.Length) value = 0x80;
                                else if (position >= paddedLength - 8)
                                    value = (byte)(bitLength >> (int)((paddedLength - 1 - position) * 8));
                                word = (word << 8) | value;
                            }
                            words[i] = word;
                        }
                        for (var i = 16; i < 64; i++)
                        {
                            var x = words[i - 15];
                            var y = words[i - 2];
                            var sigma0 = RotateRight(x, 7) ^ RotateRight(x, 18) ^ (x >> 3);
                            var sigma1 = RotateRight(y, 17) ^ RotateRight(y, 19) ^ (y >> 10);
                            words[i] = words[i - 16] + sigma0 + words[i - 7] + sigma1;
                        }
                        var a = state[0]; var b = state[1]; var c = state[2]; var d = state[3];
                        var e = state[4]; var f = state[5]; var g = state[6]; var h = state[7];
                        for (var i = 0; i < 64; i++)
                        {
                            var sum1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
                            var choice = (e & f) ^ (~e & g);
                            var temp1 = h + sum1 + choice + Sha256RoundConstants[i] + words[i];
                            var sum0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
                            var majority = (a & b) ^ (a & c) ^ (b & c);
                            var temp2 = sum0 + majority;
                            h = g; g = f; f = e; e = d + temp1;
                            d = c; c = b; b = a; a = temp1 + temp2;
                        }
                        state[0] += a; state[1] += b; state[2] += c; state[3] += d;
                        state[4] += e; state[5] += f; state[6] += g; state[7] += h;
                    }
                }
                var result = new StringBuilder(64);
                foreach (var word in state) result.Append(word.ToString("X8"));
                return result.ToString();
            }

            private static uint RotateRight(uint value, int count) => (value >> count) | (value << (32 - count));

            private static byte ReadCarrierByte(Color32 pixel, int channel)
            {
                if (channel == 0)
                {
                    return pixel.r;
                }

                if (channel == 1)
                {
                    return pixel.g;
                }

                return pixel.b;
            }

        }
    }
}
