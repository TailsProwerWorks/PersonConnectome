using System;

namespace Mod
{
    // Diagnostics only. The sample never changes the simulated graph or its inputs.
    internal sealed class BrainMapSample
    {
        public const int MaximumPoints = 8192;
        public readonly BrainMapPoint[] Points;
        public readonly int LocatedCount;
        public readonly int NeuronCount;

        private BrainMapSample(BrainMapPoint[] points, int locatedCount, int neuronCount)
        {
            Points = points;
            LocatedCount = locatedCount;
            NeuronCount = neuronCount;
        }

        public static BrainMapSample Create(float[] soma, string[] superclasses)
        {
            var count = soma.Length / 3;
            var located = 0;
            for (var id = 0; id < count; id++) if (HasPosition(soma, id)) located++;
            var points = new BrainMapPoint[Math.Min(located, MaximumPoints)];
            var ordinal = 0;
            var selected = 0;
            for (var id = 0; id < count && selected < points.Length; id++)
            {
                if (!HasPosition(soma, id)) continue;
                // Evenly sample the valid soma sequence, which retains source neuron IDs.
                if (ordinal == (long)selected * located / points.Length)
                {
                    points[selected++] = new BrainMapPoint(id, soma[id * 3], soma[id * 3 + 2], Category(superclasses[id]));
                }
                ordinal++;
            }
            return new BrainMapSample(points, located, count);
        }

        private static bool HasPosition(float[] soma, int id)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var value = soma[id * 3 + axis];
                if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            }
            return true;
        }

        private static int Category(string superclass)
        {
            if (superclass == "ol_intrinsic" || superclass == "ol_sensory") return 0;
            if (superclass == "cb_intrinsic") return 1;
            if (superclass == "vnc_intrinsic") return 2;
            if (superclass != null && (superclass.IndexOf("motor", StringComparison.Ordinal) >= 0 || superclass == "descending_neuron")) return 3;
            return 4;
        }
    }

    internal struct BrainMapPoint
    {
        public readonly int NeuronId, Category;
        public readonly float X, Z;
        public BrainMapPoint(int neuronId, float x, float z, int category)
        {
            NeuronId = neuronId;
            X = x;
            Z = z;
            Category = category;
        }
    }

    internal sealed partial class ConnectomeBrain
    {
        public BrainMapSample BrainMap => (asset as RuntimeAsset)?.BrainMap;
    }
}
