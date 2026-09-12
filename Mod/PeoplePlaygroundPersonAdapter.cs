using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    internal sealed class PeoplePlaygroundPersonAdapter
    {
        private const float DeathHealthThreshold = .001f;
        private const float ContactImpactAlertThreshold = .15f;
        private readonly GameObject root;
        private readonly PersonBehaviour person;
        private readonly List<LimbBehaviour> limbs = [];
        private readonly List<PersonConnectomeLimbController> limbControllers = [];
        private readonly List<LimbBehaviour> discoveredLimbs = [];
        private readonly Dictionary<CirculationBehaviour, float> bloodBaselines = [];
        private readonly Action<float> reportCollision;
        private readonly Action<float> reportProjectile;
        private float appliedWalk;
        private int appliedLimbCount;
        private float jointSpeedLimit = 30f;
        private bool jointSpeedValid = true;
        private bool hasAppliedControl;
        private readonly float visionRadius;
        private readonly Collider2D[] nearbyColliders = new Collider2D[128];
        private float collision;
        private float vibration;
        private float projectile;
        private SensoryFrame lastFrame;
        private float nativeAdrenaline;
        private bool hasReadFrame;
        private string audioSourceSummary = "none";
        private string lastHeardAudio;
        private float lastHeardAudioTime;
        private string visionTargetSummary = "none";
        private readonly Dictionary<string, LiquidReading> liquidReadings = new Dictionary<string, LiquidReading>(StringComparer.Ordinal);
        private int readableLiquidContainers;
        private int invalidLiquidReadings;
        public bool IsUsable { get { return person != null && LimbCount > 0; } }
        public bool HasSample => hasReadFrame;
        public bool IsTerminal { get { return hasReadFrame && (lastFrame.BrainDead || (lastFrame.HealthValid && !lastFrame.Alive)); } }
        public bool IsBrainDead { get { return hasReadFrame && lastFrame.BrainDead; } }
        public string CapabilitySummary { get { return "walk=" + (person != null) + " limbs=" + LimbCount + " capable=" + DrivenLimbCount + "/" + LimbCount + " grip=optional sensors=" + SensorSummary; } }
        public Transform StatusAnchor { get { return FindStatusAnchor(); } }
        public string LiveState { get { return GetLiveState(); } }
        public string LiveSignal { get { return GetLiveSignal(); } }
        public float LiveSignalValue { get { return GetLiveSignalValue(); } }
        public float LiveThreat { get { return GetLiveThreat(); } }
        public string LiveBodySummary
        {
            get
            {
                return "BODY:\n  native-hp=" + (lastFrame.HealthValid ? lastFrame.Health.ToString("0.00") : "unknown") + "  limb-damage=" + (lastFrame.DamageValid ? lastFrame.Damage.ToString("0.00") : "unknown") + "  pain=" + lastFrame.Pain.ToString("0.00") + "  shock=" + lastFrame.Shock.ToString("0.00") + "\n  oxygen=" + (lastFrame.OxygenValid ? lastFrame.Oxygen.ToString("0.00") : "unknown") + "  conscious=" + (lastFrame.ConsciousnessValid ? lastFrame.Consciousness.ToString("0.00") : "unknown") + "  unconscious=" + (lastFrame.ConsciousnessValid ? lastFrame.Unconscious.ToString("0.00") : "unknown") + "  adrenaline(raw)=" + (IsFinite(nativeAdrenaline) ? nativeAdrenaline.ToString("0.00") : "unknown") + "  adrenaline(normalized)=" + lastFrame.Adrenaline.ToString("0.00") + "\n  heartbeat=" + lastFrame.Heartbeat.ToString("0.00") + "  velocity=" + lastFrame.Velocity.ToString("0.00") + "  velocity-xy(native rigidbody)=" + (lastFrame.VelocityValid ? lastFrame.VelocityX.ToString("0.00") + "," + lastFrame.VelocityY.ToString("0.00") : "unknown") + "  falling=" + lastFrame.Fall.ToString("0.00") + "\n  rotation=" + lastFrame.Rotation.ToString("0.00") + "  tilt(native angle proxy)=" + (lastFrame.TiltValid ? lastFrame.SignedTilt.ToString("0.00") : "unknown") + "  balance=" + lastFrame.Balance.ToString("0.00") + "  proprioception(proxy)=" + lastFrame.Proprioception.ToString("0.00") + "\n  joint-reading position=" + (lastFrame.JointSensingValid ? lastFrame.JointPosition.ToString("0.00") : "unknown") + "  motion=" + (lastFrame.JointSensingValid ? lastFrame.JointMotion.ToString("0.00") : "unknown") + "  brain=" + BrainSummary;
            }
        }
        public string LiveInjurySummary
        {
            get
            {
                return "INJURY:\n  bleeding=" + lastFrame.Bleeding.ToString("0.00") + "  internal=" + lastFrame.InternalBleeding.ToString("0.00") + "  wounds=" + lastFrame.Wounds.ToString("0.00") + "  blood-loss=" + (lastFrame.BloodValid ? lastFrame.Blood.ToString("0.00") : "unknown") + "\n  vitality=" + (lastFrame.VitalityValid ? lastFrame.Vitality.ToString("0.00") : "unknown") + "  circulation=" + (lastFrame.CirculationValid ? lastFrame.Circulation.ToString("0.00") : "unknown") + "  limb-loss=" + lastFrame.LimbLoss.ToString("0.00") + "  breakage=" + lastFrame.Breakage.ToString("0.00") + "  disconnected=" + lastFrame.Disconnected.ToString("0.00") + "\n  joint-stress=" + lastFrame.JointStress.ToString("0.00") + "  paralysis=" + lastFrame.Paralysis.ToString("0.00") + "  numbness=" + lastFrame.Numbness.ToString("0.00") + "\n  lung-damage=" + lastFrame.LungDamage.ToString("0.00") + "  zombie(native)=" + lastFrame.Infection.ToString("0.00") + "  brain-damage=" + lastFrame.BrainDamage.ToString("0.00") + "  seizure=" + lastFrame.Seizure.ToString("0.00") + "  frozen=" + lastFrame.Frozen.ToString("0.00");
            }
        }
        public string LiveEnvironmentSummary
        {
            get
            {
                return "ENVIRONMENT:\n  fire=" + lastFrame.Fire.ToString("0.00") + "  lava=" + lastFrame.Lava.ToString("0.00") + "  acid=" + lastFrame.AcidExposure.ToString("0.00") + "  burn=" + lastFrame.BurnProgress.ToString("0.00") + "\n  heat=" + lastFrame.Heat.ToString("0.00") + "  cold=" + lastFrame.Cold.ToString("0.00") + "  ambient-heat=" + lastFrame.AmbientHeat.ToString("0.00") + "  ambient-cold=" + lastFrame.AmbientCold.ToString("0.00") + "  light=" + lastFrame.Light.ToString("0.00") + "\n  nearby=" + lastFrame.Nearby.ToString("0.00") + "  direction=" + lastFrame.NearbyDirection.ToString("0.00") + "  vision=" + lastFrame.Vision.ToString("0.00") + "  direction-world-x=" + (lastFrame.VisionDirectionValid ? lastFrame.VisionDirection.ToString("0.00") : "unknown") + "  approach proxy=" + lastFrame.VisualApproach.ToString("0.00") + "  target=" + visionTargetSummary + "\n  sound=" + lastFrame.Sound.ToString("0.00") + "  direction-world-x=" + (lastFrame.SoundDirectionValid ? lastFrame.SoundDirection.ToString("0.00") : "unknown") + "  impact=" + lastFrame.Impact.ToString("0.00") + "  vibration=" + lastFrame.Vibration.ToString("0.00") + "  projectile=" + lastFrame.Projectile.ToString("0.00") + "\n  touch=" + lastFrame.Touch.ToString("0.00") + "  contact/held=" + lastFrame.PhysicalContact.ToString("0.00") + "  wet=" + lastFrame.Wetness.ToString("0.00") + "\n  underwater=" + lastFrame.UnderWater.ToString("0.00") + "  submerged-hypoxia=" + lastFrame.SubmergedHypoxia.ToString("0.00") + "  liquid=" + lastFrame.LiquidExposure.ToString("0.00") + "\n  hazard-exposure=" + lastFrame.LiquidHazard.ToString("0.00") + "  sedative-exposure=" + lastFrame.LiquidSedation.ToString("0.00") + "  stimulant-exposure=" + lastFrame.LiquidStimulation.ToString("0.00") + "\n  restorative-exposure=" + lastFrame.LiquidHealing.ToString("0.00") + "  water-liquid=" + lastFrame.LiquidWater.ToString("0.00") + "  charge=" + lastFrame.Charge.ToString("0.00") + "\n  stabbed=" + lastFrame.Stabbed.ToString("0.00") + "  weightless=" + lastFrame.Weightless.ToString("0.00") + "  sliding=" + lastFrame.Sliding.ToString("0.00");
            }
        }
        public string LiveLiquidSummary
        {
            get
            {
                if (!hasReadFrame) return "CIRCULATING LIQUIDS: not sampled";
                var summary = "CIRCULATING LIQUIDS\nHighest fraction in any tracked limb (including detached limbs).\nExposure categories are adapter mappings, not measured effect strength.";
                if (liquidReadings.Count == 0)
                {
                    summary += readableLiquidContainers == 0 ? "\n  unknown: no readable circulation" : "\n  none detected";
                }
                else
                {
                    var names = new List<string>(liquidReadings.Keys);
                    names.Sort(StringComparer.Ordinal);
                    foreach (var name in names)
                    {
                        var reading = liquidReadings[name];
                        summary += "\n  " + name + ": " + (reading.Fraction * 100f).ToString("0.0") + "% (" + reading.Kind + ")";
                    }
                }
                if (invalidLiquidReadings > 0) summary += "\n  Incomplete: invalid/unavailable liquid readings skipped.";
                return summary;
            }
        }
        public string LiveAudioSummary
        {
            get
            {
                var current = !hasReadFrame ? "not sampled" : lastFrame.Sound > 0f ? "detected" : "none";
                if (lastHeardAudio == null) return "AUDIO (LATEST SAMPLE): " + current + "\nNo external audio detected yet.";
                var age = Mathf.Max(0f, Time.realtimeSinceStartup - lastHeardAudioTime);
                return "AUDIO (LATEST SAMPLE): " + current + "\nLAST HEARD: " + age.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                    " seconds ago (real time)\n  " + lastHeardAudio.Replace(" d=", "\n  distance at detection=").Replace(" signal=", "\n  signal at detection=").Replace(" volume=", "\n  volume at detection=");
            }
        }
        public string LiveLimbSummary
        {
            get
            {
                var summary = "LIMBS: total=" + LimbCount + " driveable=" + DrivenLimbCount + "/" + LimbCount + " lost=" + LostLimbCount + "/" + LimbCount + " submitted=" + appliedLimbCount + " walk-request=" + appliedWalk.ToString("0.00") +
                    "\nJoint target limit=" + (jointSpeedValid ? jointSpeedLimit.ToString("0.0") + " deg/s" : "invalid (stopped)") + "; native walking gate=0.50.";
                if (IsTerminal)
                {
                    return summary + "\nCONTROL: stopped (" + (IsBrainDead ? "brain dead" : "dead") + "; per-limb capability suppressed)";
                }

                return summary + "\nEXCLUDED:\n  " + ExcludedLimbSummary.Replace(",", "\n  ");
            }
        }
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
        private int LostLimbCount
        {
            get
            {
                var count = 0;
                foreach (var limb in limbs)
                {
                    if (IsLostLimb(limb)) count++;
                }

                return count;
            }
        }
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
        private string SensorSummary { get { return "health=" + (lastFrame.HealthValid ? lastFrame.Health.ToString("0.00") : "unknown") + " damage=" + (lastFrame.DamageValid ? lastFrame.Damage.ToString("0.00") : "unknown") + " pain=" + lastFrame.Pain.ToString("0.00") + " fire=" + lastFrame.Fire.ToString("0.00") + " shock=" + lastFrame.Shock.ToString("0.00") + " oxygen=" + (lastFrame.OxygenValid ? lastFrame.Oxygen.ToString("0.00") : "unknown") + " nearby=" + lastFrame.Nearby.ToString("0.00") + " liquid=" + lastFrame.LiquidExposure.ToString("0.00"); } }

        public PeoplePlaygroundPersonAdapter(GameObject root, float visionRadius, Action<float> reportCollision, Action<float> reportProjectile = null)
        {
            this.root = root;
            this.visionRadius = IsFinite(visionRadius) ? Mathf.Clamp(visionRadius, 1f, 30f) : 8f;
            this.reportCollision = reportCollision;
            this.reportProjectile = reportProjectile;
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
            AttachProbe(limb.gameObject, root.transform, reportCollision, reportProjectile);
        }

        private void AttachProbe(GameObject limbObject, Transform ownerRoot, Action<float> reportCollision, Action<float> reportProjectile)
        {
            var probe = limbObject.GetComponent<PersonConnectomeLimbProbe>() ?? limbObject.AddComponent<PersonConnectomeLimbProbe>();

            probe.OwnerRoot = ownerRoot;
            probe.IsOwned = IsOwnTransform;
            probe.Report = reportCollision;
            probe.ReportProjectile = reportProjectile;
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

            if (!lastFrame.ConsciousnessValid)
            {
                return "DATA LIMITED";
            }

            if (lastFrame.Consciousness <= .8f)
            {
                return "UNCONSCIOUS";
            }

            if (lastFrame.LiquidSedation > .05f)
            {
                return "SEDATIVE EXPOSURE";
            }

            if (lastFrame.BrainDamage > .05f)
            {
                return "BRAIN INJURED";
            }

            if (LiveThreat > .05f)
            {
                return "THREAT";
            }

            if (lastFrame.Sound > .05f || lastFrame.Impact > ContactImpactAlertThreshold)
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
            if (lastFrame.AcidExposure > .05f) return new LiveReading("ACID", lastFrame.AcidExposure);
            if (lastFrame.Infection > .05f) return new LiveReading("NATIVE ZOMBIE STATE", lastFrame.Infection);
            if (lastFrame.LiquidHazard > .05f) return new LiveReading("HAZARDOUS LIQUID", lastFrame.LiquidHazard);
            var fire = Mathf.Max(lastFrame.Fire, lastFrame.Lava);
            if (fire > .05f) return new LiveReading("FIRE/LAVA", fire);
            if (lastFrame.SubmergedHypoxia > .05f) return new LiveReading("SUBMERGED HYPOXIA", lastFrame.SubmergedHypoxia);
            if (lastFrame.OxygenValid && lastFrame.Oxygen < .95f) return new LiveReading("LOW OXYGEN", 1f - lastFrame.Oxygen);
            if (lastFrame.AmbientHeat > .05f) return new LiveReading("AMBIENT HEAT", lastFrame.AmbientHeat);
            if (lastFrame.AmbientCold > .05f) return new LiveReading("AMBIENT COLD", lastFrame.AmbientCold);
            if (lastFrame.Heat > .05f) return new LiveReading("HEAT", lastFrame.Heat);
            if (lastFrame.Cold > .05f) return new LiveReading("COLD", lastFrame.Cold);
            var shock = Mathf.Max(lastFrame.Shock, lastFrame.Charge);
            if (shock > .05f) return new LiveReading("SHOCK", shock);
            var injury = Mathf.Max(lastFrame.Pain, Mathf.Max(lastFrame.Damage, lastFrame.Bleeding));
            if (injury > .05f) return new LiveReading("PAIN/INJURY", injury);
            if (lastFrame.LiquidSedation > .05f) return new LiveReading("SEDATIVE EXPOSURE", lastFrame.LiquidSedation);
            if (lastFrame.Adrenaline > .05f) return new LiveReading("NATIVE ADRENALINE", lastFrame.Adrenaline);
            if (lastFrame.LiquidStimulation > .05f) return new LiveReading("STIMULANT EXPOSURE", lastFrame.LiquidStimulation);
            if (lastFrame.LiquidHealing > .05f) return new LiveReading("RESTORATIVE EXPOSURE", lastFrame.LiquidHealing);
            if (lastFrame.Fall > .05f) return new LiveReading("FALLING", lastFrame.Fall);
            if (lastFrame.Projectile > .05f) return new LiveReading("PROJECTILE", lastFrame.Projectile);
            if (lastFrame.Sound > .05f) return new LiveReading("OBJECT AUDIO", lastFrame.Sound);
            if (lastFrame.Vision > .05f) return new LiveReading("VISION", lastFrame.Vision);
            if (lastFrame.Impact > ContactImpactAlertThreshold) return new LiveReading("CONTACT IMPACT", lastFrame.Impact);
            if (lastFrame.Nearby > .05f) return new LiveReading("NEARBY", lastFrame.Nearby);
            var contact = Mathf.Max(lastFrame.Touch, lastFrame.PhysicalContact);
            if (contact > .05f) return new LiveReading("CONTACT", contact);
            if (lastFrame.UnderWater > .05f) return new LiveReading("UNDERWATER", lastFrame.UnderWater);
            if (lastFrame.LiquidWater > .05f) return new LiveReading("INTERNAL WATER", lastFrame.LiquidWater);
            if (lastFrame.LiquidExposure > .05f) return new LiveReading("LIQUID EXPOSURE", lastFrame.LiquidExposure);
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
            return Mathf.Clamp01(lastFrame.Pain + lastFrame.Damage + lastFrame.Bleeding + lastFrame.Fire + lastFrame.Heat + lastFrame.Cold + lastFrame.AmbientHeat + lastFrame.AmbientCold + lastFrame.Shock + lastFrame.SubmergedHypoxia + lastFrame.AcidExposure + lastFrame.LiquidHazard + lastFrame.Lava + lastFrame.BrainDamage + lastFrame.Seizure + lastFrame.Frozen + lastFrame.LimbLoss + lastFrame.Breakage + lastFrame.Disconnected + lastFrame.Fall + lastFrame.Projectile);
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
            var signal = Unit(magnitude / 20f);
            collision = Mathf.Max(collision, signal);
            vibration = Mathf.Max(vibration, signal);
        }

        public void RegisterProjectile(float magnitude)
        {
            projectile = Mathf.Max(projectile, Unit(magnitude / 20f));
        }

        public SensoryFrame Read()
        {
            liquidReadings.Clear();
            readableLiquidContainers = 0;
            invalidLiquidReadings = 0;
            RefreshLimbs();
            if (!IsUsable)
            {
                lastFrame = new SensoryFrame();
                hasReadFrame = true;
                return lastFrame;
            }

            var frame = ReadPersonState();
            audioSourceSummary = "none";
            visionTargetSummary = "none";
            var healthSum = 0f;
            var healthCount = 0;
            var trackedLimbCount = 0;
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                trackedLimbCount++;
                var health = ReadLimb(ref frame, limb, out var healthValid);
                if (healthValid)
                {
                    healthSum += health;
                    healthCount++;
                }
            }

            var lostLimbCount = 0;
            foreach (var limb in limbs)
            {
                if (IsLostLimb(limb)) lostLimbCount++;
            }

            frame.LimbLoss = trackedLimbCount == 0 ? 0f : (float)lostLimbCount / trackedLimbCount;
            frame.DamageValid = healthCount > 0;
            frame.Damage = frame.DamageValid ? 1f - healthSum / healthCount : 0f;
            frame.Fall = ReadFalling(frame.Touch > .05f || person.IsTouchingFloor);
            frame.Proprioception = Mathf.Clamp01(frame.Velocity * .35f + frame.Rotation * .25f + frame.Balance * .25f + frame.JointStress * .15f);
            ApplyCollision(ref frame);
            ReadNearby(ref frame);
            // Display history only. The next sensory frame still contains zero
            // sound as soon as current playback is absent or filtered out.
            if (frame.Sound > 0f)
            {
                lastHeardAudio = audioSourceSummary;
                lastHeardAudioTime = Time.realtimeSinceStartup;
            }
            frame.BrainDead = person.Braindead;
            frame.HealthValid = IsFinite(person.AverageHealth);
            frame.Alive = frame.HealthValid && !person.Braindead && !HasZeroHealth(person.AverageHealth);
            frame.SubmergedHypoxia = frame.OxygenValid ? frame.UnderWater * (1f - frame.Oxygen) : 0f;
            lastFrame = frame;
            hasReadFrame = true;
            return frame;
        }

        private float ReadFalling(bool floorContact)
        {
            if (floorContact || root == null)
            {
                return 0f;
            }

            var maximumDownwardSpeed = 0f;
            foreach (var limb in limbs)
            {
                var body = limb == null || limb.PhysicalBehaviour == null ? null : limb.PhysicalBehaviour.rigidbody;
                if (body == null || !IsFinite(body.velocity.y))
                {
                    continue;
                }

                maximumDownwardSpeed = Mathf.Max(maximumDownwardSpeed, -body.velocity.y);
            }

            return Mathf.Clamp01(maximumDownwardSpeed / 12f);
        }

        public void Apply(MotorCommand command, bool chemistry, float jointSpeedDegreesPerSecond = 30f, float walkingRequestGain = 2f)
        {
            hasAppliedControl = true;
            appliedLimbCount = 0;
            jointSpeedValid = IsFinite(jointSpeedDegreesPerSecond);
            jointSpeedLimit = jointSpeedValid ? Mathf.Clamp(jointSpeedDegreesPerSecond, 0f, 120f) : 0f;
            if (!IsUsable || !hasReadFrame || !lastFrame.Alive || !lastFrame.ConsciousnessValid || lastFrame.Consciousness <= .8f ||
                !IsFinite(command.Freeze) || command.Freeze > .5f)
            {
                Stop();
                return;
            }

            // Native walking selects a pose only at |request| >= .5. Convert
            // neural amplitude explicitly, retaining zero and its sign and
            // capping the result at the ordinary unit walking request.
            var walkGain = IsFinite(walkingRequestGain) ? Mathf.Clamp(walkingRequestGain, 0f, 4f) : 0f;
            var walk = command.Walk * walkGain * (1f - Unit(command.Avoid) * .25f);
            ApplyWalking(walk);
            foreach (var controller in limbControllers)
            {
                if (controller.Apply(command, jointSpeedDegreesPerSecond)) appliedLimbCount++;
                controller.ApplyChemistry(command, chemistry);
            }

            if (chemistry)
            {
                var adrenalineChange = Unit(command.Stimulate) * .05f - Unit(command.Calm) * .05f;
                if (adrenalineChange != 0f && IsFinite(person.AdrenalineLevel))
                    // Native Update clamps adrenaline to 0..20; only its neural input is unit-clamped.
                    person.AdrenalineLevel = Mathf.Clamp(person.AdrenalineLevel + adrenalineChange, 0f, 20f);
            }
        }

        public void Suspend()
        {
            Stop();
            hasReadFrame = false;
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
                if (probe != null && probe.OwnerRoot == root.transform)
                {
                    probe.Report = null;
                    probe.ReportProjectile = null;
                    probe.IsOwned = null;
                }
            }
        }

        private SensoryFrame ReadPersonState()
        {
            nativeAdrenaline = person.AdrenalineLevel;
            var frame = new SensoryFrame
            {
                Pain = Unit(person.PainLevel),
                Shock = Unit(person.ShockLevel),
                ConsciousnessValid = IsFinite(person.Consciousness),
                Consciousness = Unit(person.Consciousness),
                Adrenaline = Unit(person.AdrenalineLevel),
                OxygenValid = IsFinite(person.OxygenLevel),
                Oxygen = Unit(person.OxygenLevel),
                Health = Unit(person.AverageHealth),
                Fire = Unit(person.AverageFireIntensity),
                Wetness = Unit(person.AverageWetness),
                Velocity = Unit(person.AverageSpeed / 10f),
                Touch = person.IsTouchingFloor ? 1f : 0f,
                Light = Unit(RenderSettings.ambientLight.grayscale),
                LightValid = IsFinite(RenderSettings.ambientLight.grayscale),
                Rotation = Unit(Mathf.Abs(person.AngleOffset) / 180f),
                TiltValid = IsFinite(person.AngleOffset),
                SignedTilt = IsFinite(person.AngleOffset) ? Mathf.Clamp(person.AngleOffset / 180f, -1f, 1f) : 0f,
                Balance = Unit(Mathf.Abs(person.BalanceOffset) / 10f),
                Heartbeat = Unit(person.Heartbeat),
                BrainDamage = person.BrainDamaged ? 1f : 0f,
                Seizure = Unit(person.SeizureTime / 5f),
                Vitality = 0f,
                Circulation = 0f
            };
            var anchor = FindStatusAnchor();
            var body = anchor == null ? null : anchor.GetComponent<Rigidbody2D>();
            frame.VelocityValid = body != null && IsFinite(body.velocity.x) && IsFinite(body.velocity.y);
            if (frame.VelocityValid)
            {
                frame.VelocityX = Mathf.Clamp(body.velocity.x / 10f, -1f, 1f);
                frame.VelocityY = Mathf.Clamp(body.velocity.y / 10f, -1f, 1f);
            }
            frame.BrainDamage = Mathf.Max(frame.BrainDamage, person.Braindead ? 1f : 0f);
            if (IsFinite(person.BrainDamagedTime))
            {
                frame.BrainDamage = Mathf.Max(frame.BrainDamage, Unit(person.BrainDamagedTime / 5f));
            }
            if (!frame.ConsciousnessValid)
            {
                frame.Consciousness = 0f;
            }

            frame.Unconscious = frame.ConsciousnessValid ? 1f - frame.Consciousness : 0f;
            return frame;
        }

        private static bool HasZeroHealth(float averageHealth)
        {
            return !float.IsNaN(averageHealth) && !float.IsInfinity(averageHealth) && averageHealth <= DeathHealthThreshold;
        }

        private float ReadLimb(ref SensoryFrame frame, LimbBehaviour limb, out bool healthValid)
        {
            var joint = limb.Joint;
            if (joint != null && joint.transform == limb.transform && joint.connectedBody != null && !IsLostLimb(limb) && limb.CirculationBehaviour != null && !limb.CirculationBehaviour.IsDisconnected && IsFinite(joint.jointAngle) && IsFinite(joint.jointSpeed))
            {
                frame.JointSensingValid = true;
                frame.JointPosition = Mathf.Max(frame.JointPosition, Unit(Mathf.Abs(joint.jointAngle) / 180f));
                frame.JointMotion = Mathf.Max(frame.JointMotion, Unit(Mathf.Abs(joint.jointSpeed) / 180f));
                if (IsFinite(limb.JointStress))
                {
                    frame.NeuralJointLoad = Mathf.Max(frame.NeuralJointLoad, Unit(limb.JointStress / 100f));
                }
            }
            healthValid = IsFinite(limb.Health) && IsFinite(limb.InitialHealth) && limb.InitialHealth > 0f;
            var health = healthValid ? Unit(limb.Health / Mathf.Max(1f, limb.InitialHealth)) : 0f;
            frame.Breakage = Mathf.Max(frame.Breakage, limb.Broken || limb.CurrentlyShattered != 0 ? 1f : 0f);
            frame.JointStress = Mathf.Max(frame.JointStress, Unit(limb.JointStress / 100f));
            frame.Heat = Mathf.Max(frame.Heat, TemperatureHeat(limb.BodyTemperature));
            frame.Heat = Mathf.Max(frame.Heat, TemperatureHeat(limb.InternalTemperature));
            frame.Cold = Mathf.Max(frame.Cold, TemperatureCold(limb.BodyTemperature));
            frame.Cold = Mathf.Max(frame.Cold, TemperatureCold(limb.InternalTemperature));
            frame.Frozen = Mathf.Max(frame.Frozen, limb.Frozen ? 1f : 0f);
            frame.Paralysis = Mathf.Max(frame.Paralysis, limb.IsParalysed ? 1f : 0f);
            frame.Numbness = Mathf.Max(frame.Numbness, Unit(limb.Numbness));
            if (healthValid)
            {
                var vitality = ReadVitality(limb, health);
                frame.Vitality = frame.VitalityValid ? Mathf.Min(frame.Vitality, vitality) : vitality;
                frame.VitalityValid = true;
            }
            frame.LungDamage = Mathf.Max(frame.LungDamage, limb.HasLungs && limb.LungsPunctured ? 1f : 0f);
            frame.Touch = Mathf.Max(frame.Touch, limb.IsOnFloor ? 1f : 0f);
            ReadCirculation(ref frame, limb.CirculationBehaviour);
            ReadPhysical(ref frame, limb.PhysicalBehaviour);
            frame.Infection = Mathf.Max(frame.Infection, limb.IsZombie ? 1f : 0f);
            return health;
        }

        private static bool IsLostLimb(LimbBehaviour limb)
        {
            return limb != null && (limb.IsDismembered || (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.isDisintegrated));
        }

        private void ReadCirculation(ref SensoryFrame frame, CirculationBehaviour circulation)
        {
            if (circulation == null)
            {
                invalidLiquidReadings++;
                return;
            }

            frame.Bleeding = Mathf.Max(frame.Bleeding, Unit(circulation.BleedingRate));
            frame.InternalBleeding = Mathf.Max(frame.InternalBleeding, Unit(circulation.InternalBleedingIntensity));
            frame.Heartbeat = Mathf.Max(frame.Heartbeat, Unit(circulation.GetHeartRate() / 120f));
            // HasBloodFlow is an observed native state: false is a known zero even if
            // the optional flow scalar is unavailable. A non-finite positive-flow
            // scalar remains unknown, rather than becoming a fabricated deficit.
            var circulationValueKnown = !circulation.HasBloodFlow || IsFinite(circulation.BloodFlow);
            if (circulationValueKnown)
            {
                var circulationValue = circulation.HasBloodFlow ? Unit(circulation.BloodFlow) : 0f;
                frame.Circulation = frame.CirculationValid ? Mathf.Min(frame.Circulation, circulationValue) : circulationValue;
                frame.CirculationValid = true;
            }
            frame.Disconnected = Mathf.Max(frame.Disconnected, circulation.IsDisconnected || !circulation.HasCirculation ? 1f : 0f);
            frame.Wounds = Mathf.Max(frame.Wounds, Unit((circulation.StabWoundCount + circulation.GunshotWoundCount + circulation.BleedingPointCount) / 8f));
            var bloodDeficit = ReadBloodDeficit(circulation, out var bloodValid);
            if (bloodValid)
            {
                frame.Blood = frame.BloodValid ? Mathf.Max(frame.Blood, bloodDeficit) : bloodDeficit;
                frame.BloodValid = true;
            }
            ReadLiquidIdentities(ref frame, circulation);
        }

        private static float ReadVitality(LimbBehaviour limb, float health)
        {
            return IsFinite(limb.Vitality) && limb.Vitality > .001f ? Unit(limb.Vitality) : health;
        }

        private float ReadBloodDeficit(CirculationBehaviour circulation, out bool valid)
        {
            var amount = circulation.GetAmountOfBlood();
            if (!IsFinite(amount))
            {
                valid = false;
                return 0f;
            }

            if (amount <= .001f)
            {
                valid = bloodBaselines.ContainsKey(circulation);
                return valid ? 1f : 0f;
            }

            if (!bloodBaselines.TryGetValue(circulation, out var baseline) || amount > baseline)
            {
                bloodBaselines[circulation] = amount;
                valid = true;
                return 0f;
            }

            valid = true;
            return 1f - Unit(amount / baseline);
        }

        private enum LiquidKind
        {
            UnknownExposure, Blood, Hazard, Sedative, Stimulant, Restorative, OtherExposure
        }

        private struct LiquidReading
        {
            public float Fraction;
            public LiquidKind Kind;
        }

        // Exact stock IDs from Global.Awake in the installed 1.27.17 assembly.
        // These are engineered exposure routes, not measurements of an effect.
        private static LiquidKind ClassifyLiquid(string identity)
        {
            switch (identity)
            {
                case "BLOOD": return LiquidKind.Blood;
                case "GORSE BLOOD":
                case "OIL":
                case "NITRO":
                case "TRITIUM":
                case "COOLANT":
                case "REANIMATION AGENT":
                case "ACID":
                case "BONE EATING POISON":
                case "INSTANT DEATH POISON":
                case "FREEZE POISON":
                case "OSTEOMORPHOSIS AGENT":
                case "VESTIBULAR POISON":
                case "MUSCLE POISON":
                case "NUMBING POISON":
                case "EXPLOSION POISON":
                case "CRUSHING POISON":
                case "DISTORTION POISON":
                case "CIRCULATION POISON":
                case "COMBUSTION AGENT":
                case "TISSUE DECONSTRUCTION AGENT": return LiquidKind.Hazard;
                case "KNOCKOUT POISON": return LiquidKind.Sedative;
                case "ADRENALINE": return LiquidKind.Stimulant;
                case "COAGULATION SERUM":
                case "LIFE SERUM":
                case "MENDING SERUM":
                case "IMMORTALITY SERUM":
                case "REGENERATION SERUM": return LiquidKind.Restorative;
                case "ULTRA STRENGTH SERUM":
                case "DURABILITY SERUM":
                case "ENHANCING SERUM":
                case "EXOTIC LIQUID":
                case "INERT LIQUID":
                case "BEVERAGE M04":
                case "WATER BREATHING SERUM":
                case "PAIN KILLER":
                case "INERT PINK LIQUID":
                case "MIRRORISING AGENT":
                case "TRANSPARENCY AGENT":
                case "MASS AGENT":
                case "DEBUG LIQUID 001": return LiquidKind.OtherExposure;
                default: return LiquidKind.UnknownExposure;
            }
        }

        private void ReadLiquidIdentities(ref SensoryFrame frame, CirculationBehaviour circulation)
        {
            var total = circulation.TotalLiquidAmount;
            if (circulation.LiquidDistribution == null || !IsFinite(total) || total < 0f)
            {
                invalidLiquidReadings++;
                return;
            }
            readableLiquidContainers++;
            foreach (var entry in circulation.LiquidDistribution)
            {
                if (entry.Key == null || entry.Value == null || !IsFinite(entry.Value.Raw) || entry.Value.Raw < 0f)
                {
                    invalidLiquidReadings++;
                    continue;
                }
                if (entry.Value.Raw == 0f) continue;
                if (total <= 0f)
                {
                    invalidLiquidReadings++;
                    continue;
                }
                var amount = entry.Value.Raw >= total ? 1f : entry.Value.Raw / total;
                if (amount <= 0f) continue;
                var identity = Liquid.GetIdentity(entry.Key);
                var kind = ClassifyLiquid(identity);
                // A display name is useful text but cannot grant a known effect.
                var name = !String.IsNullOrEmpty(identity) ? identity :
                    (entry.Key.GetDisplayName() ?? "Unnamed liquid") + " (unregistered)";
                if (!liquidReadings.TryGetValue(name, out var reading) || amount > reading.Fraction)
                {
                    liquidReadings[name] = new LiquidReading { Fraction = amount, Kind = kind };
                }
                if (kind != LiquidKind.Blood) frame.LiquidExposure = Mathf.Max(frame.LiquidExposure, amount);
                switch (kind)
                {
                    case LiquidKind.Hazard: frame.LiquidHazard = Mathf.Max(frame.LiquidHazard, amount); break;
                    case LiquidKind.Sedative: frame.LiquidSedation = Mathf.Max(frame.LiquidSedation, amount); break;
                    case LiquidKind.Stimulant: frame.LiquidStimulation = Mathf.Max(frame.LiquidStimulation, amount); break;
                    case LiquidKind.Restorative: frame.LiquidHealing = Mathf.Max(frame.LiquidHealing, amount); break;
                }
            }
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

        private static float AmbientTemperatureHeat(float temperature)
        {
            return Unit((temperature - 30f) / 70f);
        }

        private static float AmbientTemperatureCold(float temperature)
        {
            return Unit((10f - temperature) / 10f);
        }

        private void ApplyCollision(ref SensoryFrame frame)
        {
            frame.Touch = Mathf.Max(frame.Touch, collision);
            frame.Impact = Mathf.Max(frame.Impact, collision);
            frame.Vibration = Mathf.Max(Mathf.Max(vibration, frame.Impact * .6f), Mathf.Max(frame.PhysicalContact * .2f, frame.Sliding * .25f));
            frame.Projectile = projectile;
            collision *= .65f;
            vibration *= .82f;
            projectile *= .35f;
        }

        private void ApplyWalking(float walk)
        {
            appliedWalk = IsFinite(walk) ? Mathf.Clamp(walk, -1f, 1f) : 0f;
            if (person != null) person.DesiredWalkingDirection = appliedWalk;
        }

        internal bool IsOwnTransform(Transform candidate)
        {
            if (candidate == null || root == null)
            {
                return false;
            }

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
            var origin = (Vector2)FindStatusAnchor().position;
            ReadAmbientTemperature(ref f, origin);
            Collider2D closestCollider = null;
            PhysicalBehaviour closestPhysical = null;
            var closestPoint = origin;
            var hitCount = Physics2D.OverlapCircleNonAlloc(origin, visionRadius, nearbyColliders);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = nearbyColliders[i];
                if (hit == null || IsOwnTransform(hit.transform))
                {
                    continue;
                }

                var delta = hit.ClosestPoint(origin) - origin;
                var distance = delta.magnitude;
                if (!IsFinite(distance) || !IsFinite(delta.x)) continue;

                var acidPool = hit.GetComponentInParent<AcidPoolBehaviour>();
                if (acidPool != null && !IsOwnTransform(acidPool.transform) && distance <= .05f)
                {
                    var acid = Mathf.Max(Unit(acidPool.AcidProgress), Unit(acidPool.PainIntensity));
                    f.AcidExposure = Mathf.Max(f.AcidExposure, acid);
                }

                var lava = hit.GetComponentInParent<LavaBehaviour>();
                if (lava != null && !IsOwnTransform(lava.transform))
                {
                    var distanceSignal = Mathf.Clamp01(1f - distance / visionRadius);
                    f.Lava = Mathf.Max(f.Lava, distanceSignal);
                    f.AmbientHeat = Mathf.Max(f.AmbientHeat, AmbientTemperatureHeat(lava.LavaTemperature) * distanceSignal);
                }

                var physical = hit.GetComponentInParent<PhysicalBehaviour>();
                if (physical == null)
                {
                    continue;
                }

                ReadExternalTemperature(ref f, physical, distance);
                ReadExternalSound(ref f, physical, origin);
                if (distance < closest)
                {
                    closest = distance;
                    f.Nearby = Mathf.Clamp01(1f - distance / visionRadius);
                    f.NearbyDirection = Mathf.Abs(delta.x) < .001f ? 0f : Mathf.Sign(delta.x);
                    closestCollider = hit;
                    closestPhysical = physical;
                    closestPoint = hit.ClosestPoint(origin);
                }
            }

            if (closestCollider != null && Physics2D.Linecast(origin, closestPoint).collider == closestCollider)
            {
                f.Vision = Mathf.Clamp01(f.Nearby * f.Light);
                f.VisionDirection = WorldHorizontalBearing(closestPoint - origin, out var visionDirectionValid);
                f.VisionDirectionValid = visionDirectionValid;
                visionTargetSummary = ClassifyVisualTarget(closestPhysical, closestCollider);
                var targetBody = closestPhysical == null ? null : closestPhysical.rigidbody;
                var anchorBody = FindStatusAnchor() == null ? null : FindStatusAnchor().GetComponent<Rigidbody2D>();
                if (f.Vision > 0f && targetBody != null && anchorBody != null &&
                    IsFinite(targetBody.velocity.x) && IsFinite(targetBody.velocity.y) && IsFinite(anchorBody.velocity.x) && IsFinite(anchorBody.velocity.y))
                {
                    var delta = closestPoint - origin;
                    var distance = delta.magnitude;
                    if (IsFinite(distance) && distance > .001f && IsFinite(delta.x) && IsFinite(delta.y))
                    {
                        var closing = -((targetBody.velocity.x - anchorBody.velocity.x) * delta.x + (targetBody.velocity.y - anchorBody.velocity.y) * delta.y) / distance;
                        f.VisualApproach = Unit(closing / 10f) * f.Vision;
                    }
                }
                if (PersonConnectomeProjectileDetection.IsMovingProjectile(closestCollider, closestPhysical))
                {
                    f.Projectile = Mathf.Max(f.Projectile, f.Vision);
                }
            }
        }

        private string ClassifyVisualTarget(PhysicalBehaviour physical, Collider2D collider)
        {
            if (physical == null)
            {
                return "object";
            }

            if (PersonConnectomeProjectileDetection.IsProjectile(collider))
            {
                return "projectile";
            }

            if (physical.GetComponentInParent<PersonBehaviour>() != null)
            {
                return "person";
            }

            if (physical.GetComponentInParent<AcidPoolBehaviour>() != null)
            {
                return "acid/hazard";
            }

            return "object";
        }

        private void ReadExternalTemperature(ref SensoryFrame frame, PhysicalBehaviour physical, float distance)
        {
            if (physical == null || IsOwnPhysical(physical) || physical.rigidbody == null || physical.rigidbody.bodyType == RigidbodyType2D.Static)
            {
                return;
            }

            var distanceSignal = Mathf.Clamp01(1f - distance / visionRadius);
            frame.AmbientHeat = Mathf.Max(frame.AmbientHeat, AmbientTemperatureHeat(physical.Temperature) * distanceSignal);
            frame.AmbientCold = Mathf.Max(frame.AmbientCold, AmbientTemperatureCold(physical.Temperature) * distanceSignal);
        }

        private static void ReadAmbientTemperature(ref SensoryFrame frame, Vector2 origin)
        {
            var grid = AmbientTemperatureGridBehaviour.Instance;
            if (grid == null)
            {
                return;
            }

            var temperature = grid.GetTemperatureAtPoint(origin);
            if (!IsFinite(temperature))
            {
                return;
            }

            frame.AmbientHeat = Mathf.Max(frame.AmbientHeat, AmbientTemperatureHeat(temperature));
            frame.AmbientCold = Mathf.Max(frame.AmbientCold, AmbientTemperatureCold(temperature));
        }

        private void ReadExternalSound(ref SensoryFrame frame, PhysicalBehaviour physical, Vector2 origin)
        {
            var audio = physical == null ? null : physical.MainAudioSource;
            var sourceDelta = audio == null ? new Vector2() : (Vector2)audio.transform.position - origin;
            var sourceDistance = sourceDelta.magnitude;
            if (physical == null || IsOwnPhysical(physical) || audio == null || !IsFinite(sourceDistance) || IsLikelySelfRootAudio(physical, audio, sourceDistance) ||
                IsOwnTransform(audio.transform) ||
                !audio.isPlaying || audio.mute || !audio.isActiveAndEnabled)
            {
                return;
            }

            var distanceSignal = Mathf.Clamp01(1f - sourceDistance / visionRadius);
            var signal = Unit(audio.volume) * distanceSignal;
            if (signal > frame.Sound)
            {
                frame.Sound = signal;
                frame.SoundDirection = WorldHorizontalBearing(sourceDelta, out var soundDirectionValid);
                frame.SoundDirectionValid = soundDirectionValid;
                var sourceKind = physical.GetComponentInParent<PersonBehaviour>() == null ? "object" : "person";
                audioSourceSummary = "external " + sourceKind + " " + physical.name + "/" + audio.name + " d=" + sourceDistance.ToString("0.00") + " signal=" + signal.ToString("0.00") + " volume=" + Unit(audio.volume).ToString("0.00");
            }
        }

        private static float WorldHorizontalBearing(Vector2 delta, out bool valid)
        {
            var distance = delta.magnitude;
            valid = IsFinite(delta.x) && IsFinite(delta.y) && IsFinite(distance) && distance > .001f;
            return valid ? Mathf.Clamp(delta.x / distance, -1f, 1f) : 0f;
        }

        private bool IsOwnPhysical(PhysicalBehaviour physical)
        {
            if (physical == null)
            {
                return true;
            }

            if (IsOwnTransform(physical.transform) || physical.GetComponent<PersonBehaviour>() == person || physical.GetComponentInParent<PersonBehaviour>() == person)
            {
                return true;
            }

            var limb = physical.GetComponentInParent<LimbBehaviour>();
            if (limb != null && limb.Person == person)
            {
                return true;
            }

            foreach (var ownedLimb in limbs)
            {
                if (ownedLimb != null && ownedLimb.PhysicalBehaviour == physical)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLikelySelfRootAudio(PhysicalBehaviour physical, AudioSource audio, float distance)
        {
            if (!IsRootName(physical.name) || !IsRootName(audio.name))
            {
                return false;
            }

            // Generic Root/Root sources without a PersonBehaviour have ambiguous
            // ownership. Conservatively exclude these possible self-audio artifacts
            // throughout the sensor radius; a name alone cannot prove ownership.
            return physical.GetComponentInParent<PersonBehaviour>() == null && distance <= visionRadius;
        }

        private static bool IsRootName(string value)
        {
            if (value == null || !value.StartsWith("Root", StringComparison.OrdinalIgnoreCase)) return false;
            var index = 4;
            while (index < value.Length)
            {
                if (value[index] == ' ') { index++; continue; }
                if (value[index] != '(') return false;
                var end = value.IndexOf(')', index + 1);
                if (end < 0 || end == index + 1) return false;
                var suffix = value.Substring(index + 1, end - index - 1);
                if (!string.Equals(suffix, "Clone", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var digit in suffix) if (digit < '0' || digit > '9') return false;
                }
                index = end + 1;
            }
            return true;
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
