using System;
using System.Collections.Generic;
using System.Globalization;
using ShadowNineX.PersonConnectome.UI;

namespace ShadowNineX.PersonConnectome.Core
{
    /// <summary>Validated graph contract shared by every body adapter.</summary>
    internal interface IConnectomeAsset
    {
        int NeuronCount { get; }
        int EdgeCount { get; }
        IReadOnlyList<int> Population(string name);
        int OutgoingStart(int neuronId);
        int OutgoingEnd(int neuronId);
        int TargetAt(int edgeIndex);
        float WeightAt(int edgeIndex);
        sbyte SignAt(int neuronId);
        string SuperclassAt(int neuronId);
        string SideAt(int neuronId);
        void SetPopulation(string name, int[] ids);
    }

    // One authoritative LIF implementation. It is compiled into the game script
    // and the game-independent runtime tests; Unity-specific asset loading stays
    // behind IConnectomeAsset in ConnectomeAsset.cs.
    internal sealed partial class LifBrain
    {
        public BrainMapSample? BrainMap => (asset as ModAsset)?.BrainMap;

        private readonly IConnectomeAsset asset;
        private readonly RewardModulatedPlasticity plasticity;
        private readonly float[] potential;
        private readonly long[] refractoryUntil;
        private float[] pending;
        private bool[] pendingPresent;
        private List<int> pendingIds;
        private List<int> active;
        private bool[] activePresent;
        private readonly List<int> priority;
        private readonly bool[] priorityPresent;
        private readonly List<int> scheduled;
        private readonly bool[] scheduledPresent;
        private readonly List<int> fired;
        private readonly bool[] firedPresent;
        private float[] nextPending;
        private bool[] nextPendingPresent;
        private List<int> nextPendingIds;
        private List<int> nextActive;
        private bool[] nextActivePresent;
        private readonly List<int> orderedActive;
        private FlyMotorCommand lastCommand;
        private bool stopped;
        private long simulationTick;
        private long backlogCursor;
        private int processedThisStep;
        private int droppedThisStep;
        private int decayedThisStep;
        private float lightDrive, audioDrive, touchDrive, damageDrive, regionalTouchDrive, smallVisualDrive, opticRollDrive, gravityDrive, jointDrive, hotDrive, coldDrive, approachDrive, effectiveVisualThreatDrive;
        private float foodNearbyDrive, foodContactDrive;
        private float flyEscapeCooldown, flyEscapeQuiet, flyEscapeEvidenceSeconds;
        private bool flyEscapeArmed = true;
        private int sensoryQueued;
        private float preAdvancedLearningSeconds;
        private bool hasPreviousLight;
        private float previousLight, lightOnDrive, lightOffDrive;
        private float forwardFilter, backwardFilter, yawFilter;
        private const int MaxSpikesPerStep = 24000;
        private const int RefractoryTicks = 5;
        private const float DefaultStepSeconds = .05f;
        private const string FlyLegPopulation = "motor:leg";

        private LifBrain(IConnectomeAsset asset)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
            plasticity = new RewardModulatedPlasticity(this.asset);
            potential = new float[asset.NeuronCount];
            refractoryUntil = new long[asset.NeuronCount];
            var sparseCapacity = Math.Min(asset.NeuronCount, MaxSpikesPerStep * 3);
            pendingIds = new List<int>(sparseCapacity);
            active = new List<int>(sparseCapacity);
            nextPendingIds = new List<int>(sparseCapacity);
            nextActive = new List<int>(sparseCapacity);
            var spikeCapacity = Math.Min(asset.NeuronCount, MaxSpikesPerStep);
            priority = new List<int>(spikeCapacity);
            scheduled = new List<int>(spikeCapacity);
            fired = new List<int>(spikeCapacity);
            orderedActive = new List<int>(spikeCapacity);
            firedPresent = new bool[asset.NeuronCount];
            pending = new float[asset.NeuronCount];
            pendingPresent = new bool[asset.NeuronCount];
            activePresent = new bool[asset.NeuronCount];
            priorityPresent = new bool[asset.NeuronCount];
            scheduledPresent = new bool[asset.NeuronCount];
            nextPending = new float[asset.NeuronCount];
            nextPendingPresent = new bool[asset.NeuronCount];
            nextActivePresent = new bool[asset.NeuronCount];
        }

        public string Status => "MaleCNS v1.0 " + asset.NeuronCount + " neurons / " + asset.EdgeCount +
            (stopped ? " STOPPED" : " queued=" + pendingIds.Count + " input-integrated=" + processedThisStep +
            " dropped-spikes=" + droppedThisStep + " fired=" + fired.Count + " input=" + Format(LastSensoryDrive) +
            " request-forward=" + Format(lastCommand.FlyForward) + " | " + plasticity.StatusText);

        public string DisplaySummary
        {
            get
            {
                if (stopped)
                {
                    return "NEURAL: STOPPED\n  queued=0  integrated=0  decay-only=0  dropped-spikes=0  fired=0";
                }

                var scheduler = droppedThisStep > 0 ? "OVERLOADED" : "STEADY";
                return "NEURAL:\n  input=" + Format(LastSensoryDrive) + "  queued=" + pendingIds.Count + "  active=" + active.Count +
                    "\n  integrated=" + processedThisStep + "  decay-only=" + decayedThisStep + "  dropped-spikes=" + droppedThisStep +
                    "\n  fired=" + fired.Count + "/" + MaxSpikesPerStep + "  scheduler=" + scheduler +
                    "\n  " + plasticity.StatusText +
                    (droppedThisStep > 0 ? "\nFiring-event limit reached; dropped spikes are not deferred. This is not a CPU-time measurement." : "");
            }
        }

