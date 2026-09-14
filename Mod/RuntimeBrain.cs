using System;
using System.Collections.Generic;
using System.Globalization;

#pragma warning disable CA2249 // IndexOf keeps this shared source compatible with the net48 game target.

namespace Mod
{
    internal interface IRuntimeConnectomeAsset
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

    // The controller brain is kept in one source file and compiled into both the
    // game script assembly and the game-independent source project. RuntimeAsset
    // remains game-specific; the interface above is the only boundary.
    internal sealed partial class ConnectomeBrain
    {
        private readonly IRuntimeConnectomeAsset asset;
        private readonly float[] potential;
        private readonly long[] refractoryUntil;
        private Dictionary<int, float> pending = [];
        private HashSet<int> active = [];
        private readonly HashSet<int> priority = [];
        private readonly HashSet<int> scheduled = [];
        private readonly List<int> fired = [];
        private readonly HashSet<int> firedIds = [];
        private Dictionary<int, float> nextPending = [];
        private HashSet<int> nextActive = [];
        private readonly List<int> orderedActive = [];
        private MotorCommand lastCommand;
        private bool stopped;
        private long simulationTick;
        private long backlogCursor;
        private int processedThisStep;
        private int droppedThisStep;
        private int decayedThisStep;
        private float lightDrive, audioDrive, touchDrive, damageDrive, regionalTouchDrive, smallVisualDrive, opticRollDrive, gravityDrive, jointDrive, hotDrive, coldDrive, approachDrive;
        private float bodyThreat, visualThreat, neuralEscape;
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

        private ConnectomeBrain(IRuntimeConnectomeAsset asset)
        {
            this.asset = asset ?? throw new ArgumentNullException(nameof(asset));
            potential = new float[asset.NeuronCount];
            refractoryUntil = new long[asset.NeuronCount];
        }

        public string Status => "MaleCNS v1.0 " + asset.NeuronCount + " neurons / " + asset.EdgeCount +
            (stopped ? " STOPPED" : " queued=" + pending.Count + " input-integrated=" + processedThisStep +
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
                return "NEURAL:\n  input=" + Format(LastSensoryDrive) + "  queued=" + pending.Count + "  active=" + active.Count +
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
            "REQUEST (" + (escapeWalkSeconds > 0f ? "ESCAPE BURST" : "WALK " + (locomotionMode > 0 ? "FORWARD" : locomotionMode < 0 ? "BACKWARD" : "IDLE")) + "):\n  arms=" + Format(lastCommand.LeftArm) + "/" + Format(lastCommand.RightArm) +
            "  legs=" + Format(lastCommand.LeftLeg) + "/" + Format(lastCommand.RightLeg) +
            "\n  head=" + Format(lastCommand.Head) + "  core=" + Format(lastCommand.Core) +
            "  grips=" + Format(lastCommand.LeftGrip) + "/" + Format(lastCommand.RightGrip) +
            "\n  escape-burst-remaining=" + Format(lastCommand.EscapeLocomotionSeconds) + " s (native walking adaptation; shooter direction unknown)" +
            "\nTHREAT SOURCES\n  body=" + Format(lastCommand.BodyThreat) + "  injury-event=" + lastCommand.InjuryEvent.ToString("0.0000", CultureInfo.InvariantCulture) + "  looming=" + Format(lastCommand.VisualThreat) +
            "  DNp01-fired=" + Format(lastCommand.DNp01Activity) + "  escape-request=" + Format(lastCommand.NeuralEscape) + "  recent-threat-window=" + Format(escapeContextSeconds) + " s  response=" + (lastCommand.NeuralEscape > 0f ? "neural escape" : lastCommand.Avoid > 0f ? "body avoidance" : "none") +
            "\nFILTERED NEURAL READOUT (fractions):\n  forward=" + Format(forwardFilter) + "  backward=" + Format(backwardFilter) + "  turn(R-L)=" + Format(yawFilter);

        public long SimulationTick => simulationTick;
        public int FiredCount => fired.Count;
        public int ProcessedCount => processedThisStep;
        public int DroppedCount => droppedThisStep;
        public int DecayedCount => decayedThisStep;
        public int PendingCount => pending.Count;
        public int ActiveCount => active.Count;
        public bool IsStopped => stopped;
        public MotorCommand LastCommand => lastCommand;

        public bool DidFire(int neuronId) => neuronId >= 0 && neuronId < potential.Length && firedIds.Contains(neuronId);

        public int PopulationCount(string name) => string.IsNullOrEmpty(name) ? 0 : asset.Population(name).Count;

        public int PopulationFiredCount(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            var ids = asset.Population(name);
            var count = 0;
            for (var i = 0; i < ids.Count; i++) if (firedIds.Contains(ids[i])) count++;
            return count;
        }

        public MotorCommand Step(SensoryFrame sensory) => Step(sensory, DefaultStepSeconds, null);

        public MotorCommand Step(SensoryFrame sensory, float elapsedSeconds) => Step(sensory, elapsedSeconds, null);

        public MotorCommand Step(SensoryFrame sensory, float elapsedSeconds, ManualInputState manualInput)
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
            bodyThreat = visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
            sensoryQueued = 0;
            fired.Clear();
            firedIds.Clear();
            nextPending.Clear();
            nextActive.Clear();
            priority.Clear();
            DriveSensoryPopulations(sensory, manualInput);

            ProcessActiveNeurons(nextPending, nextActive);
            // A target can fire after an earlier source queued it this tick.
            // Clear only those fired IDs, rather than scanning the whole queue.
            foreach (var id in fired)
            {
                nextPending.Remove(id);
                nextActive.Remove(id);
            }
            SwapPendingState();
            priority.Clear();
            manualInput?.FinishTick();

            return BuildMotorCommand(sensory, elapsedSeconds);
        }

