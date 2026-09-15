namespace Mod.Core
{
    internal sealed partial class LifBrain
    {
        internal static LifBrain CreateForTest(int neuronCount, int[] rows, int[] posts, float[] weights)
        {
            var runtime = new ModAsset
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
            return new LifBrain(runtime);
        }

        internal void SetTestPopulation(string name, params int[] ids)
        {
            asset.SetPopulation(name, ids);
        }

        internal void SetTestNeuronMetadata(int id, string superclass, string side)
        {
            var runtime = (ModAsset)asset;
            runtime.Superclasses[id] = superclass;
            runtime.Sides[id] = side;
        }

        internal void SetTestPending(int id, float value)
        {
            if (!pendingPresent[id])
            {
                pendingPresent[id] = true;
                pendingIds.Add(id);
            }
            pending[id] = value;
            if (!activePresent[id])
            {
                activePresent[id] = true;
                active.Add(id);
            }
        }

        internal void SetTestActiveRange(int count, float value)
        {
            for (var id = 0; id < count; id++)
            {
                if (!pendingPresent[id])
                {
                    pendingPresent[id] = true;
                    pendingIds.Add(id);
                }
                pending[id] = value;
                if (!activePresent[id])
                {
                    activePresent[id] = true;
                    active.Add(id);
                }
            }
        }

        internal float TestPendingValue(int id) => pendingPresent[id] ? pending[id] : 0f;
        internal float TestPotentialValue(int id) => potential[id];
        internal int TestTraversedEdges
        {
            get
            {
                var count = 0;
                foreach (var id in fired) count += asset.OutgoingEnd(id) - asset.OutgoingStart(id);
                return count;
            }
        }
        internal int TestPendingCount => pendingIds.Count;
        internal int TestActiveCount => active.Count;
        internal int TestProcessedCount => processedThisStep;
        internal int TestFiredCount => fired.Count;
        internal float TestSensoryDrive => LastSensoryDrive;
        internal (int Edges, int NonzeroSignMembers) TestPopulationConnectivity(string name)
        {
            var edges = 0;
            var signed = 0;
            foreach (var id in asset.Population(name))
            {
                edges += asset.OutgoingEnd(id) - asset.OutgoingStart(id);
                if (asset.SignAt(id) != 0) signed++;
            }
            return (edges, signed);
        }
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
        internal static string PayloadDigestForTest(byte[] data) => ModAsset.ComputePayloadSha256(data);
        internal static byte[] DecodePayloadForTest(UnityEngine.Texture2D texture) => ModAsset.ReadTexturePayload(texture);
        internal static void ReadAssetForTest(Stream stream)
        {
            using var reader = new ModAsset.ByteReader(stream);
            _ = ModAsset.Read(reader);
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
