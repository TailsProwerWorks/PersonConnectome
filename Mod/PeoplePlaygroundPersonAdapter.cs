using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    internal sealed class PeoplePlaygroundPersonAdapter
    {
        private const float DeathHealthThreshold = .001f;
        private readonly GameObject root;
        private readonly PersonBehaviour person;
        private readonly List<LimbBehaviour> limbs = [];
        private readonly List<PersonConnectomeLimbController> limbControllers = [];
        private readonly List<LimbBehaviour> discoveredLimbs = [];
        private readonly Dictionary<CirculationBehaviour, float> bloodBaselines = [];
        private readonly Action<float> reportCollision;
        private float appliedWalk;
        private int appliedLimbCount;
        private bool hasAppliedControl;
        private readonly float visionRadius;
        private readonly Collider2D[] nearbyColliders = new Collider2D[128];
        private float collision;
        private SensoryFrame lastFrame;
        private bool hasReadFrame;
        private string audioSourceSummary = "none";
        public bool IsUsable { get { return person != null && LimbCount > 0; } }
        public bool IsTerminal { get { return hasReadFrame && (lastFrame.BrainDead || (lastFrame.HealthValid && !lastFrame.Alive)); } }
        public bool IsBrainDead { get { return hasReadFrame && lastFrame.BrainDead; } }
        public string CapabilitySummary { get { return "walk=" + (person != null) + " limbs=" + LimbCount + " capable=" + DrivenLimbCount + "/" + LimbCount + " grip=optional sensors=" + SensorSummary; } }
        public Transform StatusAnchor { get { return FindStatusAnchor(); } }
        public string LiveState { get { return GetLiveState(); } }
        public string LiveSignal { get { return GetLiveSignal(); } }
        public float LiveSignalValue { get { return GetLiveSignalValue(); } }
        public float LiveThreat { get { return GetLiveThreat(); } }
        public string LiveBodySummary { get { return "BODY: hp=" + (lastFrame.HealthValid ? lastFrame.Health.ToString("0.00") : "unknown") + " dmg=" + lastFrame.Damage.ToString("0.00") + " pain=" + lastFrame.Pain.ToString("0.00") + " shock=" + lastFrame.Shock.ToString("0.00") + " o2=" + lastFrame.Oxygen.ToString("0.00") + " brain=" + BrainSummary; } }
        public string LiveEnvironmentSummary { get { return "ENV: fire=" + lastFrame.Fire.ToString("0.00") + " heat=" + lastFrame.Heat.ToString("0.00") + " near=" + lastFrame.Nearby.ToString("0.00") + " sound=" + lastFrame.Sound.ToString("0.00") + " liquid=" + lastFrame.LiquidExposure.ToString("0.00"); } }
        public string LiveAudioSummary { get { return "AUDIO: " + audioSourceSummary; } }
        public string LiveLimbSummary { get { return "LIMBS: " + LimbCount + " capable=" + DrivenLimbCount + "/" + LimbCount + " applied=" + appliedLimbCount + " walk=" + appliedWalk.ToString("0.00") + " excluded=" + ExcludedLimbSummary; } }
        private string ExcludedLimbSummary
        {
            get
            {
                var excluded = new List<string>();
                foreach (var controller in limbControllers)
                {
                    var diagnostic = controller == null ? "missing-controller" : controller.DiagnosticSummary;
                    if (!String.IsNullOrEmpty(diagnostic)) excluded.Add(diagnostic);
                }

                return excluded.Count == 0 ? "none" : String.Join(",", excluded.ToArray());
            }
        }
        private int LimbCount { get { var count = 0; foreach (var limb in limbs) { if (limb != null) count++; } return count; } }
        private int DrivenLimbCount
        {
            get
            {
                if (limbControllers == null)
                {
                    return 0;
                }

                var count = 0;
                foreach (var controller in limbControllers)
                {
                    if (controller != null && controller.CanDrive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        private string SensorSummary { get { return "health=" + lastFrame.Health.ToString("0.00") + " damage=" + lastFrame.Damage.ToString("0.00") + " pain=" + lastFrame.Pain.ToString("0.00") + " fire=" + lastFrame.Fire.ToString("0.00") + " shock=" + lastFrame.Shock.ToString("0.00") + " oxygen=" + lastFrame.Oxygen.ToString("0.00") + " nearby=" + lastFrame.Nearby.ToString("0.00") + " liquid=" + lastFrame.LiquidExposure.ToString("0.00"); } }

        public PeoplePlaygroundPersonAdapter(GameObject root, float visionRadius, Action<float> reportCollision)
        {
            this.root = root;
            this.visionRadius = IsFinite(visionRadius) ? Mathf.Clamp(visionRadius, 1f, 30f) : 8f;
            this.reportCollision = reportCollision;
            person = root.GetComponent<PersonBehaviour>();
            RefreshLimbs();
        }

        private void RefreshLimbs()
        {
            for (var i = limbs.Count - 1; i >= 0; i--)
            {
                if (limbs[i] == null)
                {
                    limbControllers[i].Stop();
                    limbs.RemoveAt(i);
                    limbControllers.RemoveAt(i);
                }
            }

            if (root == null) return;
            root.GetComponentsInChildren(true, discoveredLimbs);
            foreach (var limb in discoveredLimbs) RegisterLimb(limb);
            if (person != null && person.Limbs != null)
            {
                foreach (var limb in person.Limbs) RegisterLimb(limb);
            }
        }

        private void RegisterLimb(LimbBehaviour limb)
        {
            if (limb == null || limbs.Contains(limb)) return;
            limbs.Add(limb);
            limbControllers.Add(new PersonConnectomeLimbController(limb, root.transform));
            AttachProbe(limb.gameObject, root.transform, reportCollision);
        }

        private static void AttachProbe(GameObject limbObject, Transform ownerRoot, Action<float> reportCollision)
        {
            var probe = limbObject.GetComponent<PersonConnectomeLimbProbe>() ?? limbObject.AddComponent<PersonConnectomeLimbProbe>();

            probe.OwnerRoot = ownerRoot;
            probe.Report = reportCollision;
        }

        private Transform FindStatusAnchor()
        {
            if (limbs != null)
            {
                foreach (var limb in limbs)
                {
                    if (limb != null && limb.HasBrain)
                    {
                        return limb.transform;
                    }
                }
            }

            return root == null ? null : root.transform;
        }

        private string GetLiveState()
        {
            if (!IsUsable)
            {
                return "OFFLINE";
            }

            if (!hasReadFrame)
            {
                return "INITIALIZING";
            }

            if (IsBrainDead)
            {
                return "BRAIN DEAD";
            }

            if (!lastFrame.HealthValid) return "INVALID DATA";

            if (IsTerminal)
            {
                return "DEAD";
            }

            if (lastFrame.Consciousness <= .8f)
            {
                return "UNCONSCIOUS";
            }

            if (lastFrame.LiquidSedation > .05f)
            {
                return "SEDATED";
            }

            if (lastFrame.BrainDamage > .05f)
            {
                return "BRAIN INJURED";
            }

            if (LiveThreat > .05f)
            {
                return "THREAT";
            }

            if (lastFrame.Sound > .05f || lastFrame.Impact > .05f)
            {
                return "ALERT";
            }

            if (lastFrame.Nearby > .05f || lastFrame.LiquidExposure > .05f || lastFrame.Touch > .05f || lastFrame.PhysicalContact > .05f)
            {
                return "SENSING";
            }

            return "CALM";
        }

        private string GetLiveSignal() => SelectLiveSignal().Name;
        private float GetLiveSignalValue() => SelectLiveSignal().Value;

        // Keep label and value together. Prioritize critical state, then body hazards,
        // then informative environmental cues (proximity must not hide playing audio).
        private LiveReading SelectLiveSignal()
        {
            if (!hasReadFrame) return new LiveReading("NO DATA", 0f);
            if (IsBrainDead) return new LiveReading("BRAIN DEATH", 1f);
            if (!lastFrame.HealthValid) return new LiveReading("INVALID HEALTH", 0f);
            if (IsTerminal) return new LiveReading("DEATH", 1f);
            if (lastFrame.BrainDamage > .05f) return new LiveReading("BRAIN INJURY", lastFrame.BrainDamage);
            if (lastFrame.LiquidHazard > .05f) return new LiveReading("ACID/POISON", lastFrame.LiquidHazard);
            var fire = Mathf.Max(lastFrame.Fire, lastFrame.Lava);
            if (fire > .05f) return new LiveReading("FIRE/LAVA", fire);
            if (lastFrame.SubmergedHypoxia > .05f) return new LiveReading("SUBMERGED HYPOXIA", lastFrame.SubmergedHypoxia);
            if (lastFrame.Oxygen < .95f) return new LiveReading("LOW OXYGEN", 1f - lastFrame.Oxygen);
            var shock = Mathf.Max(lastFrame.Shock, lastFrame.Charge);
            if (shock > .05f) return new LiveReading("SHOCK", shock);
            var injury = Mathf.Max(lastFrame.Pain, Mathf.Max(lastFrame.Damage, lastFrame.Bleeding));
            if (injury > .05f) return new LiveReading("PAIN/INJURY", injury);
            if (lastFrame.LiquidSedation > .05f) return new LiveReading("SEDATION", lastFrame.LiquidSedation);
            var stimulation = Mathf.Max(lastFrame.LiquidStimulation, lastFrame.Adrenaline);
            if (stimulation > .05f) return new LiveReading("STIMULATION", stimulation);
            if (lastFrame.LiquidHealing > .05f) return new LiveReading("HEALING", lastFrame.LiquidHealing);
            if (lastFrame.Sound > .05f) return new LiveReading("OBJECT AUDIO", lastFrame.Sound);
            if (lastFrame.Impact > .05f) return new LiveReading("CONTACT IMPACT", lastFrame.Impact);
            if (lastFrame.Nearby > .05f) return new LiveReading("NEARBY", lastFrame.Nearby);
            var contact = Mathf.Max(lastFrame.Touch, lastFrame.PhysicalContact);
            if (contact > .05f) return new LiveReading("CONTACT", contact);
            if (lastFrame.UnderWater > .05f) return new LiveReading("UNDERWATER", lastFrame.UnderWater);
            if (lastFrame.LiquidWater > .05f) return new LiveReading("INTERNAL WATER", lastFrame.LiquidWater);
            if (lastFrame.Wetness > .05f) return new LiveReading("WETNESS", lastFrame.Wetness);
            return lastFrame.Velocity > .05f ? new LiveReading("SELF-MOTION", lastFrame.Velocity) : new LiveReading("NONE", 0f);
        }

        private struct LiveReading
        {
            public readonly string Name;
            public readonly float Value;
            public LiveReading(string name, float value) { Name = name; Value = value; }
        }

        private float GetLiveThreat()
        {
            return Mathf.Clamp01(lastFrame.Pain + lastFrame.Damage + lastFrame.Bleeding + lastFrame.Fire + lastFrame.Heat + lastFrame.Cold + lastFrame.Shock + lastFrame.SubmergedHypoxia + lastFrame.LiquidHazard + lastFrame.Lava + lastFrame.BrainDamage + lastFrame.Seizure + lastFrame.Frozen + lastFrame.LimbLoss + lastFrame.Disconnected);
        }

        private string BrainSummary
        {
            get
            {
                if (!hasReadFrame)
                {
                    return "unknown";
                }

                if (IsBrainDead)
                {
                    return "dead";
                }

                return lastFrame.BrainDamage > .05f ? "injured" : "ok";
            }
        }

        public void RegisterCollision(float magnitude)
        {
            collision = Mathf.Max(collision, Unit(magnitude / 20f));
        }

        public SensoryFrame Read()
        {
            RefreshLimbs();
            if (!IsUsable)
            {
                lastFrame = new SensoryFrame();
                hasReadFrame = true;
                return lastFrame;
            }

            var frame = ReadPersonState();
            audioSourceSummary = "none";
            var healthSum = 0f;
            var healthCount = 0;
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                healthSum += ReadLimb(ref frame, limb);
                healthCount++;
            }

            frame.Damage = healthCount == 0 ? 0f : 1f - healthSum / healthCount;
            ApplyCollision(ref frame);
            ReadNearby(ref frame);
            frame.BrainDead = person.Braindead;
            frame.HealthValid = IsFinite(person.AverageHealth);
            frame.Alive = frame.HealthValid && !person.Braindead && !HasZeroHealth(person.AverageHealth);
            frame.SubmergedHypoxia = frame.UnderWater * (1f - frame.Oxygen);
            lastFrame = frame;
            hasReadFrame = true;
            return frame;
        }

        public void Apply(MotorCommand command, bool chemistry)
        {
            hasAppliedControl = true;
            appliedLimbCount = 0;
            if (!IsUsable || !hasReadFrame || !lastFrame.Alive || lastFrame.Consciousness <= .8f ||
                !IsFinite(command.Freeze) || command.Freeze > .5f)
            {
                Stop();
                return;
            }

            var walk = command.Walk * (1f - Unit(command.Avoid) * .25f);
            ApplyWalking(walk);
            foreach (var controller in limbControllers)
            {
                if (controller.Apply(command)) appliedLimbCount++;
                controller.ApplyChemistry(command, chemistry);
            }

            if (chemistry)
            {
                var adrenalineChange = Unit(command.Stimulate) * .05f - Unit(command.Calm) * .05f;
                if (adrenalineChange != 0f && IsFinite(person.AdrenalineLevel))
                    person.AdrenalineLevel = Unit(person.AdrenalineLevel + adrenalineChange);
            }
        }

        public void Stop()
        {
            // A failed asset load never activates control. Disposing that adapter
            // must not clear native walking, joint or held-object state.
            if (!hasAppliedControl) return;
            appliedLimbCount = 0;
            ApplyWalking(0f);
            foreach (var controller in limbControllers) controller.Stop();
            hasAppliedControl = false;
        }

        public void Dispose()
        {
            Stop();
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                var probe = limb.GetComponent<PersonConnectomeLimbProbe>();
                if (probe != null && probe.OwnerRoot == root.transform) probe.Report = null;
            }
        }

        private SensoryFrame ReadPersonState()
        {
            var frame = new SensoryFrame
            {
                Pain = Unit(person.PainLevel),
                Shock = Unit(person.ShockLevel),
                Consciousness = Unit(person.Consciousness),
                Adrenaline = Unit(person.AdrenalineLevel),
                Oxygen = Unit(person.OxygenLevel),
                Health = Unit(person.AverageHealth),
                Fire = Unit(person.AverageFireIntensity),
                Wetness = Unit(person.AverageWetness),
                Velocity = Unit(person.AverageSpeed / 10f),
                Touch = person.IsTouchingFloor ? 1f : 0f,
                Light = Unit(RenderSettings.ambientLight.grayscale),
                Rotation = Unit(Mathf.Abs(person.AngleOffset) / 180f),
                Balance = Unit(Mathf.Abs(person.BalanceOffset) / 10f),
                Heartbeat = Unit(person.Heartbeat),
                BrainDamage = person.BrainDamaged ? 1f : 0f,
                Seizure = Unit(person.SeizureTime / 5f),
                Vitality = 1f,
                Circulation = 1f
            };
            frame.BrainDamage = Mathf.Max(frame.BrainDamage, person.Braindead ? 1f : 0f);
            frame.BrainDamage = Mathf.Max(frame.BrainDamage, Unit(person.BrainDamagedTime / 5f));
            frame.Unconscious = 1f - frame.Consciousness;
            return frame;
        }

        private static bool HasZeroHealth(float averageHealth)
        {
            return !float.IsNaN(averageHealth) && !float.IsInfinity(averageHealth) && averageHealth <= DeathHealthThreshold;
        }

        private float ReadLimb(ref SensoryFrame frame, LimbBehaviour limb)
        {
            var health = Unit(limb.Health / Mathf.Max(1f, limb.InitialHealth));
            frame.LimbLoss = Mathf.Max(frame.LimbLoss, limb.IsDismembered || limb.Broken || limb.CurrentlyShattered != 0 ? 1f : 0f);
            frame.JointStress = Mathf.Max(frame.JointStress, Unit(limb.JointStress / 100f));
            frame.Heat = Mathf.Max(frame.Heat, TemperatureHeat(limb.BodyTemperature));
            frame.Heat = Mathf.Max(frame.Heat, TemperatureHeat(limb.InternalTemperature));
            frame.Cold = Mathf.Max(frame.Cold, TemperatureCold(limb.BodyTemperature));
            frame.Cold = Mathf.Max(frame.Cold, TemperatureCold(limb.InternalTemperature));
            frame.Frozen = Mathf.Max(frame.Frozen, limb.Frozen ? 1f : 0f);
            frame.Paralysis = Mathf.Max(frame.Paralysis, limb.IsParalysed ? 1f : 0f);
            frame.Paralysis = Mathf.Max(frame.Paralysis, limb.IsCapable ? 0f : 1f);
            frame.Numbness = Mathf.Max(frame.Numbness, Unit(limb.Numbness));
            frame.Vitality = Mathf.Min(frame.Vitality, ReadVitality(limb, health));
            frame.LungDamage = Mathf.Max(frame.LungDamage, limb.HasLungs && limb.LungsPunctured ? 1f : 0f);
            frame.Touch = Mathf.Max(frame.Touch, limb.IsOnFloor ? 1f : 0f);
            ReadCirculation(ref frame, limb.CirculationBehaviour);
            ReadPhysical(ref frame, limb.PhysicalBehaviour);
            frame.Infection = Mathf.Max(frame.Infection, limb.IsZombie ? 1f : 0f);
            return health;
        }

        private void ReadCirculation(ref SensoryFrame frame, CirculationBehaviour circulation)
        {
            if (circulation == null)
            {
                return;
            }

            frame.Bleeding = Mathf.Max(frame.Bleeding, Unit(circulation.BleedingRate));
            frame.InternalBleeding = Mathf.Max(frame.InternalBleeding, Unit(circulation.InternalBleedingIntensity));
            frame.Heartbeat = Mathf.Max(frame.Heartbeat, Unit(circulation.GetHeartRate() / 120f));
            frame.Circulation = Mathf.Min(frame.Circulation, circulation.HasBloodFlow ? Unit(circulation.BloodFlow) : 0f);
            frame.Disconnected = Mathf.Max(frame.Disconnected, circulation.IsDisconnected || !circulation.HasCirculation ? 1f : 0f);
            frame.Wounds = Mathf.Max(frame.Wounds, Unit((circulation.StabWoundCount + circulation.GunshotWoundCount + circulation.BleedingPointCount) / 8f));
            frame.Blood = Mathf.Max(frame.Blood, ReadBloodDeficit(circulation));
            ReadLiquidIdentities(ref frame, circulation);
        }

        private static float ReadVitality(LimbBehaviour limb, float health)
        {
            return IsFinite(limb.Vitality) && limb.Vitality > .001f ? Unit(limb.Vitality) : health;
        }

        private float ReadBloodDeficit(CirculationBehaviour circulation)
        {
            var amount = circulation.GetAmountOfBlood();
            if (!IsFinite(amount) || amount <= .001f)
            {
                return bloodBaselines.ContainsKey(circulation) ? 1f : 0f;
            }

            if (!bloodBaselines.TryGetValue(circulation, out var baseline) || amount > baseline)
            {
                bloodBaselines[circulation] = amount;
                return 0f;
            }

            return 1f - Unit(amount / baseline);
        }

        private static void ReadLiquidIdentities(ref SensoryFrame frame, CirculationBehaviour circulation)
        {
            if (circulation.LiquidDistribution == null || circulation.TotalLiquidAmount <= .001f)
            {
                return;
            }

            foreach (var entry in circulation.LiquidDistribution)
            {
                var amount = Unit(entry.Value.Raw / Mathf.Max(.001f, circulation.TotalLiquidAmount));
                if (entry.Key == null || amount <= .001f)
                {
                    continue;
                }

                var identity = Liquid.GetIdentity(entry.Key);
                if (String.IsNullOrEmpty(identity))
                {
                    identity = entry.Key.GetDisplayName();
                }

                var isBlood = String.Equals(identity, "BLOOD", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(identity, "Human blood", StringComparison.OrdinalIgnoreCase);
                var isSedation = MatchesLiquid(identity, "knockout", "anesthetic", "anaesthetic", "sedation");
                if (!isBlood)
                {
                    frame.LiquidExposure = Mathf.Max(frame.LiquidExposure, amount);
                }

                if (MatchesLiquid(identity, "acid", "corrosion", "gorse", "toxin", "zombie") ||
                    MatchesLiquid(identity, "reanimation", "deconstruction", "combustion", "osteomorphosis", "tritium") ||
                    MatchesLiquid(identity, "nitro", "gasoline", "coolant", "oil", "explosive") ||
                    (!isSedation && MatchesLiquid(identity, "poison")))
                {
                    frame.LiquidHazard = Mathf.Max(frame.LiquidHazard, amount);
                }

                if (isSedation)
                {
                    frame.LiquidSedation = Mathf.Max(frame.LiquidSedation, amount);
                }

                if (MatchesLiquid(identity, "adrenaline", "stimulation", "enhancing", "ultra strength", "durability"))
                {
                    frame.LiquidStimulation = Mathf.Max(frame.LiquidStimulation, amount);
                }

                if (MatchesLiquid(identity, "regeneration", "healing", "immortality", "life serum", "mending") ||
                    MatchesLiquid(identity, "coagulation"))
                {
                    frame.LiquidHealing = Mathf.Max(frame.LiquidHealing, amount);
                }

                if (MatchesLiquid(identity, "water"))
                {
                    frame.LiquidWater = Mathf.Max(frame.LiquidWater, amount);
                }
            }
        }

        private static bool MatchesLiquid(string identity, string first, string second = null, string third = null, string fourth = null, string fifth = null)
        {
            if (String.IsNullOrEmpty(identity))
            {
                return false;
            }

            return ContainsLiquidName(identity, first) ||
                ContainsLiquidName(identity, second) ||
                ContainsLiquidName(identity, third) ||
                ContainsLiquidName(identity, fourth) ||
                ContainsLiquidName(identity, fifth);
        }

        private static bool ContainsLiquidName(string identity, string name)
        {
            return !String.IsNullOrEmpty(name) && identity.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReadPhysical(ref SensoryFrame frame, PhysicalBehaviour physical)
        {
            if (physical == null)
            {
                return;
            }

            frame.Fire = Mathf.Max(frame.Fire, GetFireValue(physical));
            frame.Heat = Mathf.Max(frame.Heat, TemperatureHeat(physical.Temperature));
            frame.Cold = Mathf.Max(frame.Cold, TemperatureCold(physical.Temperature));
            frame.BurnProgress = Mathf.Max(frame.BurnProgress, Unit(physical.BurnProgress));
            frame.Wetness = Mathf.Max(frame.Wetness, Unit(physical.Wetness));
            frame.Charge = Mathf.Max(frame.Charge, Unit(Mathf.Abs(physical.Charge)));
            frame.UnderWater = Mathf.Max(frame.UnderWater, physical.IsUnderWater ? 1f : 0f);
            frame.Wetness = Mathf.Max(frame.Wetness, frame.UnderWater);
            frame.Lava = Mathf.Max(frame.Lava, physical.IsInLava ? 1f : 0f);
            frame.Stabbed = Mathf.Max(frame.Stabbed, physical.IsBeingStabbed ? 1f : 0f);
            frame.PhysicalContact = Mathf.Max(frame.PhysicalContact, physical.IsTouchingSomething || physical.beingHeldByGripper ? 1f : 0f);
            frame.Weightless = Mathf.Max(frame.Weightless, physical.IsWeightless ? 1f : 0f);
            frame.Sliding = Mathf.Max(frame.Sliding, physical.isSliding ? 1f : 0f);
            frame.LimbLoss = Mathf.Max(frame.LimbLoss, physical.isDisintegrated ? 1f : 0f);
        }

        private static float GetFireValue(PhysicalBehaviour physical)
        {
            return physical.OnFire ? Unit(physical.BurnIntensity) : 0f;
        }

        private static float TemperatureHeat(float temperature)
        {
            return Unit((temperature - 39f) / 61f);
        }

        private static float TemperatureCold(float temperature)
        {
            return Unit((32f - temperature) / 32f);
        }

        private void ApplyCollision(ref SensoryFrame frame)
        {
            frame.Touch = Mathf.Max(frame.Touch, collision);
            frame.Impact = Mathf.Max(frame.Impact, collision);
            collision *= .65f;
        }

        private void ApplyWalking(float walk)
        {
            appliedWalk = IsFinite(walk) ? Mathf.Clamp(walk, -1f, 1f) : 0f;
            if (person != null) person.DesiredWalkingDirection = appliedWalk;
        }

        private bool IsOwnTransform(Transform candidate)
        {
            if (candidate == root.transform || candidate.IsChildOf(root.transform)) return true;
            foreach (var limb in limbs)
            {
                if (limb != null && (candidate == limb.transform || candidate.IsChildOf(limb.transform))) return true;
            }
            return false;
        }

        private void ReadNearby(ref SensoryFrame f)
        {
            var closest = float.MaxValue;
            var origin = (Vector2)root.transform.position;
            var hitCount = Physics2D.OverlapCircleNonAlloc(origin, visionRadius, nearbyColliders);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = nearbyColliders[i];
                if (hit == null || IsOwnTransform(hit.transform))
                {
                    continue;
                }

                var physical = hit.GetComponentInParent<PhysicalBehaviour>();
                if (physical == null)
                {
                    continue;
                }

                var delta = hit.ClosestPoint(origin) - origin;
                var distance = delta.magnitude;
                if (!IsFinite(distance) || !IsFinite(delta.x)) continue;
                ReadExternalSound(ref f, physical, distance);
                if (distance < closest)
                {
                    closest = distance;
                    f.Nearby = Mathf.Clamp01(1f - distance / visionRadius);
                    f.NearbyDirection = Mathf.Abs(delta.x) < .001f ? 0f : Mathf.Sign(delta.x);
                }
            }
        }

        private void ReadExternalSound(ref SensoryFrame frame, PhysicalBehaviour physical, float distance)
        {
            var audio = physical == null ? null : physical.MainAudioSource;
            if (physical == null || IsOwnTransform(physical.transform) || physical.GetComponentInParent<PersonBehaviour>() == person ||
                audio == null || IsOwnTransform(audio.transform) ||
                !audio.isPlaying || audio.mute || !audio.isActiveAndEnabled)
            {
                return;
            }

            var distanceSignal = Mathf.Clamp01(1f - distance / visionRadius);
            var signal = Unit(audio.volume) * distanceSignal;
            if (signal > frame.Sound)
            {
                frame.Sound = signal;
                audioSourceSummary = "external " + physical.name + "/" + audio.name + " d=" + distance.ToString("0.00") + " signal=" + signal.ToString("0.00") + " volume=" + Unit(audio.volume).ToString("0.00");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Unit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp01(value);
        }
    }
}
