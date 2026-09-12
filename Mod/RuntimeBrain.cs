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
        private float injuryDrive, hazardDrive, motionDrive, arousalDrive;
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
            "INPUT:\n  injury=" + Format(injuryDrive) + "  hazard=" + Format(hazardDrive) + "  motion=" + Format(motionDrive) +
            "\n  arousal=" + Format(arousalDrive) + "  total=" + Format(LastSensoryDrive);

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
            injuryDrive = 0f;
            hazardDrive = 0f;
            motionDrive = 0f;
            arousalDrive = 0f;
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
            processedThisStep = 0;
            droppedThisStep = 0;
            injuryDrive = hazardDrive = motionDrive = arousalDrive = 0f;
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
            var left = FiredOnSide("L");
            var right = FiredOnSide("R");
            var center = FiredOnSide("M");
            var sideBias = right - left;
            var hasMotorActivity = left > 0f || right > 0f || center > 0f;
            var neuralWalk = hasMotorActivity ? Clamp(sideBias + (FiredMotorPopulation("type:DNp09") ? .35f : 0f) - (FiredMotorPopulation("type:MDN") ? .5f : 0f), -1f, 1f) : 0f;
            var freeze = Unit(sensory.Unconscious + sensory.LiquidSedation);
            var movementPermitted = IsMovementPermitted(sensory, freeze);
            var walk = movementPermitted ? neuralWalk : 0f;
            var motorSideBias = movementPermitted ? sideBias : 0f;
            var motorCenter = movementPermitted ? center : 0f;
            var armSwing = Signed((left - right) * .75f * (movementPermitted ? 1f : 0f) + walk * .35f + motorCenter * .15f);
            var reach = movementPermitted && FiredMotorPopulation("type:MN9") && !danger ? Unit(sensory.Nearby) : 0f;
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
            if (!movementPermitted)
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
            return sensory.Pain + sensory.Fire + sensory.Shock + sensory.SubmergedHypoxia + sensory.Projectile > .5f || Fired("type:DNp01");
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

        private bool Fired(string population)
        {
            var ids = asset.Population(population);
            for (var i = 0; i < ids.Count; i++) if (firedIds.Contains(ids[i])) return true;
            return false;
        }

        private float FiredOnSide(string side)
        {
            var total = 0;
            var count = 0;
            for (var i = 0; i < fired.Count; i++)
            {
                var id = fired[i];
                if (!IsMotorNeuron(id)) continue;
                count++;
                if (asset.SideAt(id).IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0) total++;
            }

            return count == 0 ? 0f : Unit((float)total / count);
        }

        private bool IsMotorNeuron(int id)
        {
            var superclass = asset.SuperclassAt(id);
            return superclass.IndexOf("descending", StringComparison.OrdinalIgnoreCase) >= 0 || superclass.IndexOf("motor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DriveSensoryPopulations(SensoryFrame sensory)
        {
            var healthDeficit = sensory.HealthValid && IsFinite(sensory.Health) ? 1f - Unit(sensory.Health) : 0f;
            var oxygenDeficit = sensory.OxygenValid && IsFinite(sensory.Oxygen) ? 1f - Unit(sensory.Oxygen) : 0f;
            var consciousnessDeficit = sensory.ConsciousnessValid && IsFinite(sensory.Consciousness) ? 1f - Unit(sensory.Consciousness) : 0f;
            var vitalityDeficit = sensory.VitalityValid && IsFinite(sensory.Vitality) ? 1f - Unit(sensory.Vitality) : 0f;
            var injury = Unit(sensory.Pain + (sensory.DamageValid ? sensory.Damage : 0f) + sensory.Bleeding + healthDeficit + (sensory.BloodValid ? sensory.Blood : 0f) + sensory.LimbLoss + sensory.Breakage + sensory.JointStress + sensory.BrainDamage + sensory.InternalBleeding + sensory.Wounds + sensory.LungDamage + vitalityDeficit + sensory.Stabbed + sensory.Seizure);
            var circulationDeficit = sensory.CirculationValid && IsFinite(sensory.Circulation) ? 1f - Unit(sensory.Circulation) : 0f;
            var hazard = Unit(sensory.Fire + sensory.Heat + sensory.Cold + sensory.Shock + sensory.SubmergedHypoxia + oxygenDeficit + sensory.Wetness + sensory.Charge + sensory.Infection + sensory.AcidExposure + sensory.LiquidHazard + sensory.Lava + sensory.BurnProgress + sensory.Disconnected + sensory.Frozen + sensory.Fall + sensory.Projectile + sensory.AmbientHeat + sensory.AmbientCold + circulationDeficit);
            var bodyMotion = Unit(sensory.Impact + sensory.Sound * .6f + sensory.Velocity * .6f + sensory.Rotation * .4f + sensory.Balance * .4f + sensory.Numbness + sensory.Paralysis + sensory.Weightless + sensory.Sliding + sensory.Fall * .6f + sensory.Vibration * .35f + sensory.Proprioception * .4f + sensory.PhysicalContact * .15f + sensory.Touch * .15f + sensory.LiquidExposure + sensory.LiquidWater + sensory.Heartbeat * .1f);
            var visual = Unit(sensory.Light + sensory.Vision);
            var arousal = Unit(sensory.Adrenaline + (sensory.ConsciousnessValid ? sensory.Unconscious : 0f) + consciousnessDeficit + sensory.LiquidStimulation);
            injuryDrive = injury;
            hazardDrive = hazard;
            motionDrive = bodyMotion;
            arousalDrive = arousal;
            LastSensoryDrive = Unit(injury + hazard + bodyMotion + arousal);

            Drive("superclass:ol_sensory", LastSensoryDrive, 2048);
            Drive("type:R1-R6", visual, 256);
            Drive("type:R7R8_unclear", sensory.Light, 64);
            Drive("type:R7_unclear", sensory.Light, 64);
            Drive("type:R7d", sensory.Light, 64);
            Drive("type:R7p", sensory.Light, 64);
            Drive("type:R7y", sensory.Light, 64);
            Drive("type:R8_unclear", sensory.Light, 64);
            Drive("type:R8d", sensory.Light, 64);
            Drive("type:R8p", sensory.Light, 64);
            Drive("type:R8y", sensory.Light, 64);
            Drive("type:LC4", Unit(hazard + sensory.Nearby), 128);
            Drive("type:LPLC2", Unit(injury + hazard + sensory.LiquidSedation), 128);
            Drive("type:MDN", Unit(injury + sensory.SubmergedHypoxia), 128);
            Drive("type:DNp09", Unit(visual + bodyMotion), 128);
        }

        private float LastSensoryDrive { get; set; }

        private void Drive(string population, float value, int maximum)
        {
            value = Clamp(value, 0f, 4f);
            if (value <= .001f) return;
            var ids = asset.Population(population);
            var count = Math.Min(maximum, ids.Count);
            for (var i = 0; i < count; i++)
            {
                var id = ids[i];
                if (simulationTick < refractoryUntil[id]) continue;
                var queued = pending.TryGetValue(id, out var valueAtId) ? valueAtId : 0f;
                pending[id] = Clamp(queued + value, -4f, 4f);
                active.Add(id);
                priority.Add(id);
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
