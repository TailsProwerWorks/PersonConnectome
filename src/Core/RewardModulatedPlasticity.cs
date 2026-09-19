using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShadowNineX.PersonConnectome.Core
{
    /// <summary>
    /// Engineering prototype of reward-modulated, pair-based eligibility
    /// plasticity. It changes efficacy magnitudes for a bounded, deterministic
    /// subset of existing edges; the shared connectome topology never changes.
    /// This is not a biologically validated Drosophila dopamine model.
    /// </summary>
    internal sealed class RewardModulatedPlasticity
    {
        private const string SchemaVersion = "rmcp-v1";
        private const int DefaultEdgeBudget = 4096;
        private const float TauPreSeconds = .2f;
        private const float TauPostSeconds = .2f;
        private const float TauEligibilitySeconds = .8f;
        private const float TauModulatorSeconds = 1.5f;
        private const float AMinus = .012f;
        private const float APlus = .015f;
        private const float LearningRate = .08f;
        private const float BaselineRate = .05f;
        private const float MinimumWeightScale = .25f;
        private const float MaximumWeightScale = 2f;
        private const float TraceEpsilon = .00001f;
        private const float DeltaEpsilon = .0000001f;
        private static readonly string[] LearnablePopulationNames =
        {
            "input:light", "type:Mi1", "type:L2", "type:L3", "input:auditory",
            "input:touch-head", "input:touch-arms", "input:touch-legs", "input:touch-core",
            "input:touch-other", "input:tactile", "input:gravity", "input:joint-position",
            "input:joint-motion", "input:joint-load", "input:hot", "input:cold", "type:LC4",
            "type:LPLC2", "type:LC11", "type:LC18", "input:optic-roll", "type:ORN_DM1",
            "type:ORN_DM2", "type:ORN_VA2", "type:LB3b", "type:LB3c",
            "type:DNp09", "type:DNg100", "type:DNge053", "type:DNg02", "type:MDN",
            "type:DNa02", "type:DNa01", "type:DNp01", "motor", "motor:leg"
        };

        private readonly IConnectomeAsset asset;
        private readonly int[] edgeIndexes;
        private readonly int[] edgeSources;
        private readonly int[] edgeTargets;
        private readonly float[] edgeBaseWeights;
        private readonly float[] edgeDeltas;
        private readonly Dictionary<int, int> edgeSlots;
        private readonly float[] eligibility;
        private readonly float[] preTrace;
        private readonly float[] postTrace;
        private readonly int[] outgoingOffsets;
        private readonly int[] incomingOffsets;
        private readonly int[] outgoingSlots;
        private readonly int[] incomingSlots;
        private readonly List<int> activePreTrace = new();
        private readonly List<int> activePostTrace = new();
        private readonly bool[] preTracePresent;
        private readonly bool[] postTracePresent;
        private ConnectomeLearningMode mode;
        private float modulator;
        private float rewardBaseline;
        private bool hasRewardBaseline;
        private float lastReinforcementFactor;
        private string lastReinforcementKind = "none";
        private int modifiedEdgeCount;

        public RewardModulatedPlasticity(IConnectomeAsset asset, int edgeBudget = DefaultEdgeBudget,
            IReadOnlyList<int>? explicitEdgeIndexes = null)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
            var selected = SelectEdges(asset, edgeBudget, explicitEdgeIndexes);
            edgeIndexes = new int[selected.Count];
            edgeSources = new int[selected.Count];
            edgeTargets = new int[selected.Count];
            edgeBaseWeights = new float[selected.Count];
            edgeDeltas = new float[selected.Count];
            eligibility = new float[selected.Count];
            BuildSelectedEdges(selected);
            edgeSlots = new Dictionary<int, int>(edgeIndexes.Length);
            for (var index = 0; index < edgeIndexes.Length; index++) edgeSlots[edgeIndexes[index]] = index;

            preTrace = new float[asset.NeuronCount];
            postTrace = new float[asset.NeuronCount];
            preTracePresent = new bool[asset.NeuronCount];
            postTracePresent = new bool[asset.NeuronCount];
            outgoingOffsets = BuildOffsets(edgeSources, asset.NeuronCount);
            incomingOffsets = BuildOffsets(edgeTargets, asset.NeuronCount);
            outgoingSlots = BuildSlots(edgeSources, outgoingOffsets, asset.NeuronCount);
            incomingSlots = BuildSlots(edgeTargets, incomingOffsets, asset.NeuronCount);
            mode = ConnectomeLearningMode.FrozenBaseline;
        }

        public ConnectomeLearningMode Mode => mode;
        public int SelectedEdgeCount => edgeIndexes.Length;
        public int ModifiedEdgeCount => modifiedEdgeCount;
        public float LastReinforcementFactor => lastReinforcementFactor;
        public float RewardBaseline => hasRewardBaseline ? rewardBaseline : 0f;
        public static string RuleName => "Experimental reward-modulated connectome plasticity";

        public string StatusText
        {
            get
            {
                string label;
                if (mode == ConnectomeLearningMode.FrozenBaseline)
                {
                    label = "frozen baseline";
                }
                else if (mode == ConnectomeLearningMode.FrozenLearnedConnectome)
                {
                    label = "learned playback";
                }
                else
                {
                    label = "plastic";
                }
                return "PLASTICITY: " + label + " | selected " + SelectedEdgeCount + " | modified " + modifiedEdgeCount +
                    " | eligibility " + CountEligibleEdges() + " | last " + lastReinforcementKind + " " +
                    lastReinforcementFactor.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
            }
        }

        public void SetMode(ConnectomeLearningMode value)
        {
            if (mode == value) return;
            mode = value;
            if (!IsPlasticMode) ResetTransientState();
        }

        public void BeginStep(float elapsedSeconds)
        {
            if (!IsPlasticMode) return;
            var seconds = PositiveSeconds(elapsedSeconds);
            DecayActiveTraces(preTrace, preTracePresent, activePreTrace, seconds / TauPreSeconds);
            DecayActiveTraces(postTrace, postTracePresent, activePostTrace, seconds / TauPostSeconds);
            var eligibilityDecay = Decay(seconds, TauEligibilitySeconds);
            for (var index = 0; index < eligibility.Length; index++) eligibility[index] *= eligibilityDecay;
            modulator *= Decay(seconds, TauModulatorSeconds);
        }

        /// <summary>Records a source firing event before its outgoing arrival is queued.</summary>
        public void RecordPresynaptic(int source)
        {
            if (!IsPlasticMode) return;
            if (source < 0 || source >= preTrace.Length) return;
            var start = outgoingOffsets[source];
            var end = outgoingOffsets[source + 1];
            for (var cursor = start; cursor < end; cursor++)
            {
                var slot = outgoingSlots[cursor];
                eligibility[slot] -= AMinus * postTrace[edgeTargets[slot]];
                eligibility[slot] = Clamp(eligibility[slot], -1f, 1f);
            }

            AddTrace(preTracePresent, activePreTrace, source);
            preTrace[source] += 1f;
        }

        /// <summary>Records a target firing event using the documented same-tick order.</summary>
        public void RecordPostsynaptic(int target)
        {
            if (!IsPlasticMode) return;
            if (target < 0 || target >= postTrace.Length) return;
            var start = incomingOffsets[target];
            var end = incomingOffsets[target + 1];
            for (var cursor = start; cursor < end; cursor++)
            {
                var slot = incomingSlots[cursor];
                eligibility[slot] += APlus * preTrace[edgeSources[slot]];
                eligibility[slot] = Clamp(eligibility[slot], -1f, 1f);
            }

            AddTrace(postTracePresent, activePostTrace, target);
            postTrace[target] += 1f;
        }

        /// <summary>
        /// Applies the continuous posture signal at the synchronization point
        /// after the preceding action interval. The elapsed factor is applied
        /// exactly once.
        /// </summary>
        public int ApplyReward(float reward, float elapsedSeconds = .05f)
        {
            if (!IsPlasticMode) return 0;
            var observed = ClampFinite(reward, -1f, 1f);
            var factor = hasRewardBaseline ? observed - rewardBaseline : observed;
            rewardBaseline = hasRewardBaseline ? rewardBaseline + BaselineRate * factor : observed;
            hasRewardBaseline = true;
            factor = Clamp(factor, -2f, 2f);
            if (Math.Abs(factor) >= .001f)
            {
                lastReinforcementFactor = factor;
                lastReinforcementKind = "posture";
            }
            modulator = Clamp(modulator + factor, -2f, 2f);
            var step = PositiveSeconds(elapsedSeconds);
            return ApplyWeightChange(modulator, step);
        }

        /// <summary>
        /// Applies a discrete player feedback event. Unlike the continuous
        /// posture signal, an event is already an impulse and must not be
        /// multiplied by one neural tick (which previously made Good/Bad about
        /// twenty times weaker than their displayed +/-1 value).
        /// </summary>
        public int ApplyRewardImpulse(float reward)
        {
            if (!IsPlasticMode) return 0;
            var factor = ClampFinite(reward, -1f, 1f);
            if (Math.Abs(factor) < .000001f) return 0;
            lastReinforcementFactor = factor;
            lastReinforcementKind = "feedback";
            modulator = Clamp(modulator + factor, -2f, 2f);
            return ApplyWeightChange(factor, 1f);
        }

        public float EffectiveWeight(int edgeIndex, float baseMagnitude, sbyte sign)
        {
            if (sign == 0 || mode == ConnectomeLearningMode.FrozenBaseline) return baseMagnitude * sign;
            var slot = edgeSlots.TryGetValue(edgeIndex, out var selectedSlot) ? selectedSlot : -1;
            var magnitude = slot < 0 ? baseMagnitude : Clamp(baseMagnitude + edgeDeltas[slot], baseMagnitude * MinimumWeightScale, Math.Min(4f, baseMagnitude * MaximumWeightScale));
            return magnitude * sign;
        }

        public void ResetTransientState()
        {
            Array.Clear(eligibility, 0, eligibility.Length);
            Array.Clear(preTrace, 0, preTrace.Length);
            Array.Clear(postTrace, 0, postTrace.Length);
            Array.Clear(preTracePresent, 0, preTracePresent.Length);
            Array.Clear(postTracePresent, 0, postTracePresent.Length);
            activePreTrace.Clear();
            activePostTrace.Clear();
            modulator = 0f;
            rewardBaseline = 0f;
            hasRewardBaseline = false;
            lastReinforcementFactor = 0f;
            lastReinforcementKind = "none";
        }

        public void ResetLearning()
        {
            ResetTransientState();
            Array.Clear(edgeDeltas, 0, edgeDeltas.Length);
            modifiedEdgeCount = 0;
        }

        public string Serialize()
        {
            var result = new StringBuilder(SchemaVersion.Length + edgeDeltas.Length * 10 + 64);
            result.Append(SchemaVersion).Append('|').Append(asset.NeuronCount).Append('|').Append(asset.EdgeCount).Append('|').Append(CatalogHash()).Append('|');
            for (var index = 0; index < edgeDeltas.Length; index++)
            {
                if (index != 0) result.Append(',');
                result.Append(edgeDeltas[index].ToString("R", CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        public bool TryLoad(string serialized)
        {
            if (String.IsNullOrWhiteSpace(serialized)) return false;
            var fields = serialized.Split('|');
            if (fields.Length != 5 || fields[0] != SchemaVersion || fields[1] != asset.NeuronCount.ToString(CultureInfo.InvariantCulture) ||
                fields[2] != asset.EdgeCount.ToString(CultureInfo.InvariantCulture) || fields[3] != CatalogHash().ToString(CultureInfo.InvariantCulture)) return false;
            var values = fields[4].Split(',');
            if (values.Length != edgeDeltas.Length) return false;
            var loaded = new float[edgeDeltas.Length];
            for (var index = 0; index < loaded.Length; index++)
            {
                if (!float.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out loaded[index]) ||
                    float.IsNaN(loaded[index]) || float.IsInfinity(loaded[index]) || loaded[index] < LowerDelta(index) || loaded[index] > UpperDelta(index)) return false;
            }

            Array.Copy(loaded, edgeDeltas, loaded.Length);
            modifiedEdgeCount = 0;
            for (var index = 0; index < edgeDeltas.Length; index++) if (Math.Abs(edgeDeltas[index]) >= DeltaEpsilon) modifiedEdgeCount++;
            ResetTransientState();
            return true;
        }

        private bool IsPlasticMode => mode == ConnectomeLearningMode.PlasticConnectome;

        private int ApplyWeightChange(float reinforcement, float scale)
        {
            var changed = 0;
            for (var index = 0; index < edgeDeltas.Length; index++)
            {
                var update = LearningRate * reinforcement * eligibility[index] * scale;
                if (Math.Abs(update) < DeltaEpsilon) continue;
                var next = Clamp(edgeDeltas[index] + update, LowerDelta(index), UpperDelta(index));
                if (Math.Abs(next - edgeDeltas[index]) < DeltaEpsilon) continue;
                if (Math.Abs(edgeDeltas[index]) < DeltaEpsilon) modifiedEdgeCount++;
                edgeDeltas[index] = next;
                changed++;
            }
            return changed;
        }

        private void BuildSelectedEdges(List<int> selected)
        {
            var selectedSet = new HashSet<int>(selected);
            var slot = 0;
            for (var source = 0; source < asset.NeuronCount; source++)
            {
                for (var edge = asset.OutgoingStart(source); edge < asset.OutgoingEnd(source); edge++)
                {
                    if (!selectedSet.Contains(edge)) continue;
                    edgeIndexes[slot] = edge;
                    edgeSources[slot] = source;
                    edgeTargets[slot] = asset.TargetAt(edge);
                    edgeBaseWeights[slot] = asset.WeightAt(edge);
                    slot++;
                }
            }
        }

        private static List<int> SelectEdges(IConnectomeAsset asset, int edgeBudget, IReadOnlyList<int>? explicitEdges)
        {
            var budget = Math.Max(0, edgeBudget);
            var selected = new List<int>(Math.Min(budget, asset.EdgeCount));
            if (explicitEdges != null)
            {
                SelectExplicitEdges(selected, explicitEdges, asset.EdgeCount, budget);
                return selected;
            }

            var sourceIds = new HashSet<int>();
            AddLearnablePopulations(sourceIds, asset);
            SelectLearnableEdges(selected, sourceIds, asset, budget);

            if (selected.Count == 0)
            {
                SelectFallbackEdges(selected, asset.EdgeCount, budget);
            }
            return selected;
        }

        internal static int[] SelectDefaultEdgesForTest(IConnectomeAsset asset, int edgeBudget)
        {
            return SelectEdges(asset, edgeBudget, null).ToArray();
        }

        private static void SelectExplicitEdges(List<int> selected, IReadOnlyList<int> explicitEdges, int edgeCount, int budget)
        {
            for (var index = 0; index < explicitEdges.Count && selected.Count < budget; index++)
            {
                var edge = explicitEdges[index];
                if (edge >= 0 && edge < edgeCount && !selected.Contains(edge)) selected.Add(edge);
            }
        }

        private static void AddLearnablePopulations(HashSet<int> sourceIds, IConnectomeAsset asset)
        {
            for (var index = 0; index < LearnablePopulationNames.Length; index++)
            {
                AddPopulation(sourceIds, asset, LearnablePopulationNames[index]);
            }
        }

        private static void SelectLearnableEdges(List<int> selected, HashSet<int> sourceIds, IConnectomeAsset asset, int budget)
        {
            var sources = new List<int>(sourceIds);
            sources.Sort();
            var maximumDepth = MaximumOutgoingDepth(sources, asset);

            // Round-robin by outgoing depth so large populations cannot consume
            // the whole bounded catalog before smaller fly sensory routes get a slot.
            for (var depth = 0; depth < maximumDepth && selected.Count < budget; depth++)
            {
                SelectEdgesAtDepth(selected, sources, asset, budget, depth);
            }
            selected.Sort();
        }

        private static int MaximumOutgoingDepth(List<int> sources, IConnectomeAsset asset)
        {
            var maximumDepth = 0;
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (asset.SignAt(source) == 0) continue;
                maximumDepth = Math.Max(maximumDepth, asset.OutgoingEnd(source) - asset.OutgoingStart(source));
            }
            return maximumDepth;
        }

        private static void SelectEdgesAtDepth(List<int> selected, List<int> sources, IConnectomeAsset asset, int budget, int depth)
        {
            for (var index = 0; index < sources.Count && selected.Count < budget; index++)
            {
                var source = sources[index];
                if (asset.SignAt(source) == 0) continue;
                var edge = asset.OutgoingStart(source) + depth;
                if (edge >= asset.OutgoingEnd(source)) continue;
                if (asset.WeightAt(edge) > 0f) selected.Add(edge);
            }
        }

        private static void SelectFallbackEdges(List<int> selected, int edgeCount, int budget)
        {
            for (var edge = 0; edge < edgeCount && selected.Count < budget; edge++) selected.Add(edge);
        }

        private static void AddPopulation(HashSet<int> destination, IConnectomeAsset asset, string name)
        {
            var ids = asset.Population(name);
            for (var index = 0; index < ids.Count; index++) destination.Add(ids[index]);
        }

        private static int[] BuildOffsets(int[] ids, int count)
        {
            var offsets = new int[count + 1];
            for (var index = 0; index < ids.Length; index++) offsets[ids[index] + 1]++;
            for (var index = 1; index < offsets.Length; index++) offsets[index] += offsets[index - 1];
            return offsets;
        }

        private static int[] BuildSlots(int[] ids, int[] offsets, int count)
        {
            var slots = new int[ids.Length];
            var cursors = new int[count];
            Array.Copy(offsets, 0, cursors, 0, count);
            for (var slot = 0; slot < ids.Length; slot++) slots[cursors[ids[slot]]++] = slot;
            return slots;
        }

        private float LowerDelta(int index) => edgeBaseWeights[index] * (MinimumWeightScale - 1f);
        private float UpperDelta(int index) => Math.Min(4f, edgeBaseWeights[index] * MaximumWeightScale) - edgeBaseWeights[index];
        private int CountEligibleEdges()
        {
            var count = 0;
            for (var index = 0; index < eligibility.Length; index++) if (Math.Abs(eligibility[index]) >= TraceEpsilon) count++;
            return count;
        }

        private int CatalogHash()
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var index = 0; index < edgeIndexes.Length; index++)
                {
                    hash = (hash ^ (uint)edgeIndexes[index]) * 16777619u;
                    hash = (hash ^ (uint)edgeSources[index]) * 16777619u;
                    hash = (hash ^ (uint)edgeTargets[index]) * 16777619u;
                }
                return (int)hash;
            }
        }

        private static void AddTrace(bool[] present, List<int> active, int id)
        {
            if (present[id]) return;
            present[id] = true;
            active.Add(id);
        }

        private static void DecayActiveTraces(float[] trace, bool[] present, List<int> active, float exponent)
        {
            var factor = (float)Math.Exp(-Math.Max(0f, exponent));
            for (var index = active.Count - 1; index >= 0; index--)
            {
                var id = active[index];
                trace[id] *= factor;
                if (Math.Abs(trace[id]) >= TraceEpsilon) continue;
                trace[id] = 0f;
                present[id] = false;
                active.RemoveAt(index);
            }
        }

        private static float PositiveSeconds(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? Math.Min(value, .25f) : .05f;
        private static float Decay(float seconds, float tau) => (float)Math.Exp(-seconds / tau);
        private static float ClampFinite(float value, float minimum, float maximum) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Clamp(value, minimum, maximum);
        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