        public string DisplayInputSummary => stopped ? "INPUT: STOPPED" :
            "LIVE ENCODER REQUESTS (normalized amplitudes, not Hz):\n  light=" + Format(lightDrive) + "  audio=" + Format(audioDrive) +
            "  broad-touch=" + Format(touchDrive) + "  injury-proxy=" + Format(damageDrive) + "  regional-touch=" + Format(regionalTouchDrive) +
            "\n  gravity-proxy=" + Format(gravityDrive) + "  joints=" + Format(jointDrive) + "  small-visual=" + Format(smallVisualDrive) + "  optic-roll=" + Format(opticRollDrive) +
            "\n  light-level change ON=" + Format(lightOnDrive) + "  OFF=" + Format(lightOffDrive) +
            "\n  warm=" + Format(hotDrive) + "  cool=" + Format(coldDrive) + "  approach-proxy=" + Format(approachDrive) + "  effective-visual-threat=" + Format(effectiveVisualThreatDrive) +
            "\n  food-nearby(gameplay)=" + Format(foodNearbyDrive) + "  food-head-contact(gameplay)=" + Format(foodContactDrive) +
            "\n  input neurons queued this tick=" + sensoryQueued +
            "\nFood cues are explicit catalog-based gameplay mappings, not measured smell/taste. Health/chemistry remain telemetry and control constraints.\nVisual directions use head-relative 2D bearing (positive CCW to R); audio/tilt use world horizontal. These are engineering projections.";

        public string DisplayMotorSummary => stopped ?
            "FLY REQUESTS: STOPPED\n  forward=0.00  yaw=0.00  backward=0.00  halt=0.00  brake=0.00  escape=0.00" :
            "FLY REQUESTS (normalized decoder channels):\n  forward=" + Format(lastCommand.FlyForward) + "  yaw=" + Format(lastCommand.FlyYaw) + "  backward=" + Format(lastCommand.FlyBackward) +
            "  halt=" + Format(lastCommand.FlyHalt) + "  brake=" + Format(lastCommand.FlyBrake) + "  escape=" + Format(lastCommand.FlyEscape) +
            "\n  jump=" + Format(lastCommand.FlyJump) + "  takeoff=" + Format(lastCommand.FlyTakeoff) + "  landing=" + Format(lastCommand.FlyLanding) +
            "  flight-power=" + Format(lastCommand.FlyFlightPower) + "  flight-yaw=" + Format(lastCommand.FlyFlightYaw) + "  wing-motor=" + Format(lastCommand.FlyWingMotor) +
            "\n  groom(antenna/head/leg/abdomen)=" + Format(lastCommand.FlyGroomAntenna) + "/" + Format(lastCommand.FlyGroomHead) + "/" + Format(lastCommand.FlyGroomLeg) + "/" + Format(lastCommand.FlyGroomAbdomen) +
            "\n  feed=" + Format(lastCommand.FlyFeed) + "  courtship=" + Format(lastCommand.FlyCourtship) + "  song=" + Format(lastCommand.FlySong) + "  song-pulse=" + Format(lastCommand.FlySongPulse) +
            "\n  leg-motor=" + Format(lastCommand.FlyLegMotor) + "  leg-asymmetry=" + Format(lastCommand.FlyLegMotorAsym) +
            "\nFILTERED NEURAL READOUT (fractions):\n  forward=" + Format(forwardFilter) + "  backward=" + Format(backwardFilter) + "  turn(R-L)=" + Format(yawFilter);

        public long SimulationTick => simulationTick;
        public int FiredCount => fired.Count;
        public IReadOnlyList<int> FiredNeurons => fired;
        public int ProcessedCount => processedThisStep;
        public int DroppedCount => droppedThisStep;
        public int DecayedCount => decayedThisStep;
        public int PendingCount => pendingIds.Count;
        public int ActiveCount => active.Count;
        public bool IsStopped => stopped;
        public FlyMotorCommand LastCommand => lastCommand;
        public RewardModulatedPlasticity Plasticity => plasticity;
        public ConnectomeLearningMode LearningMode => plasticity.Mode;
        public string LearningStatusText => plasticity.StatusText;
        public int LearnedSynapseCount => plasticity.ModifiedEdgeCount;

        public void SetLearningMode(ConnectomeLearningMode mode) => plasticity.SetMode(mode);
        /// <summary>Advances plasticity time before a reward for the completed interval.</summary>
        public void AdvanceLearningTime(float elapsedSeconds)
        {
            var seconds = GameElapsedSeconds(elapsedSeconds);
            plasticity.BeginStep(seconds);
            preAdvancedLearningSeconds += seconds;
        }
        public int ApplyReinforcement(float reward, float elapsedSeconds = DefaultStepSeconds) => plasticity.ApplyReward(reward, elapsedSeconds);
        public int ApplyFeedbackReinforcement(float reward) => plasticity.ApplyRewardImpulse(reward);
        public void ResetTransientLearningState()
        {
            plasticity.ResetTransientState();
            preAdvancedLearningSeconds = 0f;
        }
        public void ResetLearnedMemory() => plasticity.ResetLearning();
        public string SerializeLearnedMemory() => plasticity.Serialize();
        public bool TryLoadLearnedMemory(string serialized) => plasticity.TryLoad(serialized);

        public bool DidFire(int neuronId) => neuronId >= 0 && neuronId < potential.Length && firedPresent[neuronId];

        public int PopulationCount(string name) => string.IsNullOrEmpty(name) ? 0 : asset.Population(name).Count;

