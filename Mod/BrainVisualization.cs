using System;
using System.Collections.Generic;

namespace Mod
{
    // Diagnostics only. The sample never changes the simulated graph or its inputs.
    internal sealed class BrainMapSample
    {
        public readonly BrainMapPoint[] Points;
        public readonly string[] Classes;
        public readonly int LocatedCount;
        public readonly int NeuronCount;

        private BrainMapSample(BrainMapPoint[] points, string[] classes, int locatedCount, int neuronCount)
        {
            Points = points;
            Classes = classes;
            LocatedCount = locatedCount;
            NeuronCount = neuronCount;
        }

        public static BrainMapSample Create(float[] soma, string[] superclasses)
        {
            var count = soma.Length / 3;
            var located = 0;
            for (var id = 0; id < count; id++) if (HasPosition(soma, id)) located++;
            var classIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var id = 0; id < count; id++)
            {
                var name = ClassName(superclasses[id]);
                if (!classIndexes.ContainsKey(name)) classIndexes.Add(name, 0);
            }
            var classNames = new List<string>(classIndexes.Keys);
            classNames.Sort((left, right) =>
            {
                var result = StringComparer.OrdinalIgnoreCase.Compare(FriendlyClassName(left), FriendlyClassName(right));
                return result != 0 ? result : StringComparer.Ordinal.Compare(left, right);
            });
            var classes = classNames.ToArray();
            classIndexes.Clear();
            for (var i = 0; i < classes.Length; i++) classIndexes.Add(classes[i], i);
            var points = new BrainMapPoint[located];
            var selected = 0;
            for (var id = 0; id < count; id++)
            {
                if (!HasPosition(soma, id)) continue;
                // Keep every finite soma position and retain its source neuron ID.
                points[selected++] = new BrainMapPoint(id, soma[id * 3], soma[id * 3 + 2], classIndexes[ClassName(superclasses[id])]);
            }
            return new BrainMapSample(points, classes, located, count);
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

        private static string ClassName(string superclass)
        {
            return String.IsNullOrEmpty(superclass) ? "unannotated" : superclass;
        }

        public static string FriendlyClassName(string superclass)
        {
            switch (superclass)
            {
                case "ascending_neuron": return "Ascending neurons";
                case "cb_endocrine": return "Central brain hormone-control neurons";
                case "cb_efferent": return "Central brain output neurons";
                case "cb_intrinsic": return "Central brain local-circuit neurons";
                case "cb_motor": return "Central brain motor neurons";
                case "cb_sensory": return "Central brain sensory neurons";
                case "cb_sensory_tbc": return "Central brain sensory neurons (pending classification)";
                case "descending_neuron": return "Descending neurons";
                case "descending_neuron_tbc": return "Descending neurons (pending classification)";
                case "efferent_ascending": return "Ascending output neurons";
                case "efferent_descending": return "Descending output neurons";
                case "ENS": return "Enteric nervous system";
                case "ol_intrinsic": return "Optic lobe local-circuit neurons";
                case "ol_sensory": return "Optic lobe sensory neurons";
                case "sensory_ascending": return "Ascending sensory neurons";
                case "sensory_ascending_tbc": return "Ascending sensory neurons (pending classification)";
                case "sensory_descending": return "Descending sensory neurons";
                case "unannotated": return "Unannotated neurons";
                case "vnc_endocrine": return "Ventral nerve cord hormone-control neurons";
                case "vnc_efferent": return "Ventral nerve cord output neurons";
                case "vnc_intrinsic": return "Ventral nerve cord local-circuit neurons";
                case "vnc_motor": return "Ventral nerve cord motor neurons";
                case "vnc_sensory": return "Ventral nerve cord sensory neurons";
                case "vnc_sensory_tbc": return "Ventral nerve cord sensory neurons (pending classification)";
                case "vnc_tbc": return "Ventral nerve cord neurons (pending classification)";
                case "visual_centrifugal": return "Visual centrifugal neurons (visual-system feedback)";
                case "visual_projection": return "Visual projection neurons";
                case "visual_projection_tbc": return "Visual projection neurons (pending classification)";
                default: return HumanizeClassName(superclass);
            }
        }

        private static string HumanizeClassName(string superclass)
        {
            if (String.IsNullOrEmpty(superclass)) return "Unannotated neurons";
            var words = superclass.Replace('_', ' ').Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                words[i] = Char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }
            return String.Join(" ", words) + " neurons";
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
