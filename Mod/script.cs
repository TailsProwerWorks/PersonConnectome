// Standalone People Playground script. The brain is deliberately separate from
// PeoplePlaygroundPersonAdapter so the bounded control logic has no game types.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public static class Mod
    {
        public static void Main()
        {
            var human = ModAPI.FindSpawnable("Human");
            if (human == null)
            {
                Debug.Log("Person Connectome: Human spawnable was not found; active variation was not registered.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                NameOverride = "Person Connectome (Active)",
                DescriptionOverride = "Active bounded connectome control with live overhead telemetry.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                AfterSpawn = instance =>
                {
                    if (instance != null && instance.GetComponent<PersonConnectomeController>() == null)
                    {
                        instance.AddComponent<PersonConnectomeController>();
                    }
                }
            });
        }
    }

    internal sealed partial class ConnectomeBrain
    {
        private static readonly object AssetLock = new();
        private static RuntimeAsset sharedAsset;
        private readonly RuntimeAsset asset;
        private readonly float[] potential;
        private readonly long[] refractoryUntil;
        private readonly Dictionary<int, float> pending = [];
        private readonly HashSet<int> active = [];
        private readonly HashSet<int> priority = [];
        private readonly HashSet<int> scheduled = [];
        private readonly List<int> fired = [];
        private readonly HashSet<int> firedIds = [];
        private readonly Dictionary<int, float> nextPending = [];
        private readonly HashSet<int> nextActive = [];
        private MotorCommand lastCommand;
        private bool stopped;
        private long simulationTick;
        private long backlogCursor;
        private int processedThisStep;
        private int deferredThisStep;
        private float injuryDrive, hazardDrive, motionDrive, arousalDrive;
        private const int MaxActivePerStep = 24000;
        private const int RefractoryTicks = 5;

        private ConnectomeBrain(RuntimeAsset asset)
        {
            this.asset = asset;
            potential = new float[asset.NeuronCount];
            refractoryUntil = new long[asset.NeuronCount];
        }
        public string Status { get { return "MaleCNS v1.0 " + asset.NeuronCount + " neurons / " + asset.EdgeCount + (stopped ? " STOPPED" : " queued=" + pending.Count + " processed=" + processedThisStep + " deferred=" + deferredThisStep + " fired=" + fired.Count + " input=" + LastSensoryDrive.ToString("0.00") + " request-walk=" + lastCommand.Walk.ToString("0.00")); } }
        public string DisplaySummary { get { return stopped ? "NEURAL: STOPPED\n  queued=0  processed=0  deferred=0  fired=0" : "NEURAL:\n  input=" + LastSensoryDrive.ToString("0.00") + "  queued=" + pending.Count + "  processed=" + processedThisStep + "\n  deferred=" + deferredThisStep + "  fired=" + fired.Count; } }
        public string DisplayInputSummary { get { return stopped ? "INPUT: STOPPED" : "INPUT:\n  injury=" + injuryDrive.ToString("0.00") + "  hazard=" + hazardDrive.ToString("0.00") + "  motion=" + motionDrive.ToString("0.00") + "\n  arousal=" + arousalDrive.ToString("0.00") + "  total=" + LastSensoryDrive.ToString("0.00"); } }
        public string DisplayMotorSummary { get { return stopped ? "REQUEST: STOPPED\n  arms=0.00/0.00  legs=0.00/0.00\n  head=0.00  core=0.00  grips=0.00/0.00" : "REQUEST:\n  arms=" + lastCommand.LeftArm.ToString("0.00") + "/" + lastCommand.RightArm.ToString("0.00") + "  legs=" + lastCommand.LeftLeg.ToString("0.00") + "/" + lastCommand.RightLeg.ToString("0.00") + "\n  head=" + lastCommand.Head.ToString("0.00") + "  core=" + lastCommand.Core.ToString("0.00") + "  grips=" + lastCommand.LeftGrip.ToString("0.00") + "/" + lastCommand.RightGrip.ToString("0.00"); } }

        public static ConnectomeBrain TryCreate(out string status)
        {
            lock (AssetLock)
            {
                if (sharedAsset == null && !RuntimeAsset.TryLoad(out sharedAsset, out status))
                {
                    return null;
                }
                status = "MaleCNS v1.0 loaded";
                return new ConnectomeBrain(sharedAsset);
            }
        }

        public MotorCommand Step(SensoryFrame sensory)
        {
            if (!sensory.Alive)
            {
                return Stop();
            }

            if (stopped)
            {
                ResetAfterStop();
            }

            simulationTick++;
            processedThisStep = 0;
            deferredThisStep = 0;
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
            ReplacePendingState(nextPending, nextActive);
            priority.Clear();

            return BuildMotorCommand(sensory);
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
                simulationTick = 0;
                backlogCursor = 0;
                stopped = true;
            }

            LastSensoryDrive = 0f;
            processedThisStep = 0;
            deferredThisStep = 0;
            lastCommand = new MotorCommand();
            return lastCommand;
        }

        private void ResetAfterStop()
        {
            stopped = false;
        }

        private void ProcessActiveNeurons(Dictionary<int, float> next, HashSet<int> nextActive)
        {
            if (active.Count > MaxActivePerStep)
            {
                ProcessFairBacklog(next, nextActive);
                return;
            }

            foreach (var id in active)
            {
                ProcessActiveNeuron(id, next, nextActive);
            }
        }

        private void ProcessFairBacklog(Dictionary<int, float> next, HashSet<int> nextActive)
        {
            var ordered = new List<int>(active);
            ordered.Sort();
            var start = (int)(backlogCursor % ordered.Count);
            scheduled.Clear();
            var budget = MaxActivePerStep;
            foreach (var id in priority)
            {
                if (budget == 0)
                {
                    break;
                }

                ProcessActiveNeuron(id, next, nextActive);
                scheduled.Add(id);
                budget--;
            }

            var generalProcessed = 0;
            for (var index = 0; index < ordered.Count && budget > 0; index++)
            {
                var id = ordered[(start + index) % ordered.Count];
                if (priority.Contains(id))
                {
                    continue;
                }

                ProcessActiveNeuron(id, next, nextActive);
                scheduled.Add(id);
                generalProcessed++;
                budget--;
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                var id = ordered[index];
                if (!scheduled.Contains(id))
                {
                    DeferNeuron(id, next, nextActive);
                }
            }

            backlogCursor = (start + generalProcessed) % ordered.Count;
        }

        private void ProcessActiveNeuron(int id, Dictionary<int, float> next, HashSet<int> nextActive)
        {
            processedThisStep++;
            if (simulationTick < refractoryUntil[id])
            {
                potential[id] = 0f;
                return;
            }

            ProcessNeuron(id, next, nextActive);
        }

        private void DeferNeuron(int id, Dictionary<int, float> next, HashSet<int> nextActive)
        {
            deferredThisStep++;
            nextActive.Add(id);
            if (pending.TryGetValue(id, out var queued))
            {
                MergePending(next, id, queued);
            }
        }

        private void ProcessNeuron(int id, Dictionary<int, float> next, HashSet<int> nextActive)
        {
            var input = pending.TryGetValue(id, out var queued) ? queued : 0f;
            var updatedPotential = Mathf.Clamp(potential[id] * .92f + Mathf.Clamp(input, -4f, 4f), -8f, 8f);
            if (Mathf.Abs(updatedPotential) > .001f)
            {
                nextActive.Add(id);
            }

            if (updatedPotential < 1f)
            {
                potential[id] = updatedPotential;
                return;
            }

            potential[id] = 0f;
            refractoryUntil[id] = simulationTick + RefractoryTicks + 1;
            fired.Add(id);
            firedIds.Add(id);
            QueueOutgoing(id, next, nextActive);
        }

        private void QueueOutgoing(int source, Dictionary<int, float> next, HashSet<int> nextActive)
        {
            var start = asset.RowPointers[source];
            var end = asset.RowPointers[source + 1];
            for (var edge = start; edge < end; edge++)
            {
                var target = asset.PostIndexes[edge];
                var amount = asset.Weights[edge] * asset.NtSigns[source];
                MergePending(next, target, amount);
                nextActive.Add(target);
            }
        }

        private static void MergePending(Dictionary<int, float> destination, int id, float amount)
        {
            if (!destination.TryGetValue(id, out var queued))
            {
                queued = 0f;
            }

            destination[id] = Mathf.Clamp(queued + amount, -4f, 4f);
        }

        private void ReplacePendingState(Dictionary<int, float> next, HashSet<int> nextActive)
        {
            pending.Clear();
            foreach (var pair in next)
            {
                pending[pair.Key] = pair.Value;
            }

            active.Clear();
            foreach (var id in nextActive)
            {
                active.Add(id);
            }
        }

        private MotorCommand BuildMotorCommand(SensoryFrame sensory)
        {
            var danger = IsDangerous(sensory);
            var left = FiredOnSide("L");
            var right = FiredOnSide("R");
            var center = FiredOnSide("M");
            var sideBias = right - left;
            var neuralWalk = Mathf.Clamp(sideBias + (Fired("type:DNp09") ? .35f : 0f) - (Fired("type:MDN") ? .5f : 0f), -1f, 1f);
            var walk = danger ? EscapeDirection(sensory.NearbyDirection) : neuralWalk;
            var armSwing = ClampSigned((left - right) * .75f + walk * .35f + center * .15f);
            var reach = Fired("type:MN9") && !danger ? sensory.Nearby : 0f;
            lastCommand = new MotorCommand
            {
                Walk = walk,
                LeftArm = ClampSigned(-armSwing),
                RightArm = armSwing,
                LeftLeg = ClampSigned(walk - sideBias * .2f),
                RightLeg = ClampSigned(walk + sideBias * .2f),
                Core = ClampSigned(walk * .6f + center * .15f),
                Head = ClampSigned(sideBias * .5f),
                ReachGrab = reach,
                LeftGrip = reach,
                RightGrip = reach,
                Avoid = danger ? 1f : 0f,
                Freeze = Mathf.Clamp01(sensory.Unconscious + sensory.LiquidSedation),
                Heal = Mathf.Clamp01(sensory.Damage + sensory.Bleeding + sensory.LiquidHealing),
                Stimulate = Mathf.Clamp01(sensory.Adrenaline + sensory.LiquidStimulation),
                Calm = Mathf.Clamp01(sensory.Shock + sensory.Pain + sensory.LiquidSedation),
                Extinguish = sensory.Fire
            };
            return lastCommand;
        }

        private static float ClampSigned(float value)
        {
            return Mathf.Clamp(value, -1f, 1f);
        }

        private bool IsDangerous(SensoryFrame sensory)
        {
            return sensory.Pain + sensory.Fire + sensory.Shock + sensory.SubmergedHypoxia > .5f || Fired("type:DNp01");
        }

        private static float EscapeDirection(float nearbyDirection)
        {
            var direction = nearbyDirection == 0f ? 1f : nearbyDirection;
            return -Mathf.Sign(direction);
        }

        private bool Fired(string population)
        {
            var ids = asset.Population(population);
            for (var i = 0; i < ids.Count; i++)
            {
                if (firedIds.Contains(ids[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private float FiredOnSide(string side)
        {
            var total = 0;
            var count = 0;
            for (var i = 0; i < fired.Count; i++)
            {
                var id = fired[i];
                if (!IsMotorNeuron(id))
                {
                    continue;
                }

                count++;
                if (asset.Sides[id].IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    total++;
                }
            }

            return count == 0 ? 0f : Mathf.Clamp01((float)total / count);
        }

        private bool IsMotorNeuron(int id)
        {
            var superclass = asset.Superclasses[id];
            return superclass.IndexOf("descending", StringComparison.OrdinalIgnoreCase) >= 0 || superclass.IndexOf("motor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }

}
