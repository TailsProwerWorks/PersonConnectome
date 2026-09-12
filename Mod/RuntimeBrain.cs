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
        private float lightDrive, audioDrive, touchDrive, gravityDrive, jointDrive, hotDrive, coldDrive, approachDrive;
        private int sensoryQueued;
        private bool hasPreviousLight;
        private float previousLight, lightOnDrive, lightOffDrive;
        private const int MaxActivePerStep = 24000;
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
            (stopped ? " STOPPED" : " queued=" + pending.Count + " processed=" + processedThisStep +
            " dropped=" + droppedThisStep + " fired=" + fired.Count + " input=" + Format(LastSensoryDrive) +
            " request-walk=" + Format(lastCommand.Walk));

        public string DisplaySummary
        {
            get
            {
                if (stopped)
                {
                    return "NEURAL: STOPPED\n  queued=0  processed=0  dropped=0  fired=0";
                }

                var scheduler = droppedThisStep > 0 ? "OVERLOADED" : "STEADY";
                return "NEURAL:\n  input=" + Format(LastSensoryDrive) + "  queued=" + pending.Count + "  active=" + active.Count +
                    "\n  processed=" + processedThisStep + "/" + MaxActivePerStep + "  dropped=" + droppedThisStep +
                    "\n  fired=" + fired.Count + "  scheduler=" + scheduler +
                    (droppedThisStep > 0 ? "\nWork limit reached; this is not a CPU-time measurement." : "");
            }
        }

        public string DisplayInputSummary => stopped ? "INPUT: STOPPED" :
            "ENCODER REQUESTS (normalized amplitudes, not Hz):\n  light=" + Format(lightDrive) + "  audio=" + Format(audioDrive) +
            "  touch=" + Format(touchDrive) + "\n  gravity-proxy=" + Format(gravityDrive) + "  joints=" + Format(jointDrive) +
            "\n  global light-change ON=" + Format(lightOnDrive) + "  OFF=" + Format(lightOffDrive) +
            "\n  warm=" + Format(hotDrive) + "  cool=" + Format(coldDrive) + "  approach-proxy=" + Format(approachDrive) +
            "\n  input neurons queued this tick=" + sensoryQueued +
            "\nBody health/chemistry are telemetry and control constraints; no validated receptor mapping.\nDirections use world horizontal as an engineering L/R projection.";

        public string DisplayMotorSummary => stopped ?
            "REQUEST: STOPPED\n  arms=0.00/0.00  legs=0.00/0.00\n  head=0.00  core=0.00  grips=0.00/0.00" :
            "REQUEST:\n  arms=" + Format(lastCommand.LeftArm) + "/" + Format(lastCommand.RightArm) +
            "  legs=" + Format(lastCommand.LeftLeg) + "/" + Format(lastCommand.RightLeg) +
            "\n  head=" + Format(lastCommand.Head) + "  core=" + Format(lastCommand.Core) +
            "  grips=" + Format(lastCommand.LeftGrip) + "/" + Format(lastCommand.RightGrip);

        public long SimulationTick => simulationTick;
        public int FiredCount => fired.Count;
        public int ProcessedCount => processedThisStep;
        public int DroppedCount => droppedThisStep;
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

        public MotorCommand Step(SensoryFrame sensory) => Step(sensory, DefaultStepSeconds);

        public MotorCommand Step(SensoryFrame sensory, float elapsedSeconds)
        {
            if (sensory.BrainDead || !sensory.Alive)
            {
                return Stop();
            }

            if (stopped)
            {
                stopped = false;
            }

            simulationTick++;
            processedThisStep = 0;
            droppedThisStep = 0;
            lightDrive = audioDrive = touchDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            sensoryQueued = 0;
            fired.Clear();
            firedIds.Clear();
            nextPending.Clear();
            nextActive.Clear();
            priority.Clear();
            DriveSensoryPopulations(sensory);

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

            return BuildMotorCommand(sensory, elapsedSeconds);
        }

        private MotorCommand Stop()
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
            lightDrive = audioDrive = touchDrive = gravityDrive = jointDrive = hotDrive = coldDrive = approachDrive = 0f;
            sensoryQueued = 0;
            lastCommand = new MotorCommand();
            return lastCommand;
        }

        private void ProcessActiveNeurons(Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            if (active.Count > MaxActivePerStep)
            {
                ProcessFairBacklog(next, nextActiveState);
                return;
            }

            foreach (var id in active)
            {
                ProcessActiveNeuron(id, next, nextActiveState);
            }
        }

        private void ProcessFairBacklog(Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            orderedActive.Clear();
            foreach (var id in active) orderedActive.Add(id);
            orderedActive.Sort();
            var start = (int)(backlogCursor % orderedActive.Count);
            scheduled.Clear();
            var budget = MaxActivePerStep;
            foreach (var id in priority)
            {
                if (budget == 0) break;
                if (ProcessActiveNeuron(id, next, nextActiveState)) budget--;
                scheduled.Add(id);
            }

            var examined = 0;
            for (var index = 0; index < orderedActive.Count && budget > 0; index++)
            {
                var id = orderedActive[(start + index) % orderedActive.Count];
                examined++;
                if (priority.Contains(id)) continue;
                if (ProcessActiveNeuron(id, next, nextActiveState)) budget--;
                scheduled.Add(id);
            }

            foreach (var id in orderedActive)
            {
                if (!scheduled.Contains(id))
                {
                    if (simulationTick < refractoryUntil[id]) potential[id] = 0f;
                    else DropNeuron(id);
                }
            }

            backlogCursor = (start + examined) % orderedActive.Count;
        }

        private bool ProcessActiveNeuron(int id, Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            if (simulationTick < refractoryUntil[id])
            {
                potential[id] = 0f;
                return false;
            }

            processedThisStep++;
            ProcessNeuron(id, next, nextActiveState);
            return true;
        }

        private void DropNeuron(int id)
        {
            droppedThisStep++;
            potential[id] = 0f;
        }

        private void ProcessNeuron(int id, Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            var input = pending.TryGetValue(id, out var queued) ? queued : 0f;
            var updatedPotential = Clamp(potential[id] * .92f + Clamp(input, -4f, 4f), -8f, 8f);
            if (updatedPotential < 1f)
            {
                potential[id] = updatedPotential;
                if (Math.Abs(updatedPotential) > .001f) nextActiveState.Add(id);
                return;
            }

            potential[id] = 0f;
            refractoryUntil[id] = simulationTick + RefractoryTicks + 1;
            fired.Add(id);
            firedIds.Add(id);
            QueueOutgoing(id, next, nextActiveState);
        }

        private void QueueOutgoing(int source, Dictionary<int, float> next, HashSet<int> nextActiveState)
        {
            var start = asset.OutgoingStart(source);
            var end = asset.OutgoingEnd(source);
            for (var edge = start; edge < end; edge++)
            {
                var target = asset.TargetAt(edge);
                // Propagated input arrives on the next tick. Allow input on
                // the exact recovery tick; earlier input cannot be integrated.
                if (simulationTick + 1 < refractoryUntil[target]) continue;
                MergePending(next, target, asset.WeightAt(edge) * asset.SignAt(source));
                nextActiveState.Add(target);
            }
        }

        private static void MergePending(Dictionary<int, float> destination, int id, float amount)
        {
            var queued = destination.TryGetValue(id, out var value) ? value : 0f;
            destination[id] = Clamp(queued + amount, -4f, 4f);
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
            var danger = IsDangerous(sensory);
            // Reference MotorMap walking/halting populations. Fractions below are
            // latest-tick activity, not the upstream biological firing-rate decoder.
            var forward = MotorActivity("type:DNp09") * .3f + MotorActivity("type:DNg100") * .25f +
                MotorActivity("type:DNge053") * .15f + MotorActivity("type:DNge050") * .15f + MotorActivity("type:DNg97") * .15f;
            var backward = MotorActivity("type:MDN");
            var halt = Math.Max(MotorActivity("type:DNg60"), Math.Max(MotorActivity("type:DNg74_a"), MotorActivity("type:DNg74_b")));
            var brake = PopulationActivity("type:AN19A018");
            var locomotionGate = halt >= .2f || brake >= .2f ? 0f : 1f;
            var neuralWalk = (backward >= .2f ? -backward : forward) * locomotionGate;
            // Fly leg activity is a human joint-control proxy. Wing, song and
            // proboscis populations are not reinterpreted as human arm/grip intent.
            var left = MotorActivity("motor:leg", "L") * locomotionGate;
            var right = MotorActivity("motor:leg", "R") * locomotionGate;
            var center = (left + right) * .5f;
            var sideBias = right - left;
            var freeze = Unit(sensory.Unconscious + sensory.LiquidSedation);
            var movementPermitted = IsMovementPermitted(sensory, freeze);
            var walk = movementPermitted ? neuralWalk : 0f;
            var motorSideBias = movementPermitted ? sideBias : 0f;
            var motorCenter = movementPermitted ? center : 0f;
            var armSwing = Signed((left - right) * .75f * (movementPermitted ? 1f : 0f) + walk * .35f + motorCenter * .15f);
            // MN9 extends a fly proboscis; there is no validated human-grasp mapping.
            var reach = 0f;
            var requested = new MotorCommand
            {
                Walk = walk,
                LeftArm = Signed(-armSwing),
                RightArm = armSwing,
                LeftLeg = Signed(walk - motorSideBias * .2f),
                RightLeg = Signed(walk + motorSideBias * .2f),
                Core = Signed(walk * .6f + motorCenter * .15f),
                Head = Signed(motorSideBias * .5f),
                ReachGrab = reach,
                LeftGrip = reach,
                RightGrip = reach,
                Avoid = danger ? 1f : 0f,
                Freeze = freeze,
                Heal = Unit(sensory.Damage + sensory.Bleeding + sensory.LiquidHealing),
                // Native stress values are inputs, not automatic instructions
                // to amplify adrenaline or chemically calm an injured person.
                Stimulate = Unit(sensory.LiquidStimulation),
                Calm = Unit(sensory.LiquidSedation),
                Extinguish = Unit(sensory.Fire)
            };
            if (!movementPermitted || locomotionGate == 0f)
            {
                ClearMotorRequests(ref requested);
                lastCommand = requested;
                return lastCommand;
            }

            lastCommand = SmoothMotorRequest(requested, elapsedSeconds);
            return lastCommand;
        }

        private static void ClearMotorRequests(ref MotorCommand command)
        {
            command.Walk = 0f;
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

        private bool IsDangerous(SensoryFrame sensory)
        {
            return sensory.Pain + sensory.Fire + sensory.Shock + sensory.SubmergedHypoxia + sensory.Projectile > .5f || FiredMotorPopulation("type:DNp01");
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

        private void DriveSensoryPopulations(SensoryFrame sensory)
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
            audioDrive = Unit(sensory.Sound);
            touchDrive = Math.Max(Unit(sensory.Impact + sensory.Vibration * .35f), Unit(sensory.Touch * .15f + sensory.PhysicalContact * .15f));
            gravityDrive = sensory.TiltValid ? Math.Abs(Signed(sensory.SignedTilt)) : 0f;
            var jointPosition = sensory.JointSensingValid ? Unit(sensory.JointPosition) : 0f;
            var jointMotion = sensory.JointSensingValid ? Unit(sensory.JointMotion) : 0f;
            var jointLoad = sensory.JointSensingValid ? Unit(sensory.NeuralJointLoad) : 0f;
            jointDrive = Math.Max(jointPosition, Math.Max(jointMotion, jointLoad));
            hotDrive = Math.Max(Unit(sensory.Heat), Unit(sensory.AmbientHeat));
            coldDrive = Math.Max(Unit(sensory.Cold), Unit(sensory.AmbientCold));
            approachDrive = Unit(sensory.VisualApproach);
            LastSensoryDrive = Unit(lightDrive + lightOnDrive + lightOffDrive + audioDrive + touchDrive + gravityDrive + jointDrive + hotDrive + coldDrive + approachDrive);

            // Broadband light only: these inputs do not claim color/UV sensing or
            // a per-column retina. No body-state stimulus is painted onto the eyes.
            Drive("input:light", lightDrive);
            // Reference ON/OFF entries adapted to measured global luminance
            // changes only. No per-column image or invented dark tonic current.
            Drive("type:Mi1", lightOnDrive);
            Drive("type:L2", lightOffDrive);
            Drive("type:L3", lightOffDrive);
            Drive("input:auditory", audioDrive, sensory.SoundDirectionValid ? Signed(sensory.SoundDirection) : 0f);
            Drive("input:tactile", touchDrive);
            Drive("input:gravity", gravityDrive, sensory.TiltValid ? Signed(sensory.SignedTilt) : 0f);
            Drive("input:joint-position", jointPosition);
            Drive("input:joint-motion", jointMotion);
            Drive("input:joint-load", jointLoad);
            Drive("input:hot", hotDrive);
            Drive("input:cold", coldDrive);
            // As in the reference project, visual feature drive is hand-built.
            // Here it means visible relative approach, not true angular expansion.
            var visualDirection = sensory.VisionDirectionValid ? Signed(sensory.VisionDirection) : 0f;
            Drive("type:LC4", approachDrive, visualDirection);
            Drive("type:LPLC2", approachDrive, visualDirection);
        }

        private float LastSensoryDrive { get; set; }

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
                pending[id] = Clamp(queued + input, -4f, 4f);
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