        public MotorCommand Stop()
        {
            if (!stopped)
            {
                Array.Clear(potential, 0, potential.Length);
                Array.Clear(refractoryUntil, 0, refractoryUntil.Length);
                pending.Clear();
                active.Clear();
                priority.Clear();
                scheduled.Clear();
                fired.Clear();
                firedIds.Clear();
                nextPending.Clear();
                nextActive.Clear();
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
            bodyThreat = visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
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
            fired.Clear();
            firedIds.Clear();
            LastSensoryDrive = 0f;
            sensoryQueued = 0;
            hasPreviousLight = false;
            previousLight = lightOnDrive = lightOffDrive = 0f;
            lightDrive = audioDrive = touchDrive = damageDrive = regionalTouchDrive = smallVisualDrive = opticRollDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            bodyThreat = visualThreat = neuralEscape = foodNearbyDrive = foodContactDrive = 0f;
            neuralEscapeCooldown = neuralEscapeQuiet = escapeContextSeconds = escapeWalkSeconds = 0f;
            neuralEscapeArmed = true;
            lastCommand = new MotorCommand();
            forwardFilter = backwardFilter = yawFilter = leftLegFilter = rightLegFilter = locomotionDwell = 0f;
            locomotionMode = 0;
            return lastCommand;
        }

        private void ProcessActiveNeurons(Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            // Integrate once per tick before propagating spikes. Subthreshold
            // inputs and residual decay must not compete with outgoing-edge work.
            // The active set is bounded by the loaded graph's neuron count.
            orderedActive.Clear();
            foreach (var id in active)
            {
                if (simulationTick < refractoryUntil[id]) { potential[id] = 0f; continue; }
                var input = pending.TryGetValue(id, out var queued) ? queued : 0f;
                var updated = Clamp(potential[id] * .92f + Clamp(input, -4f, 4f), -8f, 8f);
                potential[id] = updated;
                if (input == 0f) decayedThisStep++;
                else processedThisStep++;
                if (updated >= 1f) orderedActive.Add(id);
                else if (Math.Abs(updated) > .001f) nextActiveState.Add(id);
            }
            if (orderedActive.Count > MaxSpikesPerStep)
            {
                ProcessFairSpikeBudget(next, nextActiveState);
                return;
            }

            foreach (var id in orderedActive) PropagateSpike(id, next, nextActiveState);
        }

        private void ProcessFairSpikeBudget(Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            // Only threshold crossings need sorting and fair propagation selection.
            orderedActive.Sort();
            var start = (int)(backlogCursor % orderedActive.Count);
            scheduled.Clear();
            foreach (var id in orderedActive) scheduled.Add(id);
            var budget = MaxSpikesPerStep;
            foreach (var id in priority)
            {
                if (budget == 0) break;
                if (!scheduled.Remove(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }

            var examined = 0;
            for (var index = 0; index < orderedActive.Count && budget > 0; index++)
            {
                var id = orderedActive[(start + index) % orderedActive.Count];
                examined++;
                if (!scheduled.Remove(id)) continue;
                PropagateSpike(id, next, nextActiveState);
                budget--;
            }

            foreach (var id in scheduled)
            {
                if (simulationTick < refractoryUntil[id]) potential[id] = 0f;
                else DropNeuron(id);
            }

            backlogCursor = (start + examined) % orderedActive.Count;
        }

        private void PropagateSpike(int id, Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            potential[id] = 0f;
            refractoryUntil[id] = simulationTick + RefractoryTicks + 1;
            fired.Add(id);
            firedIds.Add(id);
            QueueOutgoing(id, next, nextActiveState);
        }

        private void DropNeuron(int id)
        {
            droppedThisStep++;
            potential[id] = 0f;
        }

        private void QueueOutgoing(int source, Dictionary<int, float> next, HashSet<int> nextActiveState)
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
                var exists = next.TryGetValue(target, out var queued);
                next[target] = queued + asset.WeightAt(edge) * sign;
                // Existing pending entries already have an active target.
                if (!exists) nextActiveState.Add(target);
            }
        }

        private void SwapPendingState()
        {
            var previousPending = pending;
            pending = nextPending;
            nextPending = previousPending;
            nextPending.Clear();

            var previousActive = active;
            active = nextActive;
            nextActive = previousActive;
            nextActive.Clear();
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
            bodyThreat = bodyThreatValue;
            neuralEscape = neuralEscapeValue;
            var danger = bodyThreatValue > .5f || neuralEscapeValue > 0f;
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
            // A qualified DNp01 event previously only modified an existing walk:
            // idle remained idle. Translate that neural event into a short native
            // walking burst. Preserve neural backward intent; otherwise use native
            // forward. No shooter localization, jump force or sensor-only reflex.
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
            }
            else
            {
                forwardFilter = Ema(forwardFilter, rawForward, elapsedSeconds, .15f);
                backwardFilter = Ema(backwardFilter, rawBackward, elapsedSeconds, .15f);
                yawFilter = Ema(yawFilter, rawYaw, elapsedSeconds, .1f);
                locomotionDwell += ElapsedSeconds(elapsedSeconds);
                if (rawBackward >= .5f)
                {
                    if (locomotionMode != -1) { locomotionMode = -1; locomotionDwell = 0f; }
                }
                else if (locomotionMode == 0 && forwardFilter >= .08f)
                {
                    locomotionMode = 1; locomotionDwell = 0f;
                }
                else if (locomotionDwell >= .25f &&
                    (locomotionMode > 0 ? forwardFilter < .04f : backwardFilter < .25f))
                {
                    locomotionMode = 0;
                    locomotionDwell = 0f;
                }
            }
            var locomotionGate = stopRequested ? 0f : 1f;
            var neuralWalk = locomotionMode > 0 ? Math.Max(.3f, forwardFilter) : locomotionMode < 0 ? -Math.Max(.3f, backwardFilter) : 0f;
            if (escapeWalkSeconds > 0f) neuralWalk = escapeWalkDirection * .7f;
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
            // MN9 extends a fly proboscis; there is no validated human-grasp mapping.
            var reach = 0f;
            var requested = new MotorCommand
            {
                Walk = walk,
                EscapeLocomotionSeconds = escapeWalkSeconds,
                LeftArm = Signed(-armSwing),
                RightArm = armSwing,
                LeftLeg = Signed(walk - motorSideBias * .2f),
                RightLeg = Signed(walk + motorSideBias * .2f),
                Core = Signed(walk * .6f + motorCenter * .15f + yawFilter * .25f),
                Head = Signed(motorSideBias * .5f + yawFilter),
                ReachGrab = reach,
                LeftGrip = reach,
                RightGrip = reach,
                Avoid = danger ? 1f : 0f,
                BodyThreat = bodyThreatValue,
                InjuryEvent = Unit(sensory.DamageEvent),
                VisualThreat = visualThreat,
                NeuralEscape = neuralEscapeValue,
                DNp01Activity = dnp01Activity ? 1f : 0f,
                Freeze = freeze,
                Heal = Unit(Unit(sensory.Damage) + Unit(sensory.Bleeding) + Unit(sensory.LiquidHealing)),
                // Native stress values are inputs, not automatic instructions
                // to amplify adrenaline or chemically calm an injured person.
                Stimulate = Unit(sensory.LiquidStimulation),
                Calm = Unit(sensory.LiquidSedation),
                Extinguish = Unit(sensory.Fire)
            };
            if (!movementPermitted || locomotionGate == 0f)
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
                if (firedIds.Contains(ids[i]) && IsMotorNeuron(ids[i])) return true;
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

        private float PopulationActivity(string population, string side = null, bool motorOnly = false)
        {
            var ids = asset.Population(population);
            var count = 0;
            var firing = 0;
            foreach (var id in ids)
            {
                if (side != null && asset.SideAt(id) != side) continue;
                if (motorOnly && !IsMotorNeuron(id)) continue;
                count++;
                if (firedIds.Contains(id)) firing++;
            }
            return count == 0 ? 0f : (float)firing / count;
        }

        private float MotorActivity(string population, string side = null) => PopulationActivity(population, side, true);

        private bool IsMotorNeuron(int id)
        {
            var superclass = asset.SuperclassAt(id);
            return superclass == "descending_neuron" || superclass == "vnc_motor" || superclass == "cb_motor";
        }

        private void DriveSensoryPopulations(SensoryFrame sensory, ManualInputState manualInput)
        {
            // Population choices follow the upstream sensory encoder and adult-fly
            // annotations. Our discrete amplitude model is not its Poisson/Hz model.
            // Internal blood chemistry, pain and disease do not imply external taste,
            // odor, light or an identified nociceptor signal.
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
            // The reference turns an injury event into a saturating bristle rate.
            // Our one-spike-per-tick model uses twice its normalized Hill response:
            // a 20% health-loss event reaches threshold; smaller events remain
            // graded input. This is an engineered event encoder, not felt pain.
            var damage = Unit(sensory.DamageEvent);
            var damagePower = (float)Math.Pow(damage / .2f, 1.5);
            damageDrive = Unit(2f * damagePower / (1f + damagePower));
            regionalTouchDrive = sensory.RegionalTouchValid ? Math.Max(Math.Max(Unit(sensory.TouchHead), Unit(sensory.TouchArms)), Math.Max(Unit(sensory.TouchLegs), Unit(sensory.TouchCore))) * .15f : 0f;
            var effectiveTotal = 0f;
            // Broadband light only: these inputs do not claim color/UV sensing or
            // a per-column retina. No body-state stimulus is painted onto the eyes.
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.Light, lightDrive, 0f);
            // Reference ON/OFF entries adapted to measured global luminance
            // changes only. No per-column image or invented dark tonic current.
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.LightOn, lightOnDrive, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.LightOff, lightOffDrive, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.Auditory, audioDrive,
                sensory.SoundDirectionValid ? Signed(sensory.SoundDirection) : 0f);
            // Regions partition tactile neurons. Merge broad impact, measured
            // injury and local contact by maximum so one cell is not injected twice.
            var broadTouch = Math.Max(touchDrive, damageDrive);
            if (asset.Population("input:touch-head").Count == 0 && asset.Population("input:touch-arms").Count == 0)
            {
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchOther, broadTouch, 0f);
            }
            else
            {
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchHead, Math.Max(broadTouch, sensory.RegionalTouchValid ? Unit(sensory.TouchHead) * .15f : 0f), 0f);
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchArms, Math.Max(broadTouch, sensory.RegionalTouchValid ? Unit(sensory.TouchArms) * .15f : 0f), 0f);
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchLegs, Math.Max(broadTouch, sensory.RegionalTouchValid ? Unit(sensory.TouchLegs) * .15f : 0f), 0f);
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchCore, Math.Max(broadTouch, sensory.RegionalTouchValid ? Unit(sensory.TouchCore) * .15f : 0f), 0f);
                effectiveTotal += DriveChannel(manualInput, ManualInputChannel.TouchOther, broadTouch, 0f);
            }
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.Gravity, gravityDrive, sensory.TiltValid ? Signed(sensory.SignedTilt) : 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.JointPosition, jointPosition, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.JointMotion, jointMotion, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.JointLoad, jointLoad, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.Warm, hotDrive, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.Cool, coldDrive, 0f);
            foodNearbyDrive = sensory.FoodCuesValid ? Unit(sensory.FoodNearbyCue) : 0f;
            foodContactDrive = sensory.FoodCuesValid ? Unit(sensory.FoodContactCue) : 0f;
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.FoodNearby, foodNearbyDrive, 0f);
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.FoodContact, foodContactDrive, 0f);
            // Object geometry is only used when the adapter measured it. These
            // are feature encoders, not semantic object recognition.
            var expansionInput = new VisualFeatureInput();
            var loomingInput = new VisualFeatureInput();
            var smallInput = new VisualFeatureInput();
            if (sensory.VisualFieldValid)
            {
                for (var band = 0; band < 5; band++)
                {
                    var view = sensory.ViewAt(band);
                    if (!view.Observed || Unit(view.Strength) <= 0f || !IsFinite(view.BearingDegrees) || Math.Abs(view.BearingDegrees) > 90f) continue;
                    AddVisualFeatures(view, Signed(view.BearingDegrees / 90f), ref expansionInput, ref loomingInput, ref smallInput);
                }
            }
            else
            {
                // Single-feature frames remain available to the test/manual
                // interface. Native adapters supply the spatial field above.
                var direction = sensory.VisionHeadBearingValid ? Signed(sensory.VisionHeadBearingDegrees / 90f) : 0f;
                AddVisualFeatures(new VisualObservation
                {
                    Strength = sensory.Vision,
                    Approach = sensory.VisualApproach,
                    GeometryValid = sensory.VisualGeometryValid,
                    AngularSize = sensory.VisualAngularSize,
                    Expansion = sensory.VisualExpansion,
                    AngularSpeed = sensory.VisualAngularSpeed
                }, direction, ref expansionInput, ref loomingInput, ref smallInput);
            }
            smallVisualDrive = smallInput.Amplitude;
            approachDrive = Math.Max(expansionInput.Amplitude, loomingInput.Amplitude);
            var effectiveExpansion = DriveChannel(manualInput, ManualInputChannel.ExpandingVisual, expansionInput.Amplitude, expansionInput.Direction);
            var effectiveLooming = DriveChannel(manualInput, ManualInputChannel.LoomingVisual, loomingInput.Amplitude, loomingInput.Direction);
            visualThreat = Math.Max(effectiveExpansion, effectiveLooming);
            effectiveTotal += effectiveExpansion + effectiveLooming;
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.SmallMovingVisual, smallInput.Amplitude, smallInput.Direction);
            if (lightValid && lightDrive > 0f && sensory.AngularVelocityValid && IsFinite(sensory.AngularVelocity))
            {
                var roll = Math.Abs(sensory.AngularVelocity) / (Math.Abs(sensory.AngularVelocity) + 300f) * lightDrive;
                opticRollDrive = roll;
            }
            effectiveTotal += DriveChannel(manualInput, ManualInputChannel.OpticRoll, opticRollDrive, 0f);
            LastSensoryDrive = Unit(effectiveTotal);
        }

        private struct VisualFeatureInput
        {
            private float left, right;
            public float Amplitude => Math.Max(left, right);
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

        private float DriveChannel(ManualInputState manualInput, ManualInputChannel channel, float live, float direction)
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

            var fallbackAvailable = !primaryAvailable && !string.IsNullOrEmpty(descriptor.FallbackTarget) && asset.Population(descriptor.FallbackTarget).Count > 0;
            var effective = manualInput == null ? Unit(live) : manualInput.Resolve(channel, live, direction, primaryAvailable || fallbackAvailable);
            var effectiveDirection = manualInput == null ? direction : manualInput.GetReading(channel).EffectiveDirection;
            if (effective <= .001f || (!primaryAvailable && !fallbackAvailable)) return effective;

            if (primaryAvailable)
            {
                for (var i = 0; i < descriptor.Targets.Length; i++) Drive(descriptor.Targets[i], effective, effectiveDirection);
            }
            else
            {
                Drive(descriptor.FallbackTarget, effective, effectiveDirection);
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
                var gain = side == "L" ? 1f - Math.Max(0f, direction) : side == "R" ? 1f + Math.Min(0f, direction) : 1f;
                var input = value * gain;
                if (input <= .001f) continue;
                var queued = pending.TryGetValue(id, out var valueAtId) ? valueAtId : 0f;
                pending[id] = queued + input;
                active.Add(id);
                if (priority.Add(id)) sensoryQueued++;
            }
        }

        private static float Unit(float value) => IsFinite(value) ? Clamp(value, 0f, 1f) : 0f;
        private static float Signed(float value) => IsFinite(value) ? Clamp(value, -1f, 1f) : 0f;
        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

#pragma warning restore CA2249
