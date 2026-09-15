using System;
using System.Collections.Generic;
using System.Globalization;
using Mod.UI;

namespace Mod.Core
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
        private MotorCommand lastCommand;
        private bool stopped;
        private long simulationTick;
        private long backlogCursor;
        private int processedThisStep;
        private int droppedThisStep;
        private int decayedThisStep;
        private float lightDrive, audioDrive, touchDrive, damageDrive, regionalTouchDrive, smallVisualDrive, opticRollDrive, gravityDrive, jointDrive, hotDrive, coldDrive, approachDrive;
        private float visualThreat, neuralEscape;
        private float neuralEscapeCooldown, neuralEscapeQuiet, escapeContextSeconds;
        private float escapeWalkSeconds;
        private int escapeWalkDirection;
        private float foodNearbyDrive, foodContactDrive;
        private bool neuralEscapeArmed = true;
        private int sensoryQueued;
        private bool hasPreviousLight;
        private float previousLight, lightOnDrive, lightOffDrive;
        private float forwardFilter, backwardFilter, yawFilter, leftLegFilter, rightLegFilter, locomotionDwell;
        private int locomotionMode;
        private const int MaxSpikesPerStep = 24000;
        private const int RefractoryTicks = 5;
        private const float DefaultStepSeconds = .05f;
        // Requests are consumed by the game's fixed-step motor adapter.  Keeping
        // their change rate bounded avoids alternating full-strength joint input
        // on consecutive neural ticks while retaining a responsive control loop.
        private const float MotorChangePerSecond = 8f;

        private LifBrain(IConnectomeAsset asset)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
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
            " request-walk=" + Format(lastCommand.Walk));

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
                    (droppedThisStep > 0 ? "\nFiring-event limit reached; dropped spikes are not deferred. This is not a CPU-time measurement." : "");
            }
        }

        public string DisplayInputSummary => stopped ? "INPUT: STOPPED" :
            "LIVE ENCODER REQUESTS (normalized amplitudes, not Hz):\n  light=" + Format(lightDrive) + "  audio=" + Format(audioDrive) +
            "  broad-touch=" + Format(touchDrive) + "  injury-proxy=" + Format(damageDrive) + "  regional-touch=" + Format(regionalTouchDrive) +
            "\n  gravity-proxy=" + Format(gravityDrive) + "  joints=" + Format(jointDrive) + "  small-visual=" + Format(smallVisualDrive) + "  optic-roll=" + Format(opticRollDrive) +
            "\n  light-level change ON=" + Format(lightOnDrive) + "  OFF=" + Format(lightOffDrive) +
            "\n  warm=" + Format(hotDrive) + "  cool=" + Format(coldDrive) + "  approach-proxy=" + Format(approachDrive) +
            "\n  food-nearby(gameplay)=" + Format(foodNearbyDrive) + "  food-head-contact(gameplay)=" + Format(foodContactDrive) +
            "\n  input neurons queued this tick=" + sensoryQueued +
            "\nFood cues are explicit catalog-based gameplay mappings, not measured smell/taste. Health/chemistry remain telemetry and control constraints.\nVisual directions use head-relative 2D bearing (positive CCW to R); audio/tilt use world horizontal. These are engineering projections.";

        public string DisplayMotorSummary => stopped ?
            "REQUEST: STOPPED\n  arms=0.00/0.00  legs=0.00/0.00\n  head=0.00  core=0.00  grips=0.00/0.00\nTHREAT SOURCES\n  body=0.00  injury-event=0.0000  looming=0.00  DNp01-fired=0.00  escape-request=0.00  response=stopped" :
            "REQUEST (" + RequestLabel() + "):\n  arms=" + Format(lastCommand.LeftArm) + "/" + Format(lastCommand.RightArm) +
            "  legs=" + Format(lastCommand.LeftLeg) + "/" + Format(lastCommand.RightLeg) +
            "\n  head=" + Format(lastCommand.Head) + "  core=" + Format(lastCommand.Core) +
            "  grips=" + Format(lastCommand.LeftGrip) + "/" + Format(lastCommand.RightGrip) +
            "\n  escape-burst-remaining=" + Format(lastCommand.EscapeLocomotionSeconds) + " s (native walking adaptation; shooter direction unknown)" +
            "\nTHREAT SOURCES\n  body=" + Format(lastCommand.BodyThreat) + "  injury-event=" + lastCommand.InjuryEvent.ToString("0.0000", CultureInfo.InvariantCulture) + "  looming=" + Format(lastCommand.VisualThreat) +
            "  DNp01-fired=" + Format(lastCommand.DNp01Activity) + "  escape-request=" + Format(lastCommand.NeuralEscape) + "  recent-threat-window=" + Format(escapeContextSeconds) + " s  response=" + ResponseLabel() +
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
        public MotorCommand LastCommand => lastCommand;

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

        public MotorCommand Step(SensoryFrame sensory) => Step(sensory, DefaultStepSeconds, null);

        public MotorCommand Step(SensoryFrame sensory, float elapsedSeconds) => Step(sensory, elapsedSeconds, null);

        public MotorCommand Step(SensoryFrame sensory, float elapsedSeconds, ManualInputState? manualInput)
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
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
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
                RemovePending(id, nextPending, nextPendingPresent, nextPendingIds);
                RemoveActive(id, nextActivePresent, nextActive);
            }
            SwapPendingState();
            ClearPriority();
            manualInput?.FinishTick();

            return BuildMotorCommand(sensory, elapsedSeconds);
        }

        private void ClearFired()
        {
            for (var i = 0; i < fired.Count; i++) firedPresent[fired[i]] = false;
            fired.Clear();
        }

        public MotorCommand Stop()
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
                stopped = true;
            }

            LastSensoryDrive = 0f;
            hasPreviousLight = false;
            previousLight = lightOnDrive = lightOffDrive = 0f;
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
            neuralEscapeCooldown = neuralEscapeQuiet = escapeContextSeconds = escapeWalkSeconds = 0f;
            neuralEscapeArmed = true;
            sensoryQueued = 0;
            lastCommand = new MotorCommand();
            forwardFilter = backwardFilter = yawFilter = leftLegFilter = rightLegFilter = locomotionDwell = 0f;
            locomotionMode = 0;
            return lastCommand;
        }

        private MotorCommand SuspendOutput()
        {
            processedThisStep = 0;
            droppedThisStep = 0;
            decayedThisStep = 0;
            ClearFired();
            LastSensoryDrive = 0f;
            sensoryQueued = 0;
            hasPreviousLight = false;
            previousLight = lightOnDrive = lightOffDrive = 0f;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
            neuralEscapeCooldown = neuralEscapeQuiet = escapeContextSeconds = escapeWalkSeconds = 0f;
            neuralEscapeArmed = true;
            lastCommand = new MotorCommand();
            forwardFilter = backwardFilter = yawFilter = leftLegFilter = rightLegFilter = locomotionDwell = 0f;
            locomotionMode = 0;
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
            var budget = MaxSpikesPerStep;
            for (var priorityIndex = 0; priorityIndex < priority.Count; priorityIndex++)
            {
                if (budget == 0) break;
                var id = priority[priorityIndex];
                if (!RemoveScheduled(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }

            var examined = 0;
            for (var index = 0; index < orderedActive.Count && budget > 0; index++)
            {
                var id = orderedActive[(start + index) % orderedActive.Count];
                examined++;
                if (!RemoveScheduled(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }

            for (var scheduledIndex = 0; scheduledIndex < scheduled.Count; scheduledIndex++)
            {
                var id = scheduled[scheduledIndex];
                if (!scheduledPresent[id]) continue;
                if (simulationTick < refractoryUntil[id]) potential[id] = 0f;
                else DropNeuron(id);
            }
            ClearScheduled();

            backlogCursor = (start + examined) % orderedActive.Count;
        }

        private void PropagateSpike(int id, float[] next, bool[] nextActiveState)
        {
            potential[id] = 0f;
            refractoryUntil[id] = simulationTick + RefractoryTicks + 1;
            fired.Add(id);
            firedPresent[id] = true;
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

        private static void RemovePending(int id, float[] values, bool[] membership, List<int> ids)
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

        private static void RemoveActive(int id, bool[] membership, List<int> ids)
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
                next[target] += asset.WeightAt(edge) * sign;
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

        private MotorCommand BuildMotorCommand(SensoryFrame sensory, float elapsedSeconds)
        {
            // DNp01 can fire from recurrent activity without an observed threat.
            // Keep the spike visible, but qualify its Human avoidance request
            // with recent evidence. This is a gameplay gate, not causal tracing
            // or a biological claim about why the neuron fired.
            var bodyThreatValue = BodyThreatValue(sensory);
            var freeze = Unit(Unit(sensory.Unconscious) + Unit(sensory.LiquidSedation));
            var movementPermitted = IsMovementPermitted(sensory, freeze);
            var dnp01Activity = FiredMotorPopulation("type:DNp01");
            var escapeEvidence = bodyThreatValue > .5f || Unit(sensory.DamageEvent) > .001f || visualThreat > .05f;
            var neuralEscapeValue = DecodeNeuralEscape(elapsedSeconds, dnp01Activity, escapeEvidence, movementPermitted);
            neuralEscape = neuralEscapeValue;
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
            var stopRequested = halt >= .2f || brake >= .2f;
            UpdateLocomotion(rawForward, rawBackward, rawYaw, neuralEscapeValue, stopRequested, movementPermitted, elapsedSeconds);
            var requested = CreateMotorCommand(sensory, elapsedSeconds, movementPermitted, stopRequested,
                new MotorCommandContext(bodyThreatValue, neuralEscapeValue, dnp01Activity, freeze));
            if (!movementPermitted || stopRequested)
            {
                ClearMotorRequests(ref requested);
                forwardFilter = backwardFilter = yawFilter = leftLegFilter = rightLegFilter = locomotionDwell = 0f;
                locomotionMode = 0;
                escapeWalkSeconds = 0f;
                lastCommand = requested;
                return lastCommand;
            }

            lastCommand = SmoothMotorRequest(requested, elapsedSeconds);
            return lastCommand;
        }

        private void UpdateLocomotion(float rawForward, float rawBackward, float rawYaw, float neuralEscapeValue,
            bool stopRequested, bool movementPermitted, float elapsedSeconds)
        {
            var escapeElapsed = IsFinite(elapsedSeconds) && elapsedSeconds > 0f ? elapsedSeconds : DefaultStepSeconds;
            escapeWalkSeconds = Math.Max(0f, escapeWalkSeconds - escapeElapsed);
            if (neuralEscapeValue > 0f && !stopRequested && movementPermitted)
            {
                escapeWalkSeconds = .6f;
                escapeWalkDirection = rawBackward >= .5f || locomotionMode < 0 ? -1 : 1;
            }
            if (stopRequested)
            {
                locomotionMode = 0;
                locomotionDwell = 0f;
                return;
            }

            forwardFilter = Ema(forwardFilter, rawForward, elapsedSeconds, .15f);
            backwardFilter = Ema(backwardFilter, rawBackward, elapsedSeconds, .15f);
            yawFilter = Ema(yawFilter, rawYaw, elapsedSeconds, .1f);
            locomotionDwell += ElapsedSeconds(elapsedSeconds);
            if (rawBackward >= .5f)
            {
                if (locomotionMode != -1)
                {
                    locomotionMode = -1;
                    locomotionDwell = 0f;
                }
            }
            else if (locomotionMode == 0 && forwardFilter >= .08f)
            {
                locomotionMode = 1;
                locomotionDwell = 0f;
            }
            else if (locomotionDwell >= .25f && ShouldStopLocomotion())
            {
                locomotionMode = 0;
                locomotionDwell = 0f;
            }
        }

        private bool ShouldStopLocomotion() => locomotionMode > 0 ? forwardFilter < .04f : backwardFilter < .25f;

        private MotorCommand CreateMotorCommand(SensoryFrame sensory, float elapsedSeconds, bool movementPermitted,
            bool stopRequested, MotorCommandContext context)
        {
            var locomotionGate = stopRequested ? 0f : 1f;
            var neuralWalk = ResolveNeuralWalk();
            // Fly leg activity is a human joint-control proxy. Wing, song and
            // proboscis populations are not reinterpreted as human arm/grip intent.
            var left = Ema(leftLegFilter, MotorActivity("motor:leg", "L"), elapsedSeconds, .15f) * locomotionGate;
            var right = Ema(rightLegFilter, MotorActivity("motor:leg", "R"), elapsedSeconds, .15f) * locomotionGate;
            leftLegFilter = left;
            rightLegFilter = right;
            var center = (left + right) * .5f;
            var sideBias = right - left;
            var walk = movementPermitted ? neuralWalk : 0f;
            var motorSideBias = movementPermitted ? sideBias : 0f;
            var motorCenter = movementPermitted ? center : 0f;
            var armSwing = Signed((left - right) * .75f * (movementPermitted ? 1f : 0f) + walk * .35f + motorCenter * .15f);
            return new MotorCommand
            {
                Walk = walk,
                EscapeLocomotionSeconds = escapeWalkSeconds,
                LeftArm = Signed(-armSwing),
                RightArm = armSwing,
                LeftLeg = Signed(walk - motorSideBias * .2f),
                RightLeg = Signed(walk + motorSideBias * .2f),
                Core = Signed(walk * .6f + motorCenter * .15f + yawFilter * .25f),
                Head = Signed(motorSideBias * .5f + yawFilter),
                Avoid = context.BodyThreat > .5f || context.NeuralEscape > 0f ? 1f : 0f,
                BodyThreat = context.BodyThreat,
                InjuryEvent = Unit(sensory.DamageEvent),
                VisualThreat = visualThreat,
                NeuralEscape = context.NeuralEscape,
                DNp01Activity = context.DNp01Activity ? 1f : 0f,
                Freeze = context.Freeze,
                Heal = Unit(Unit(sensory.Damage) + Unit(sensory.Bleeding) + Unit(sensory.LiquidHealing)),
                Stimulate = Unit(sensory.LiquidStimulation),
                Calm = Unit(sensory.LiquidSedation),
                Extinguish = Unit(sensory.Fire)
            };
        }

        private float ResolveNeuralWalk()
        {
            if (escapeWalkSeconds > 0f) return escapeWalkDirection * .7f;
            if (locomotionMode > 0) return Math.Max(.3f, forwardFilter);
            if (locomotionMode < 0) return -Math.Max(.3f, backwardFilter);
            return 0f;
        }

        private readonly struct MotorCommandContext
        {
            public readonly float BodyThreat;
            public readonly float NeuralEscape;
            public readonly bool DNp01Activity;
            public readonly float Freeze;

            public MotorCommandContext(float bodyThreat, float neuralEscape, bool dnp01Activity, float freeze)
            {
                BodyThreat = bodyThreat;
                NeuralEscape = neuralEscape;
                DNp01Activity = dnp01Activity;
                Freeze = freeze;
            }
        }

        private static void ClearMotorRequests(ref MotorCommand command)
        {
            command.Walk = 0f;
            command.EscapeLocomotionSeconds = 0f;
            command.LeftArm = 0f;
            command.RightArm = 0f;
            command.LeftLeg = 0f;
            command.RightLeg = 0f;
            command.Core = 0f;
            command.Head = 0f;
            command.ReachGrab = 0f;
            command.LeftGrip = 0f;
            command.RightGrip = 0f;
        }

        private bool IsMovementPermitted(SensoryFrame sensory, float freeze)
        {
            return sensory.HealthValid && sensory.ConsciousnessValid && IsFinite(sensory.Consciousness) && Unit(sensory.Consciousness) > .8f && freeze < .5f;
        }

        private static float BodyThreatValue(SensoryFrame sensory)
        {
            return Unit(Unit(sensory.Pain) + Unit(sensory.Fire) + Unit(sensory.Shock) + Unit(sensory.SubmergedHypoxia) + Unit(sensory.Projectile));
        }

        private float DecodeNeuralEscape(float elapsedSeconds, bool rawSpike, bool evidence, bool permitted)
        {
            const float refractorySeconds = 1.5f;
            const float rearmQuietSeconds = .25f;
            if (!permitted)
            {
                neuralEscapeCooldown = neuralEscapeQuiet = escapeContextSeconds = escapeWalkSeconds = 0f;
                neuralEscapeArmed = true;
                return 0f;
            }
            // Use actual elapsed game time so a delayed callback cannot keep
            // a stale threat alive through the motor smoother's 0.25 s cap.
            var seconds = IsFinite(elapsedSeconds) && elapsedSeconds > 0f ? elapsedSeconds : DefaultStepSeconds;
            escapeContextSeconds = evidence ? .5f : Math.Max(0f, escapeContextSeconds - seconds);
            neuralEscapeCooldown = Math.Max(0f, neuralEscapeCooldown - seconds);
            if (rawSpike)
            {
                neuralEscapeQuiet = 0f;
                if (escapeContextSeconds > 0f && neuralEscapeArmed && neuralEscapeCooldown <= 0f)
                {
                    neuralEscapeArmed = false;
                    neuralEscapeCooldown = refractorySeconds;
                    return 1f;
                }

                return 0f;
            }

            neuralEscapeQuiet = Math.Min(rearmQuietSeconds, neuralEscapeQuiet + seconds);
            if (neuralEscapeCooldown <= 0f && neuralEscapeQuiet >= rearmQuietSeconds)
            {
                neuralEscapeArmed = true;
            }

            return 0f;
        }

        private bool FiredMotorPopulation(string population)
        {
            var ids = asset.Population(population);
            for (var i = 0; i < ids.Count; i++)
            {
                if (firedPresent[ids[i]] && IsMotorNeuron(ids[i])) return true;
            }

            return false;
        }

        private MotorCommand SmoothMotorRequest(MotorCommand requested, float elapsedSeconds)
        {
            var seconds = IsFinite(elapsedSeconds) && elapsedSeconds > 0f ? elapsedSeconds : DefaultStepSeconds;
            var maximumChange = MotorChangePerSecond * Clamp(seconds, 0f, .25f);
            requested.Walk = MoveTowards(lastCommand.Walk, requested.Walk, maximumChange);
            requested.LeftArm = MoveTowards(lastCommand.LeftArm, requested.LeftArm, maximumChange);
            requested.RightArm = MoveTowards(lastCommand.RightArm, requested.RightArm, maximumChange);
            requested.LeftLeg = MoveTowards(lastCommand.LeftLeg, requested.LeftLeg, maximumChange);
            requested.RightLeg = MoveTowards(lastCommand.RightLeg, requested.RightLeg, maximumChange);
            requested.Core = MoveTowards(lastCommand.Core, requested.Core, maximumChange);
            requested.Head = MoveTowards(lastCommand.Head, requested.Head, maximumChange);
            requested.ReachGrab = MoveTowards(lastCommand.ReachGrab, requested.ReachGrab, maximumChange);
            requested.LeftGrip = MoveTowards(lastCommand.LeftGrip, requested.LeftGrip, maximumChange);
            requested.RightGrip = MoveTowards(lastCommand.RightGrip, requested.RightGrip, maximumChange);
            return requested;
        }

        private static float MoveTowards(float current, float target, float maximumChange)
        {
            return current < target ? Math.Min(current + maximumChange, target) : Math.Max(current - maximumChange, target);
        }

        private static float ElapsedSeconds(float value) => IsFinite(value) && value > 0f ? Clamp(value, 0f, .25f) : DefaultStepSeconds;
        private static float Ema(float prior, float target, float elapsedSeconds, float tauSeconds)
        {
            var alpha = 1f - (float)Math.Exp(-ElapsedSeconds(elapsedSeconds) / tauSeconds);
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
            smallVisualDrive = small.Amplitude;
            approachDrive = Math.Max(expansion.Amplitude, looming.Amplitude);
            var effectiveExpansion = DriveChannel(manualInput, ManualInputChannel.ExpandingVisual, expansion.Amplitude, expansion.Direction);
            var effectiveLooming = DriveChannel(manualInput, ManualInputChannel.LoomingVisual, looming.Amplitude, looming.Direction);
            visualThreat = Math.Max(effectiveExpansion, effectiveLooming);
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
        private string LocomotionLabel()
        {
            if (locomotionMode > 0) return "FORWARD";
            if (locomotionMode < 0) return "BACKWARD";
            return "IDLE";
        }

        private string RequestLabel()
        {
            if (escapeWalkSeconds > 0f) return "ESCAPE BURST";
            return "WALK " + LocomotionLabel();
        }

        private string ResponseLabel()
        {
            if (lastCommand.NeuralEscape > 0f) return "neural escape";
            if (lastCommand.Avoid > 0f) return "body avoidance";
            return "none";
        }
    }
}
