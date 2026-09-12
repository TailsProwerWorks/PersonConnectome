using System.Collections.Generic;

namespace Mod
{
    internal sealed partial class ConnectomeBrain
    {
        internal static ConnectomeBrain CreateForTest(int neuronCount, int[] rows, int[] posts, float[] weights)
        {
            var runtime = new RuntimeAsset
            {
                NeuronCount = neuronCount,
                EdgeCount = posts.Length,
                RowPointers = rows,
                PostIndexes = posts,
                Weights = weights,
                NtSigns = CreateSigns(neuronCount),
                Superclasses = CreateStrings(neuronCount),
                Sides = CreateStrings(neuronCount)
            };
            return new ConnectomeBrain(runtime);
        }

        internal void SetTestPopulation(string name, params int[] ids)
        {
            asset.SetPopulation(name, ids);
        }

        internal void SetTestNeuronMetadata(int id, string superclass, string side)
        {
            var runtime = (RuntimeAsset)asset;
            runtime.Superclasses[id] = superclass;
            runtime.Sides[id] = side;
        }

        internal void SetTestPending(int id, float value)
        {
            pending[id] = value;
            active.Add(id);
        }

        internal void SetTestActiveRange(int count, float value)
        {
            for (var id = 0; id < count; id++)
            {
                pending[id] = value;
                active.Add(id);
            }
        }

        internal float TestPendingValue(int id) => pending.TryGetValue(id, out var value) ? value : 0f;
        internal float TestPotentialValue(int id) => potential[id];
        internal int TestPendingCount => pending.Count;
        internal int TestActiveCount => active.Count;
        internal int TestProcessedCount => processedThisStep;
        internal int TestFiredCount => fired.Count;
        internal float TestSensoryDrive => LastSensoryDrive;
        internal long TestSimulationTick => simulationTick;
        internal long TestBacklogCursor => backlogCursor;
        internal int TestDroppedCount => droppedThisStep;
        internal int TestRefractoryActiveCount
        {
            get
            {
                var count = 0;
                foreach (var id in active) if (simulationTick + 1 < refractoryUntil[id]) count++;
                return count;
            }
        }
        internal static string PayloadDigestForTest(byte[] data) => RuntimeAsset.ComputePayloadSha256(data);
        internal static byte[] DecodePayloadForTest(UnityEngine.Texture2D texture) => RuntimeAsset.ReadTexturePayload(texture);
        internal static void ReadAssetForTest(Stream stream)
        {
            using var reader = new RuntimeAsset.ByteReader(stream);
            _ = RuntimeAsset.Read(reader);
        }

        private static sbyte[] CreateSigns(int count)
        {
            var signs = new sbyte[count];
            for (var i = 0; i < signs.Length; i++) signs[i] = 1;
            return signs;
        }

        private static string[] CreateStrings(int count)
        {
            var values = new string[count];
            for (var i = 0; i < values.Length; i++) values[i] = string.Empty;
            return values;
        }
    }
}
