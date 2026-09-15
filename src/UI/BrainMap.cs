using System;
using System.Collections.Generic;
using Mod.Core;

namespace Mod.UI
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
            return superclass switch
            {
                "ascending_neuron" => "Ascending neurons",
                "cb_endocrine" => "Central brain hormone-control neurons",
                "cb_efferent" => "Central brain output neurons",
                "cb_intrinsic" => "Central brain local-circuit neurons",
                "cb_motor" => "Central brain motor neurons",
                "cb_sensory" => "Central brain sensory neurons",
                "cb_sensory_tbc" => "Central brain sensory neurons (pending classification)",
                "descending_neuron" => "Descending neurons",
                "descending_neuron_tbc" => "Descending neurons (pending classification)",
                "efferent_ascending" => "Ascending output neurons",
                "efferent_descending" => "Descending output neurons",
                "ENS" => "Enteric nervous system",
                "ol_intrinsic" => "Optic lobe local-circuit neurons",
                "ol_sensory" => "Optic lobe sensory neurons",
                "sensory_ascending" => "Ascending sensory neurons",
                "sensory_ascending_tbc" => "Ascending sensory neurons (pending classification)",
                "sensory_descending" => "Descending sensory neurons",
                "unannotated" => "Unannotated neurons",
                "vnc_endocrine" => "Ventral nerve cord hormone-control neurons",
                "vnc_efferent" => "Ventral nerve cord output neurons",
                "vnc_intrinsic" => "Ventral nerve cord local-circuit neurons",
                "vnc_motor" => "Ventral nerve cord motor neurons",
                "vnc_sensory" => "Ventral nerve cord sensory neurons",
                "vnc_sensory_tbc" => "Ventral nerve cord sensory neurons (pending classification)",
                "vnc_tbc" => "Ventral nerve cord neurons (pending classification)",
                "visual_centrifugal" => "Visual centrifugal neurons (visual-system feedback)",
                "visual_projection" => "Visual projection neurons",
                "visual_projection_tbc" => "Visual projection neurons (pending classification)",
                _ => HumanizeClassName(superclass),
            };

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

    internal readonly struct BrainMapPoint(int neuronId, float x, float z, int category)
    {
        public readonly int NeuronId = neuronId, Category = category;
        public readonly float X = x, Z = z;
    }

}