        public int PopulationFiredCount(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            var ids = asset.Population(name);
            var count = 0;
            for (var i = 0; i < ids.Count; i++) if (firedPresent[ids[i]]) count++;
            return count;
        }

        public FlyMotorCommand Step(SensoryFrame sensory) => Step(sensory, DefaultStepSeconds, null);

        public FlyMotorCommand Step(SensoryFrame sensory, float elapsedSeconds) => Step(sensory, elapsedSeconds, null);

        public FlyMotorCommand Step(SensoryFrame sensory, float elapsedSeconds, ManualInputState? manualInput)
        {
            if (sensory.BrainDead || (sensory.HealthValid && !sensory.Alive))
            {
                manualInput?.Deactivate();
                return Stop();
            }

            if (!sensory.HealthValid)
            {
                manualInput?.Deactivate();
                return SuspendOutput();
            }

            if (stopped)
            {
                stopped = false;
            }

            simulationTick++;
            var learningSeconds = GameElapsedSeconds(elapsedSeconds);
            if (preAdvancedLearningSeconds > 0f)
            {
                var alreadyAdvanced = Math.Min(preAdvancedLearningSeconds, learningSeconds);
                preAdvancedLearningSeconds -= alreadyAdvanced;
                learningSeconds -= alreadyAdvanced;
            }
            if (learningSeconds > 0f) plasticity.BeginStep(learningSeconds);
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = effectiveVisualThreatDrive = 0f;
            foodNearbyDrive = foodContactDrive = 0f;
            flyEscapeEvidenceSeconds = Math.Max(0f, flyEscapeEvidenceSeconds - GameElapsedSeconds(elapsedSeconds));
            sensoryQueued = 0;
            ClearFired();
            ClearPending(nextPending, nextPendingPresent, nextPendingIds);
            ClearActive(nextActivePresent, nextActive);
            ClearPriority();
            DriveSensoryPopulations(sensory, manualInput);

            ProcessActiveNeurons(nextPending, nextActivePresent);
            // A target can fire after an earlier source queued it this tick.
            // Clear only those fired IDs, rather than scanning the whole queue.
            foreach (var id in fired)
            {
                RemovePending(id, nextPending, nextPendingPresent);
                RemoveActive(id, nextActivePresent);
            }
            SwapPendingState();
            ClearPriority();
            manualInput?.FinishTick();

            lastCommand = BuildFlyMotorCommand(elapsedSeconds);
            return lastCommand;
        }

        private void ClearFired()
        {
            for (var i = 0; i < fired.Count; i++) firedPresent[fired[i]] = false;
            fired.Clear();
        }

        public FlyMotorCommand Stop()
        {
            if (!stopped)
            {
                Array.Clear(potential, 0, potential.Length);
                Array.Clear(refractoryUntil, 0, refractoryUntil.Length);
                ClearPending(pending, pendingPresent, pendingIds);
                ClearActive(activePresent, active);
                ClearPriority();
                ClearScheduled();
                ClearFired();
                ClearPending(nextPending, nextPendingPresent, nextPendingIds);
                ClearActive(nextActivePresent, nextActive);
                orderedActive.Clear();
                simulationTick = 0;
                backlogCursor = 0;
                plasticity.ResetTransientState();
                preAdvancedLearningSeconds = 0f;
                stopped = true;
            }

            LastSensoryDrive = 0f;
            hasPreviousLight = false;
            previousLight = lightOnDrive = lightOffDrive = 0f;
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = effectiveVisualThreatDrive = 0f;
            foodNearbyDrive = foodContactDrive = 0f;
            flyEscapeCooldown = flyEscapeQuiet = flyEscapeEvidenceSeconds = 0f;
            flyEscapeArmed = true;
            sensoryQueued = 0;
            lastCommand = new FlyMotorCommand();
            forwardFilter = backwardFilter = yawFilter = 0f;
            return lastCommand;
        }

        private FlyMotorCommand SuspendOutput()
        {
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            ClearFired();
            LastSensoryDrive = 0f;
            sensoryQueued = 0;
            hasPreviousLight = false;
            previousLight = lightOnDrive = lightOffDrive = 0f;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = effectiveVisualThreatDrive = 0f;
            foodNearbyDrive = foodContactDrive = 0f;
            flyEscapeCooldown = flyEscapeQuiet = flyEscapeEvidenceSeconds = 0f;
            flyEscapeArmed = true;
            lastCommand = new FlyMotorCommand();
            forwardFilter = backwardFilter = yawFilter = 0f;
            return lastCommand;
        }

        private void ProcessActiveNeurons(float[] next, bool[] nextActiveState)
        {
            // Integrate once per tick before propagating spikes. Subthreshold
            // inputs and residual decay must not compete with outgoing-edge work.
            // The active set is bounded by the loaded graph's neuron count.
            orderedActive.Clear();
            for (var index = 0; index < active.Count; index++)
            {
                var id = active[index];
                if (simulationTick < refractoryUntil[id]) { potential[id] = 0f; continue; }
                var input = pendingPresent[id] ? pending[id] : 0f;
                var updated = Clamp(potential[id] * .92f + Clamp(input, -4f, 4f), -8f, 8f);
                potential[id] = updated;
                if (input == 0f) decayedThisStep++;
                else processedThisStep++;
                if (updated >= 1f) orderedActive.Add(id);
                else if (Math.Abs(updated) > .001f) AddActive(id, nextActiveState);
            }
            if (orderedActive.Count > MaxSpikesPerStep)
            {
                ProcessFairSpikeBudget(next, nextActiveState);
                return;
            }

            foreach (var id in orderedActive) PropagateSpike(id, next, nextActiveState);
        }

