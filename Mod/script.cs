// Standalone People Playground script. The brain is deliberately separate from
// PeoplePlaygroundPersonAdapter so the bounded control logic has no game types.
using System;
using System.Collections.Generic;
using System.Reflection;
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
                ModAPI.Log("Person Connectome: Human spawnable was not found; active variation was not registered.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                NameOverride = "Person Connectome (Active)",
                DescriptionOverride = "Active bounded connectome control. F8 immediately disables it; F7 toggles diagnostics.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                AfterSpawn = instance =>
                {
                    if (instance != null && instance.GetComponent<PersonConnectomeController>() == null)
                        instance.AddComponent<PersonConnectomeController>();
                }
            });
        }
    }

    public sealed class PersonConnectomeController : MonoBehaviour
    {
        // Configuration is intentionally exposed in the spawned object's inspector.
        public bool ActiveControl = true;
        public bool EnableChemicalOutputs = true;
        [Range(1f, 60f)] public float TickRateHz = 20f;
        [Range(.01f, 1f)] public float Smoothing = .2f;
        [Range(1f, 30f)] public float VisionRadius = 8f;
        public KeyCode EmergencyDisableKey = KeyCode.F8;
        public KeyCode DebugOverlayKey = KeyCode.F7;

        private readonly ConnectomeBrain brain = new ConnectomeBrain();
        private PeoplePlaygroundPersonAdapter adapter;
        private float accumulator;
        private bool emergencyDisabled;
        private bool showDebug;
        private MotorCommand lastCommand;
        private string lastStatus = "starting";

        private void Awake()
        {
            adapter = new PeoplePlaygroundPersonAdapter(gameObject, VisionRadius, RegisterCollision);
            if (!adapter.IsUsable)
            {
                ActiveControl = false;
                lastStatus = "PersonBehaviour unavailable";
                ModAPI.Log("Person Connectome: attachment disabled because PersonBehaviour was unavailable.");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(EmergencyDisableKey))
            {
                emergencyDisabled = true;
                adapter.Stop();
                lastStatus = "EMERGENCY DISABLED";
                ModAPI.Log("Person Connectome: emergency disable latched. Remove/re-spawn the variation to re-arm.");
            }
            if (Input.GetKeyDown(DebugOverlayKey)) showDebug = !showDebug;
        }

        private void FixedUpdate()
        {
            if (adapter == null || emergencyDisabled) return;
            accumulator = Mathf.Min(accumulator + Time.fixedDeltaTime, 4f / Mathf.Max(1f, TickRateHz));
            var interval = 1f / Mathf.Clamp(TickRateHz, 1f, 60f);
            var steps = 0;
            while (accumulator >= interval && steps++ < 4)
            {
                accumulator -= interval;
                var sensory = adapter.Read();
                lastCommand = brain.Step(sensory, interval).Smooth(lastCommand, Smoothing);
                if (ActiveControl) adapter.Apply(lastCommand, EnableChemicalOutputs);
                lastStatus = ActiveControl ? adapter.CapabilitySummary : "safe mode (active control off)";
            }
        }

        private void RegisterCollision(float magnitude) => adapter?.RegisterCollision(magnitude);

        private void OnGUI()
        {
            if (!showDebug) return;
            GUI.Box(new Rect(12, 12, 420, 115), "Person Connectome");
            GUI.Label(new Rect(22, 37, 400, 20), emergencyDisabled ? "EMERGENCY DISABLED (remove/re-spawn to re-arm)" : (ActiveControl ? "ACTIVE CONTROL" : "SAFE MODE"));
            GUI.Label(new Rect(22, 57, 400, 20), "F8 emergency stop | F7 diagnostics | " + lastStatus);
            GUI.Label(new Rect(22, 77, 400, 20), string.Format("walk {0:0.00}  reach {1:0.00}  avoid {2:0.00}  heal {3:0.00}", lastCommand.Walk, lastCommand.ReachGrab, lastCommand.Avoid, lastCommand.Heal));
            GUI.Label(new Rect(22, 97, 400, 20), "Chemistry output: " + (EnableChemicalOutputs ? "enabled" : "off (motor control remains active)"));
        }
    }

    // Collision callbacks arrive on limb GameObjects, so probes forward a bounded
    // signal to the root controller rather than assuming root collision messages.
    public sealed class PersonConnectomeLimbProbe : MonoBehaviour
    {
        public Action<float> Report;
        private void OnCollisionEnter2D(Collision2D collision) { if (Report != null && collision != null) Report(collision.relativeVelocity.magnitude); }
        private void OnCollisionStay2D(Collision2D collision) { if (Report != null && collision != null) Report(collision.relativeVelocity.magnitude * .25f); }
    }

    internal struct SensoryFrame
    {
        public float Pain, Damage, Bleeding, Health, Fire, Heat, Cold, Shock, Oxygen, Drowning;
        public float Wetness, Blood, Poison, Acid, Sedation, Healing, Stimulation, Infection, Charge;
        public float Nearby, NearbyDirection, Light, Touch, Vibration, Sound, LimbLoss, JointStress;
        public float Consciousness, Adrenaline, Velocity, Rotation, Unconscious, UnknownMaterial;
    }

    internal struct MotorCommand
    {
        public float Walk, ReachGrab, Avoid, Freeze, Heal, Stimulate, Calm, Extinguish;
        public MotorCommand Smooth(MotorCommand previous, float smoothing)
        {
            smoothing = Mathf.Clamp01(smoothing);
            Walk = Mathf.Lerp(previous.Walk, Mathf.Clamp(Walk, -1f, 1f), smoothing);
            ReachGrab = Mathf.Lerp(previous.ReachGrab, Mathf.Clamp01(ReachGrab), smoothing);
            Avoid = Mathf.Lerp(previous.Avoid, Mathf.Clamp01(Avoid), smoothing);
            Freeze = Mathf.Lerp(previous.Freeze, Mathf.Clamp01(Freeze), smoothing);
            Heal = Mathf.Lerp(previous.Heal, Mathf.Clamp01(Heal), smoothing);
            Stimulate = Mathf.Lerp(previous.Stimulate, Mathf.Clamp01(Stimulate), smoothing);
            Calm = Mathf.Lerp(previous.Calm, Mathf.Clamp01(Calm), smoothing);
            Extinguish = Mathf.Lerp(previous.Extinguish, Mathf.Clamp01(Extinguish), smoothing);
            return this;
        }
    }

    // Fixed-size leaky integrate-and-fire demonstration. It has no Unity or game API dependency.
    internal sealed class ConnectomeBrain
    {
        private readonly float[] potential = new float[5];
        private readonly int[] refractory = new int[5];
        public MotorCommand Step(SensoryFrame s, float delta)
        {
            var input = new[] { s.Pain + s.Damage + s.Bleeding + s.Fire + s.Shock + s.Drowning + s.Acid + s.Poison, s.Nearby + s.Light + s.Sound + s.Touch, s.Sedation + s.Unconscious, s.Stimulation + s.Adrenaline, s.Healing + s.Health };
            var fired = new bool[5];
            for (var i = 0; i < potential.Length; i++)
            {
                if (refractory[i] > 0) { refractory[i]--; potential[i] = 0f; continue; }
                potential[i] = Mathf.Clamp(potential[i] * .92f + Mathf.Clamp(input[i], -4f, 4f), -8f, 8f);
                if (potential[i] >= 1f) { potential[i] = 0f; refractory[i] = 2; fired[i] = true; }
            }
            var danger = fired[0] || input[0] > .5f;
            return new MotorCommand
            {
                Walk = danger ? -Mathf.Sign(s.NearbyDirection == 0f ? 1f : s.NearbyDirection) : s.Nearby * Mathf.Sign(s.NearbyDirection),
                ReachGrab = fired[1] && !danger ? s.Nearby : 0f,
                Avoid = danger ? 1f : 0f,
                Freeze = Mathf.Max(s.Sedation, s.Unconscious),
                Heal = Mathf.Clamp01(s.Damage + s.Bleeding),
                Stimulate = Mathf.Clamp01(s.Sedation + s.Unconscious),
                Calm = Mathf.Clamp01(s.Shock + s.Pain),
                Extinguish = s.Fire
            };
        }
    }

    internal sealed class PeoplePlaygroundPersonAdapter
    {
        private readonly GameObject root;
        private readonly PersonBehaviour person;
        private readonly LimbBehaviour[] limbs;
        private readonly float visionRadius;
        private float collision;
        private readonly List<string> unknownMaterials = new List<string>();
        public bool IsUsable { get { return person != null && limbs != null && limbs.Length > 0; } }
        public string CapabilitySummary { get { return "walk=" + (person != null) + " limbs=" + (limbs == null ? 0 : limbs.Length) + " grip=optional unknown-materials=" + unknownMaterials.Count; } }

        public PeoplePlaygroundPersonAdapter(GameObject root, float visionRadius, Action<float> reportCollision)
        {
            this.root = root;
            this.visionRadius = Mathf.Clamp(visionRadius, 1f, 30f);
            person = root.GetComponent<PersonBehaviour>();
            limbs = root.GetComponentsInChildren<LimbBehaviour>();
            foreach (var limb in limbs)
            {
                var probe = limb.gameObject.GetComponent<PersonConnectomeLimbProbe>() ?? limb.gameObject.AddComponent<PersonConnectomeLimbProbe>();
                probe.Report = reportCollision;
            }
        }

        public void RegisterCollision(float magnitude) { collision = Mathf.Max(collision, Mathf.Clamp01(magnitude / 20f)); }
        public void Stop() { if (person != null) person.DesiredWalkingDirection = 0f; }

        public SensoryFrame Read()
        {
            var f = new SensoryFrame();
            if (!IsUsable) return f;
            f.Pain = Unit(person.PainLevel); f.Shock = Unit(person.ShockLevel); f.Consciousness = Unit(person.Consciousness); f.Unconscious = 1f - f.Consciousness;
            f.Adrenaline = Unit(person.AdrenalineLevel); f.Oxygen = Unit(person.OxygenLevel); f.Drowning = 1f - f.Oxygen;
            f.Health = Unit(person.AverageHealth); f.Velocity = Unit(person.AverageSpeed / 10f); f.Touch = person.IsTouchingFloor ? 1f : 0f;
            f.Light = Mathf.Clamp01(RenderSettings.ambientLight.grayscale); f.Rotation = Unit(Mathf.Abs(person.AngleOffset) / 180f);
            var healthSum = 0f; var healthCount = 0;
            foreach (var limb in limbs)
            {
                healthSum += Unit(limb.Health / Mathf.Max(1f, limb.InitialHealth)); healthCount++;
                f.LimbLoss = Mathf.Max(f.LimbLoss, limb.IsDismembered ? 1f : 0f);
                f.JointStress = Mathf.Max(f.JointStress, Unit(limb.JointStress / 100f));
                f.Heat = Mathf.Max(f.Heat, Unit((limb.BodyTemperature - 37f) / 80f)); f.Cold = Mathf.Max(f.Cold, Unit((0f - limb.BodyTemperature) / 40f));
                var circulation = limb.CirculationBehaviour;
                if (circulation != null) { f.Bleeding = Mathf.Max(f.Bleeding, Unit(circulation.BleedingRate)); f.Blood = Mathf.Max(f.Blood, 1f - Unit(circulation.GetAmountOfBlood())); }
                var physical = limb.PhysicalBehaviour;
                if (physical != null)
                {
                    f.Fire = Mathf.Max(f.Fire, physical.OnFire ? Unit(physical.BurnIntensity) : 0f); f.Wetness = Mathf.Max(f.Wetness, Unit(physical.Wetness)); f.Charge = Mathf.Max(f.Charge, Unit(Mathf.Abs(physical.Charge)));
                    f.Vibration = Mathf.Max(f.Vibration, physical.rigidbody == null ? 0f : Unit(physical.rigidbody.velocity.magnitude / 10f));
                    f.Sound = Mathf.Max(f.Sound, physical.MainAudioSource != null && physical.MainAudioSource.isPlaying ? 1f : 0f);
                    ReadMaterial(physical.LastKnownFluidIdentity, ref f);
                }
                f.Infection = Mathf.Max(f.Infection, limb.IsZombie ? 1f : 0f);
            }
            f.Damage = healthCount == 0 ? 0f : 1f - healthSum / healthCount;
            f.Touch = Mathf.Max(f.Touch, collision); f.Vibration = Mathf.Max(f.Vibration, collision); collision *= .65f;
            ReadNearby(ref f);
            return f;
        }

        public void Apply(MotorCommand command, bool chemistry)
        {
            if (!IsUsable) return;
            var walk = command.Freeze > .5f ? 0f : command.Walk * (1f - command.Avoid * .25f);
            // These documented APIs form the primary active-control path. A failed
            // optional grip/chemistry capability never disables walking or limbs.
            person.DesiredWalkingDirection = Mathf.Clamp(walk, -1f, 1f);
            foreach (var limb in limbs)
            {
                limb.InfluenceMotorSpeed(Mathf.Clamp(walk, -1f, 1f), .25f);
                var grip = limb.GripBehaviour;
                if (grip != null) TryGrip(grip, command.ReachGrab);
                if (!chemistry) continue;
                if (limb.CirculationBehaviour != null) limb.CirculationBehaviour.BloodRegenerationPerSecond = Mathf.Clamp(command.Heal, 0f, 1f);
                limb.RegenerationSpeed = Mathf.Clamp(command.Heal, 0f, 1f);
                if (limb.PhysicalBehaviour != null && command.Extinguish > .01f) limb.PhysicalBehaviour.BurnIntensity = Mathf.Max(0f, limb.PhysicalBehaviour.BurnIntensity - command.Extinguish * .05f);
            }
            if (chemistry) person.AdrenalineLevel = Mathf.Clamp01(Mathf.Max(person.AdrenalineLevel, command.Stimulate));
        }

        private void ReadNearby(ref SensoryFrame f)
        {
            var closest = float.MaxValue; var origin = (Vector2)root.transform.position;
            foreach (var hit in Physics2D.OverlapCircleAll(origin, visionRadius))
            {
                if (hit == null || hit.transform.IsChildOf(root.transform)) continue;
                var delta = (Vector2)hit.transform.position - origin; var distance = delta.magnitude;
                if (distance < closest) { closest = distance; f.Nearby = 1f - distance / visionRadius; f.NearbyDirection = Mathf.Sign(delta.x); }
            }
        }

        private void ReadMaterial(string identity, ref SensoryFrame f)
        {
            if (string.IsNullOrEmpty(identity)) return;
            var key = identity.ToLowerInvariant();
            if (key.Contains("water")) f.Wetness = 1f;
            else if (key.Contains("blood")) f.Blood = 1f;
            else if (key.Contains("acid") || key.Contains("corros")) f.Acid = 1f;
            else if (key.Contains("poison") || key.Contains("toxin")) f.Poison = 1f;
            else if (key.Contains("anaesth") || key.Contains("sedat")) f.Sedation = 1f;
            else if (key.Contains("adrenal") || key.Contains("stimul")) f.Stimulation = 1f;
            else if (key.Contains("healing") || key.Contains("regen")) f.Healing = 1f;
            else if (!unknownMaterials.Contains(key) && unknownMaterials.Count < 16) unknownMaterials.Add(key);
        }

        private static void TryGrip(GripBehaviour grip, float request)
        {
            try
            {
                if (request < .7f) { grip.DropObject(); return; }
                if (grip.isHolding) return;
                var use = grip.GetType().GetMethod("Use", BindingFlags.Instance | BindingFlags.Public);
                if (use == null || use.GetParameters().Length != 1) return;
                var argumentType = use.GetParameters()[0].ParameterType;
                use.Invoke(grip, new[] { argumentType.IsValueType ? Activator.CreateInstance(argumentType) : null });
            }
            catch (Exception) { /* optional grip capability is intentionally fail-safe */ }
        }

        private static float Unit(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value); }
    }
}