        private void ProcessFairSpikeBudget(float[] next, bool[] nextActiveState)
        {
            // Only threshold crossings need sorting and fair propagation selection.
            orderedActive.Sort();
            var start = (int)(backlogCursor % orderedActive.Count);
            ClearScheduled();
            foreach (var id in orderedActive) AddScheduled(id);
            var budget = PropagatePrioritySpikes(next, nextActiveState, MaxSpikesPerStep);
            var examined = 0;
            PropagateRoundRobinSpikes(next, nextActiveState, start, budget, out examined);
            DropScheduledNeurons();
            ClearScheduled();

            backlogCursor = (start + examined) % orderedActive.Count;
        }

        private int PropagatePrioritySpikes(float[] next, bool[] nextActiveState, int budget)
        {
            for (var priorityIndex = 0; priorityIndex < priority.Count && budget > 0; priorityIndex++)
            {
                var id = priority[priorityIndex];
                if (!RemoveScheduled(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }

            return budget;
        }

        private void PropagateRoundRobinSpikes(float[] next, bool[] nextActiveState, int start, int budget, out int examined)
        {
            examined = 0;
            for (var index = 0; index < orderedActive.Count && budget > 0; index++)
            {
                var id = orderedActive[(start + index) % orderedActive.Count];
                examined++;
                if (!RemoveScheduled(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }
        }

        private void DropScheduledNeurons()
        {
            for (var scheduledIndex = 0; scheduledIndex < scheduled.Count; scheduledIndex++)
            {
                var id = scheduled[scheduledIndex];
                if (!scheduledPresent[id]) continue;
                if (simulationTick < refractoryUntil[id]) potential[id] = 0f;
                else DropNeuron(id);
            }
        }

        private void PropagateSpike(int id, float[] next, bool[] nextActiveState)
        {
            potential[id] = 0f;
            refractoryUntil[id] = simulationTick + RefractoryTicks + 1;
            fired.Add(id);
            firedPresent[id] = true;
            plasticity.RecordPresynaptic(id);
            plasticity.RecordPostsynaptic(id);
            QueueOutgoing(id, next, nextActiveState);
        }

        private void DropNeuron(int id)
        {
            droppedThisStep++;
            potential[id] = 0f;
        }

        private void AddActive(int id, bool[] membership)
        {
            if (membership[id]) return;
            membership[id] = true;
            (ReferenceEquals(membership, nextActivePresent) ? nextActive : active).Add(id);
        }

        private void AddPending(int id, float value)
        {
            if (!pendingPresent[id])
            {
                pendingPresent[id] = true;
                pendingIds.Add(id);
            }
            pending[id] += value;
        }

        private static void ClearPending(float[] values, bool[] membership, List<int> ids)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                membership[id] = false;
                values[id] = 0f;
            }
            ids.Clear();
        }

        private static void RemovePending(int id, float[] values, bool[] membership)
        {
            if (!membership[id]) return;
            membership[id] = false;
            values[id] = 0f;
        }

        private static void ClearActive(bool[] membership, List<int> ids)
        {
            for (var i = 0; i < ids.Count; i++) membership[ids[i]] = false;
            ids.Clear();
        }

        private static void RemoveActive(int id, bool[] membership)
        {
            if (!membership[id]) return;
            membership[id] = false;
        }

        private void ClearPriority()
        {
            for (var i = 0; i < priority.Count; i++) priorityPresent[priority[i]] = false;
            priority.Clear();
        }

        private void AddPriority(int id)
        {
            if (priorityPresent[id]) return;
            priorityPresent[id] = true;
            priority.Add(id);
            sensoryQueued++;
        }

        private void ClearScheduled()
        {
            for (var i = 0; i < scheduled.Count; i++) scheduledPresent[scheduled[i]] = false;
            scheduled.Clear();
        }

        private void AddScheduled(int id)
        {
            if (scheduledPresent[id]) return;
            scheduledPresent[id] = true;
            scheduled.Add(id);
        }

        private bool RemoveScheduled(int id)
        {
            if (!scheduledPresent[id]) return false;
            scheduledPresent[id] = false;
            return true;
        }

        private void QueueOutgoing(int source, float[] next, bool[] nextActiveState)
        {
            var start = asset.OutgoingStart(source);
            var end = asset.OutgoingEnd(source);
            var sign = asset.SignAt(source);
            for (var edge = start; edge < end; edge++)
            {
                var target = asset.TargetAt(edge);
                // Propagated input arrives on the next tick. Allow input on
                // the exact recovery tick; earlier input cannot be integrated.
                if (simulationTick + 1 < refractoryUntil[target]) continue;
                var exists = nextPendingPresent[target];
                next[target] += plasticity.EffectiveWeight(edge, asset.WeightAt(edge), sign);
                // Existing pending entries already have an active target.
                if (!exists)
                {
                    nextPendingPresent[target] = true;
                    nextPendingIds.Add(target);
                    AddActive(target, nextActiveState);
                }
            }
        }

        private void SwapPendingState()
        {
            Compact(nextPendingIds, nextPendingPresent);
            Compact(nextActive, nextActivePresent);
            var previousPending = pending;
            pending = nextPending;
            nextPending = previousPending;
            var previousPendingPresent = pendingPresent;
            pendingPresent = nextPendingPresent;
            nextPendingPresent = previousPendingPresent;
            var previousPendingIds = pendingIds;
            pendingIds = nextPendingIds;
            nextPendingIds = previousPendingIds;

            var previousActive = active;
            active = nextActive;
            nextActive = previousActive;
            var previousActivePresent = activePresent;
            activePresent = nextActivePresent;
            nextActivePresent = previousActivePresent;
            ClearPending(nextPending, nextPendingPresent, nextPendingIds);
            ClearActive(nextActivePresent, nextActive);
        }

        private static void Compact(List<int> ids, bool[] membership)
        {
            for (var i = 0; i < ids.Count;)
            {
                if (membership[ids[i]])
                {
                    i++;
                    continue;
                }

                var last = ids.Count - 1;
                ids[i] = ids[last];
                ids.RemoveAt(last);
            }
        }

        private FlyMotorCommand BuildFlyMotorCommand(float elapsedSeconds)
        {
            // Reference MotorMap walking/halting populations. Fractions below are
            // latest-tick activity, not the upstream biological firing-rate decoder.
            var rawForward = MotorActivity("type:DNp09") * .3f + MotorActivity("type:DNg100") * .25f +
                MotorActivity("type:DNge053") * .15f + MotorActivity("type:DNge050") * .15f + MotorActivity("type:DNg97") * .15f;
            var rawBackward = MotorActivity("type:MDN");
            var rawYaw = MotorActivity("type:DNa02", "R") - MotorActivity("type:DNa02", "L") +
                .5f * (MotorActivity("type:DNg13", "R") - MotorActivity("type:DNg13", "L")) +
                .25f * (MotorActivity("type:DNa01", "R") - MotorActivity("type:DNa01", "L"));
            var halt = Math.Max(MotorActivity("type:DNg60"), Math.Max(MotorActivity("type:DNg74_a"), MotorActivity("type:DNg74_b")));
            var brake = PopulationActivity("type:AN19A018");
            if (damageDrive > .05f || effectiveVisualThreatDrive > .05f)
            {
                flyEscapeEvidenceSeconds = .5f;
            }
            var stopRequested = halt >= .2f || brake >= .2f;
            if (stopRequested)
            {
                forwardFilter = backwardFilter = yawFilter = 0f;
            }
            else
            {
                forwardFilter = Ema(forwardFilter, rawForward, elapsedSeconds, .15f);
                backwardFilter = Ema(backwardFilter, rawBackward, elapsedSeconds, .15f);
                yawFilter = Ema(yawFilter, rawYaw, elapsedSeconds, .1f);
            }

            var command = new FlyMotorCommand();
            var dnp01Activity = FiredMotorPopulation("type:DNp01");
            PopulateFlyRequests(ref command, dnp01Activity, halt, brake);
            // A strong resolved looming signal is already neural input to LC4 /
            // LPLC2. Qualify the same bounded, refractory escape reflex even if
            // the sparse connectome does not happen to fire DNp01 on that exact
            // scheduler tick; weaker cues still require descending-neuron output.
            var strongVisualDanger = effectiveVisualThreatDrive >= .65f;
            command.FlyEscape = DecodeFlyEscape(elapsedSeconds, dnp01Activity || strongVisualDanger);
            return command;
        }

        private void PopulateFlyRequests(ref FlyMotorCommand command, bool dnp01Activity, float halt, float brake)
        {
            var leftLeg = MotorActivity(FlyLegPopulation, "L");
            var rightLeg = MotorActivity(FlyLegPopulation, "R");
            command.FlyForward = Signed(forwardFilter);
            command.FlyYaw = Signed(yawFilter);
            command.FlyBackward = Unit(backwardFilter);
            // Carry the same activity that made this tick halt internally.
            // Consumers can therefore apply the documented .2 threshold without
            // losing fractional DNg60/DNg74 halt requests to a diagnostic mix.
            command.FlyHalt = Unit(halt);
            command.FlyBrake = Unit(brake);
            command.FlyJump = dnp01Activity ? 1f : 0f;
            command.FlyTakeoff = WeightedMotorActivity("type:DNp11", .5f, "type:DNp02", .25f, "type:DNp04", .25f);
            command.FlyLanding = WeightedMotorActivity("type:DNp07", .5f, "type:DNp10", .5f);
            command.FlyFlightPower = MotorActivity("type:DNg02");
            command.FlyFlightYaw = Signed((MotorActivity("type:DNg02", "L") - MotorActivity("type:DNg02", "R") +
                MotorActivity("type:DNp03", "R") - MotorActivity("type:DNp03", "L")) * .5f);
            command.FlyWingMotor = MotorActivity("subclass:wm");
            command.FlyGroomAntenna = AverageMotorActivity("type:DNg62", "type:DNge078");
            command.FlyGroomHead = AverageMotorActivity("type:DNg12", "type:DNg07", "type:DNg08");
            command.FlyGroomLeg = MotorActivity("type:DNg11");
            command.FlyGroomAbdomen = MotorActivity("type:DNp29");
            command.FlyFeed = Unit(WeightedMotorActivity("type:MN9", .6f, "type:DNg67", .1f, "type:DNge080", .1f) +
                WeightedMotorActivity("type:DNge173", .1f, "type:DNge174", .1f));
            // pC1/P1 courtship neurons are central-brain intrinsic cells in
            // MaleCNS, so this explicitly selected population must not use the
            // locomotor motor-neuron filter.
            command.FlyCourtship = PopulationActivity("prefix:pC1_");
            command.FlySong = MotorActivity("type:pIP10");
            command.FlySongPulse = MotorActivity("type:pMP2");
            command.FlyLegMotor = Unit((leftLeg + rightLeg) * .5f);
            command.FlyLegMotorAsym = Signed(rightLeg - leftLeg);
        }

        private float WeightedMotorActivity(string first, float firstWeight, string second, float secondWeight)
        {
            return Unit(MotorActivity(first) * firstWeight + MotorActivity(second) * secondWeight);
        }

        private float WeightedMotorActivity(string first, float firstWeight, string second, float secondWeight,
            string third, float thirdWeight)
        {
            return Unit(MotorActivity(first) * firstWeight + MotorActivity(second) * secondWeight + MotorActivity(third) * thirdWeight);
        }

        private float AverageMotorActivity(string first, string second) =>
            (MotorActivity(first) + MotorActivity(second)) * .5f;

        private float AverageMotorActivity(string first, string second, string third) =>
            (MotorActivity(first) + MotorActivity(second) + MotorActivity(third)) / 3f;


        private bool FiredMotorPopulation(string population)
        {
            var ids = asset.Population(population);
            for (var i = 0; i < ids.Count; i++)
            {
                if (firedPresent[ids[i]] && IsMotorNeuron(ids[i])) return true;
            }

            return false;
        }

        private float DecodeFlyEscape(float elapsedSeconds, bool dnp01Activity)
        {
            const float RefractorySeconds = 1.5f;
            const float RearmQuietSeconds = .25f;
            var seconds = GameElapsedSeconds(elapsedSeconds);
            flyEscapeCooldown = Math.Max(0f, flyEscapeCooldown - seconds);
            if (dnp01Activity)
            {
                flyEscapeQuiet = 0f;
                if (flyEscapeEvidenceSeconds > 0f && flyEscapeArmed && flyEscapeCooldown <= 0f)
                {
                    flyEscapeArmed = false;
                    flyEscapeCooldown = RefractorySeconds;
                    return 1f;
                }

                return 0f;
            }

            flyEscapeQuiet = Math.Min(RearmQuietSeconds, flyEscapeQuiet + seconds);
            if (flyEscapeCooldown <= 0f && flyEscapeQuiet >= RearmQuietSeconds)
            {
                flyEscapeArmed = true;
            }

            return 0f;
        }


        private static float GameElapsedSeconds(float value) => IsFinite(value) && value > 0f ? value : DefaultStepSeconds;
        private static float SmoothingElapsedSeconds(float value) => Clamp(GameElapsedSeconds(value), 0f, .25f);
        private static float Ema(float prior, float target, float elapsedSeconds, float tauSeconds)
        {
            var alpha = 1f - (float)Math.Exp(-SmoothingElapsedSeconds(elapsedSeconds) / tauSeconds);
            return prior + alpha * (target - prior);
        }

        private float PopulationActivity(string population, string? side = null, bool motorOnly = false)
        {
            var ids = asset.Population(population);
            var count = 0;
            var firing = 0;
            foreach (var id in ids)
            {
                if (side != null && asset.SideAt(id) != side) continue;
                if (motorOnly && !IsMotorNeuron(id)) continue;
                count++;
                if (firedPresent[id]) firing++;
            }
            return count == 0 ? 0f : (float)firing / count;
        }

        private float MotorActivity(string population, string? side = null) => PopulationActivity(population, side, true);

        private bool IsMotorNeuron(int id)
        {
            var superclass = asset.SuperclassAt(id);
            return superclass == "descending_neuron" || superclass == "vnc_motor" || superclass == "cb_motor";
        }

        private void DriveSensoryPopulations(SensoryFrame sensory, ManualInputState? manualInput)
        {
            var effectiveTotal = 0f;
            var lightValid = PrepareSensoryDrives(sensory);
            effectiveTotal += DriveLightChannels(sensory, manualInput);
            effectiveTotal += DriveTouchChannels(sensory, manualInput);
            effectiveTotal += DriveBodyChannels(sensory, manualInput);
            effectiveTotal += DriveVisualChannels(sensory, manualInput);
            opticRollDrive = ResolveOpticRoll(sensory, lightValid);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.OpticRoll, opticRollDrive, 0f);
            LastSensoryDrive = Unit(effectiveTotal);
        }

        private bool PrepareSensoryDrives(SensoryFrame sensory)
        {
            var lightValid = sensory.LightValid && IsFinite(sensory.Light);
            lightDrive = lightValid ? Unit(sensory.Light) : 0f;
            var lightChange = lightValid && hasPreviousLight ? lightDrive - previousLight : 0f;
            lightOnDrive = Unit(lightChange);
            lightOffDrive = Unit(-lightChange);
            previousLight = lightDrive;
            hasPreviousLight = lightValid;
            audioDrive = sensory.SoundSpectrumValid ? Math.Max(Unit(sensory.SoundHigh), Unit(sensory.SoundLow) * .7f) : Unit(sensory.Sound);
            touchDrive = Math.Max(Unit(Unit(sensory.Impact) + Unit(sensory.Vibration) * .35f), sensory.RegionalTouchValid ? 0f : Unit(Unit(sensory.Touch) * .15f + Unit(sensory.PhysicalContact) * .15f));
            gravityDrive = sensory.TiltValid ? Math.Abs(Signed(sensory.SignedTilt)) : 0f;
            var jointPosition = sensory.JointSensingValid ? Unit(sensory.JointPosition) : 0f;
            var jointMotion = sensory.JointSensingValid ? Unit(sensory.JointMotion) : 0f;
            var jointLoad = sensory.JointSensingValid ? Unit(sensory.NeuralJointLoad) : 0f;
            jointDrive = Math.Max(jointPosition, Math.Max(jointMotion, jointLoad));
            hotDrive = Math.Max(Unit(sensory.Heat), Unit(sensory.AmbientHeat));
            coldDrive = Math.Max(Unit(sensory.Cold), Unit(sensory.AmbientCold));
            approachDrive = sensory.VisualGeometryValid ? 0f : Unit(sensory.VisualApproach);
            var damage = Unit(sensory.DamageEvent);
            var damagePower = (float)Math.Pow(damage / .2f, 1.5);
            damageDrive = Unit(2f * damagePower / (1f + damagePower));
            regionalTouchDrive = sensory.RegionalTouchValid ? Math.Max(Math.Max(Unit(sensory.TouchHead), Unit(sensory.TouchArms)), Math.Max(Unit(sensory.TouchLegs), Unit(sensory.TouchCore))) * .15f : 0f;
            return lightValid;
        }

        private float DriveLightChannels(SensoryFrame sensory, ManualInputState? manualInput)
        {
            var total = DriveChannel(manualInput, ManualInputChannel.Light, lightDrive, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.LightOn, lightOnDrive, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.LightOff, lightOffDrive, 0f);
            var direction = sensory.SoundDirectionValid ? Signed(sensory.SoundDirection) : 0f;
            return total + DriveChannel(manualInput, ManualInputChannel.Auditory, audioDrive, direction);
        }

        private float DriveTouchChannels(SensoryFrame sensory, ManualInputState? manualInput)
        {
            var total = 0f;
            var broadTouch = Math.Max(touchDrive, damageDrive);
            var hasRegionalTargets = asset.Population("input:touch-head").Count != 0 || asset.Population("input:touch-arms").Count != 0;
            if (!hasRegionalTargets) return DriveChannel(manualInput, ManualInputChannel.TouchOther, broadTouch, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.TouchHead, Math.Max(broadTouch, RegionalTouch(sensory.TouchHead, sensory.RegionalTouchValid)), 0f);
            total += DriveChannel(manualInput, ManualInputChannel.TouchArms, Math.Max(broadTouch, RegionalTouch(sensory.TouchArms, sensory.RegionalTouchValid)), 0f);
            total += DriveChannel(manualInput, ManualInputChannel.TouchLegs, Math.Max(broadTouch, RegionalTouch(sensory.TouchLegs, sensory.RegionalTouchValid)), 0f);
            total += DriveChannel(manualInput, ManualInputChannel.TouchCore, Math.Max(broadTouch, RegionalTouch(sensory.TouchCore, sensory.RegionalTouchValid)), 0f);
            return total + DriveChannel(manualInput, ManualInputChannel.TouchOther, broadTouch, 0f);
        }

        private float DriveBodyChannels(SensoryFrame sensory, ManualInputState? manualInput)
        {
            var total = DriveChannel(manualInput, ManualInputChannel.Gravity, gravityDrive, sensory.TiltValid ? Signed(sensory.SignedTilt) : 0f);
            total += DriveChannel(manualInput, ManualInputChannel.JointPosition, sensory.JointSensingValid ? Unit(sensory.JointPosition) : 0f, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.JointMotion, sensory.JointSensingValid ? Unit(sensory.JointMotion) : 0f, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.JointLoad, sensory.JointSensingValid ? Unit(sensory.NeuralJointLoad) : 0f, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.Warm, hotDrive, 0f);
            total += DriveChannel(manualInput, ManualInputChannel.Cool, coldDrive, 0f);
            foodNearbyDrive = sensory.FoodCuesValid ? Unit(sensory.FoodNearbyCue) : 0f;
            foodContactDrive = sensory.FoodCuesValid ? Unit(sensory.FoodContactCue) : 0f;
            total += DriveChannel(manualInput, ManualInputChannel.FoodNearby, foodNearbyDrive, 0f);
            return total + DriveChannel(manualInput, ManualInputChannel.FoodContact, foodContactDrive, 0f);
        }

        private float DriveVisualChannels(SensoryFrame sensory, ManualInputState? manualInput)
        {
            var expansion = new VisualFeatureInput();
            var looming = new VisualFeatureInput();
            var small = new VisualFeatureInput();
            if (sensory.VisualFieldValid)
            {
                for (var band = 0; band < 5; band++)
                {
                    var view = sensory.ViewAt(band);
                    if (view.Observed && Unit(view.Strength) > 0f && IsFinite(view.BearingDegrees) && Math.Abs(view.BearingDegrees) <= 90f)
                        AddVisualFeatures(view, Signed(view.BearingDegrees / 90f), ref expansion, ref looming, ref small);
                }
            }
            else
            {
                var direction = sensory.VisionHeadBearingValid ? Signed(sensory.VisionHeadBearingDegrees / 90f) : 0f;
                AddVisualFeatures(new VisualObservation { Strength = sensory.Vision, Approach = sensory.VisualApproach, GeometryValid = sensory.VisualGeometryValid, AngularSize = sensory.VisualAngularSize, Expansion = sensory.VisualExpansion, AngularSpeed = sensory.VisualAngularSpeed }, direction, ref expansion, ref looming, ref small);
            }
            if (sensory.LearnedThreatValid)
            {
                var learnedThreat = Unit(sensory.LearnedThreat);
                var learnedDirection = Signed(sensory.LearnedThreatDirection);
                expansion.Add(learnedThreat, learnedDirection);
                looming.Add(learnedThreat, learnedDirection);
            }
            var projectileThreat = Unit(sensory.Projectile);
            if (projectileThreat > 0f)
            {
                var projectileDirection = sensory.VisionHeadBearingValid ?
                    Signed(sensory.VisionHeadBearingDegrees / 90f) : 0f;
                looming.Add(projectileThreat, projectileDirection);
            }
            smallVisualDrive = small.Amplitude;
            approachDrive = Math.Max(expansion.Amplitude, looming.Amplitude);
            var effectiveExpansion = DriveChannel(manualInput, ManualInputChannel.ExpandingVisual, expansion.Amplitude, expansion.Direction);
            var effectiveLooming = DriveChannel(manualInput, ManualInputChannel.LoomingVisual, looming.Amplitude, looming.Direction);
            // Escape qualification must reflect the same resolved visual
            // channels that are injected into the neural graph. Keep the
            // unmodified approach drive above for native-world diagnostics.
            effectiveVisualThreatDrive = Math.Max(effectiveExpansion, effectiveLooming);
            return effectiveExpansion + effectiveLooming + DriveChannel(manualInput, ManualInputChannel.SmallMovingVisual, small.Amplitude, small.Direction);
        }

        private static float RegionalTouch(float value, bool available) => available ? Unit(value) * .15f : 0f;

        private float ResolveOpticRoll(SensoryFrame sensory, bool lightValid)
        {
            if (!lightValid || lightDrive <= 0f || !sensory.AngularVelocityValid || !IsFinite(sensory.AngularVelocity)) return 0f;
            return Math.Abs(sensory.AngularVelocity) / (Math.Abs(sensory.AngularVelocity) + 300f) * lightDrive;
        }

        private struct VisualFeatureInput
        {
            private float left, right;
            public readonly float Amplitude => Math.Max(left, right);
            public float Direction => Amplitude > 0f ? (right - left) / Amplitude : 0f;
            public void Add(float amplitude, float direction)
            {
                amplitude = Unit(amplitude);
                // Max pooling keeps additional colliders from multiplying drive
                // while preserving simultaneous features on both neural sides.
                left = Math.Max(left, amplitude * (1f - Math.Max(0f, direction)));
                right = Math.Max(right, amplitude * (1f + Math.Min(0f, direction)));
            }
        }

        private static void AddVisualFeatures(VisualObservation view, float direction,
            ref VisualFeatureInput expansionInput, ref VisualFeatureInput loomingInput, ref VisualFeatureInput smallInput)
        {
            if (!view.GeometryValid)
            {
                expansionInput.Add(view.Approach, direction);
                loomingInput.Add(view.Approach, direction);
                return;
            }
            var vision = Unit(view.Strength);
            if (vision <= 0f || !IsFinite(view.Expansion) || !IsFinite(view.AngularSize) || !IsFinite(view.AngularSpeed) ||
                view.AngularSize <= 0f || view.AngularSize > 180f || view.AngularSpeed < 0f) return;
            var expansion = Math.Max(0f, view.Expansion);
            var z = (view.AngularSize - 60f) / 25f;
            expansionInput.Add(expansion / (expansion + 200f) * vision, direction);
            loomingInput.Add(expansion > 0f ? (float)Math.Exp(-.5f * z * z) * vision : 0f, direction);
            smallInput.Add(view.AngularSize < 15f && view.AngularSpeed > 5f ?
                view.AngularSpeed / (view.AngularSpeed + 100f) * vision : 0f, direction);
        }

        private float LastSensoryDrive { get; set; }

        private float DriveChannel(ManualInputState? manualInput, ManualInputChannel channel, float live, float direction)
        {
            var descriptor = ManualInputCatalog.For(channel);
            var primaryAvailable = false;
            for (var i = 0; i < descriptor.Targets.Length; i++)
            {
                if (asset.Population(descriptor.Targets[i]).Count > 0)
                {
                    primaryAvailable = true;
                    break;
                }
            }

            var fallbackTarget = descriptor.FallbackTarget;
            var fallbackAvailable = !primaryAvailable && fallbackTarget != null && asset.Population(fallbackTarget).Count > 0;
            var effective = manualInput == null ? Unit(live) : manualInput.Resolve(channel, live, direction, primaryAvailable || fallbackAvailable);
            var effectiveDirection = manualInput == null ? direction : manualInput.GetReading(channel).EffectiveDirection;
            if (effective <= .001f || (!primaryAvailable && !fallbackAvailable)) return effective;

            if (primaryAvailable)
            {
                for (var i = 0; i < descriptor.Targets.Length; i++) Drive(descriptor.Targets[i], effective, effectiveDirection);
            }
            else if (fallbackTarget != null)
            {
                Drive(fallbackTarget, effective, effectiveDirection);
            }

            return effective;
        }

        private void Drive(string population, float value, float direction = 0f)
        {
            value = Unit(value);
            if (value <= .001f) return;
            var ids = asset.Population(population);
            // All annotated members are eligible; a first-N cutoff systematically
            // favored lower IDs and could omit one side of a population.
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (simulationTick < refractoryUntil[id]) continue;
                var side = asset.SideAt(id);
                var gain = SideGain(side, direction);
                var input = value * gain;
                if (input <= .001f) continue;
                AddPending(id, input);
                AddActive(id, activePresent);
                if (!priorityPresent[id]) AddPriority(id);
            }
        }

        private static float Unit(float value) => IsFinite(value) ? Clamp(value, 0f, 1f) : 0f;
        private static float Signed(float value) => IsFinite(value) ? Clamp(value, -1f, 1f) : 0f;
        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SideGain(string side, float direction)
        {
            if (side == "L") return 1f - Math.Max(0f, direction);
            if (side == "R") return 1f + Math.Min(0f, direction);
            return 1f;
        }
        private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
