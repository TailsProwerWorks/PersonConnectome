using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    internal enum FlyHealthRegion { Head, Thorax, Abdomen, LeftWing, RightWing, FrontLeftLeg, FrontRightLeg, MiddleLeftLeg, MiddleRightLeg, HindLeftLeg, HindRightLeg, Count }

    /// <summary>Regional health derived from bound native limbs, with a legacy fallback for unbound prefabs.</summary>
    internal sealed class FlyHealth : MonoBehaviour
    {
        private const int PeripheralRegionCount = 8;
        private readonly float[] regionHealth = new float[(int)FlyHealthRegion.Count];
        private readonly Dictionary<FlyHealthRegion, List<LimbBehaviour>> boundLimbs = new();
        private readonly Dictionary<LimbBehaviour, float> nativeHealthSamples = new();
        private PhysicalBehaviour? physical;
        private FlyExperienceMemory? experienceMemory;
        private bool initialized;
        private float pendingDamageEvent;

        public LimbBehaviour? Head
        {
            get
            {
                if (!boundLimbs.TryGetValue(FlyHealthRegion.Head, out var limbs)) return null;
                var head = limbs.Count > 0 ? limbs[0] : null;
                return head == null ? null : head;
            }
        }

        public bool IsAlive
        {
            get
            {
                if (!initialized) return false;
                RefreshNativeRegions(null);
                return regionHealth[(int)FlyHealthRegion.Head] > 0f && regionHealth[(int)FlyHealthRegion.Thorax] > 0f &&
                    (physical == null || !physical.isDisintegrated) && NativePersonIsAlive();
            }
        }

        public float OverallHealth { get { if (!initialized) return 0f; RefreshNativeRegions(null); return RegionAverage(); } }

        public float MinimumLimbHealth
        {
            get
            {
                if (!initialized) return 0f;
                RefreshNativeRegions(null);
                var minimum = 1f;
                for (var index = (int)FlyHealthRegion.LeftWing; index < regionHealth.Length; index++) minimum = Math.Min(minimum, regionHealth[index]);
                return minimum;
            }
        }

        public string LimbHealthSummary
        {
            get
            {
                if (!initialized) return "unavailable";
                RefreshNativeRegions(null);
                var text = new StringBuilder();
                for (var index = 0; index < regionHealth.Length; index++)
                {
                    if (index > 0) text.Append("  ");
                    text.Append(RegionName((FlyHealthRegion)index));
                    text.Append('=');
                    text.Append(regionHealth[index].ToString("0.00", CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            for (var index = 0; index < regionHealth.Length; index++) regionHealth[index] = 1f;
            physical = gameObject.GetComponent<PhysicalBehaviour>();
            experienceMemory = gameObject.GetComponent<FlyExperienceMemory>();
        }

        /// <summary>Registers the native parts for one visible fly region.</summary>
        public void BindRegion(FlyHealthRegion region, params LimbBehaviour[] limbs)
        {
            if (!initialized) Initialize();
            if ((int)region < 0 || region >= FlyHealthRegion.Count) return;
            if (!boundLimbs.TryGetValue(region, out var registered))
            {
                registered = new List<LimbBehaviour>();
                boundLimbs.Add(region, registered);
            }
            registered.Clear();
            if (limbs == null) return;
            foreach (var limb in limbs)
            {
                if (limb == null || registered.Contains(limb)) continue;
                registered.Add(limb);
                var relay = limb.gameObject.GetComponent<FlyLimbDamageRelay>();
                if (relay == null) relay = limb.gameObject.AddComponent<FlyLimbDamageRelay>();
                relay.Bind(this, limb);
            }
        }

        private void FixedUpdate()
        {
            if (!initialized) Initialize();
            if (physical == null) physical = gameObject.GetComponent<PhysicalBehaviour>();
            if (HasNativeBindings || physical == null) return;
            if (physical.isDisintegrated) { ApplyDamageToAll(1f); return; }
            var elapsed = IsFinite(Time.fixedDeltaTime) ? Math.Max(0f, Math.Min(.25f, Time.fixedDeltaTime)) : 0f;
            if (elapsed <= 0f) return;
            if (physical.OnFire || physical.BurnIntensity > .01f)
            {
                var burn = Math.Max(.02f, Unit(physical.BurnIntensity)) * .3f * elapsed;
                ApplyDamage(FlyHealthRegion.LeftWing, burn);
                ApplyDamage(FlyHealthRegion.RightWing, burn);
                ApplyDamage(FlyHealthRegion.Thorax, burn * .35f);
            }
            if (physical.IsInLava) ApplyDamageToAll(.7f * elapsed);
            if (physical.IsBeingStabbed) ApplyDamage(FlyHealthRegion.Thorax, .25f * elapsed);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!HasNativeBindings) RecordImpact(collision, null);
        }

        public float RegisterImpact(Vector2 worldPoint, float relativeSpeed)
        {
            if (!initialized) Initialize();
            var damage = DamageFor(relativeSpeed);
            if (damage <= 0f || HasNativeBindings) return damage;
            var local = transform.InverseTransformPoint(new Vector3(worldPoint.x, worldPoint.y, 0f));
            ApplyDamage(RegionAt(local), damage);
            return damage;
        }

        public void ApplyDamage(FlyHealthRegion region, float amount)
        {
            if (!initialized) Initialize();
            var index = (int)region;
            // Native simulation owns health for bound parts; never duplicate its damage.
            if (boundLimbs.ContainsKey(region) || index < 0 || index >= regionHealth.Length || !IsFinite(amount) || amount <= 0f) return;
            var before = regionHealth[index];
            regionHealth[index] = Math.Max(0f, before - amount);
            pendingDamageEvent = Math.Max(pendingDamageEvent, before - regionHealth[index]);
        }

        internal void RecordNativeImpact(LimbBehaviour source, Collision2D collision)
        {
            if (source != null && IsBound(source)) RecordImpact(collision, source);
        }

        public void Read(ref SensoryFrame frame)
        {
            if (!initialized) Initialize();
            frame = RefreshNativeRegions(frame) ?? frame;
            var person = Head == null ? null : Head.Person;
            if (person != null)
            {
                frame.BrainDead = person.Braindead;
                frame.ConsciousnessValid = IsFinite(person.Consciousness);
                frame.Consciousness = Unit(person.Consciousness);
                frame.OxygenValid = IsFinite(person.OxygenLevel);
                frame.Oxygen = Unit(person.OxygenLevel);
                frame.Unconscious = frame.ConsciousnessValid && frame.Consciousness <= .05f ? 1f : 0f;
            }
            frame.HealthValid = true;
            frame.Alive = IsAlive;
            frame.Health = OverallHealth;
            frame.DamageValid = true;
            frame.Damage = 1f - frame.Health;
            frame.VitalityValid = true;
            frame.Vitality = MinimumLimbHealth;
            frame.DamageEvent = Math.Max(frame.DamageEvent, pendingDamageEvent);
            frame.Pain = Math.Max(frame.Pain, Math.Max(frame.Damage * .5f, pendingDamageEvent));
            frame.Shock = Math.Max(frame.Shock, pendingDamageEvent);
            var lost = 0;
            for (var index = (int)FlyHealthRegion.LeftWing; index < regionHealth.Length; index++) if (regionHealth[index] <= 0f) lost++;
            frame.LimbLoss = Math.Max(frame.LimbLoss, (float)lost / PeripheralRegionCount);
            frame.Breakage = Math.Max(frame.Breakage, lost > 0 ? 1f : 0f);
            pendingDamageEvent = 0f;
        }

        private bool HasNativeBindings => boundLimbs.Count > 0;
        private bool NativePersonIsAlive()
        {
            foreach (var limbs in boundLimbs.Values)
            {
                foreach (var limb in limbs)
                {
                    if (limb == null || limb.Person == null) continue;
                    // AverageHealth is a frame aggregate and can still be zero
                    // before the first native Update. Critical limb health above
                    // is authoritative at spawn and between neural samples.
                    return !limb.Person.Braindead;
                }
            }
            return true;
        }
        private float RegionAverage() { var total = 0f; for (var i = 0; i < regionHealth.Length; i++) total += regionHealth[i]; return total / regionHealth.Length; }

        private SensoryFrame? RefreshNativeRegions(SensoryFrame? frame)
        {
            foreach (var pair in boundLimbs)
            {
                var health = 1f;
                var hasSample = false;
                foreach (var limb in pair.Value)
                {
                    var value = ReadNativeLimb(limb, ref frame, out var valid);
                    if (!valid) continue;
                    health = hasSample ? Math.Min(health, value) : value;
                    hasSample = true;
                }
                regionHealth[(int)pair.Key] = hasSample ? health : 0f;
            }
            return frame;
        }

        private float ReadNativeLimb(LimbBehaviour limb, ref SensoryFrame? frame, out bool valid)
        {
            valid = true;
            if (limb == null || limb.gameObject == null || limb.IsDismembered || (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.isDisintegrated))
            {
                TrackNativeDamage(limb, 0f, false);
                return 0f;
            }
            if (!limb.gameObject.activeInHierarchy)
            {
                if (frame.HasValue) { var value = frame.Value; value.Breakage = Math.Max(value.Breakage, 1f); frame = value; }
                TrackNativeDamage(limb, 0f, false);
                return 0f;
            }
            if (frame.HasValue) { var value = frame.Value; ReadNativeStatus(limb, ref value); frame = value; }
            if (!IsFinite(limb.Health) || !IsFinite(limb.InitialHealth) || limb.InitialHealth <= 0f)
            {
                valid = false;
                nativeHealthSamples.Remove(limb);
                return 0f;
            }
            var health = limb.Health >= limb.InitialHealth ? 1f : Unit(limb.Health / limb.InitialHealth);
            TrackNativeDamage(limb, health, true);
            return health;
        }

        private void TrackNativeDamage(LimbBehaviour? limb, float health, bool connected)
        {
            if (limb == null) return;
            if (connected)
            {
                if (nativeHealthSamples.TryGetValue(limb, out var before) && health < before) pendingDamageEvent = Math.Max(pendingDamageEvent, before - health);
                if (health > 0f) nativeHealthSamples[limb] = health; else nativeHealthSamples.Remove(limb);
                return;
            }
            if (nativeHealthSamples.TryGetValue(limb, out var lost)) pendingDamageEvent = Math.Max(pendingDamageEvent, lost);
            nativeHealthSamples.Remove(limb);
        }

        private static void ReadNativeStatus(LimbBehaviour limb, ref SensoryFrame frame)
        {
            frame.Breakage = Math.Max(frame.Breakage, limb.Broken || limb.CurrentlyShattered != 0 ? 1f : 0f);
            frame.Frozen = Math.Max(frame.Frozen, limb.Frozen ? 1f : 0f);
            frame.Paralysis = Math.Max(frame.Paralysis, limb.IsParalysed ? 1f : 0f);
            var part = limb.PhysicalBehaviour;
            if (part != null)
            {
                frame.PhysicalContact = Math.Max(frame.PhysicalContact, part.IsTouchingSomething ? 1f : 0f);
                frame.Fire = Math.Max(frame.Fire, part.OnFire ? Unit(part.BurnIntensity) : 0f);
                frame.BurnProgress = Math.Max(frame.BurnProgress, Unit(part.BurnProgress));
                frame.Stabbed = Math.Max(frame.Stabbed, part.IsBeingStabbed ? 1f : 0f);
            }
            var circulation = limb.CirculationBehaviour;
            if (circulation == null) return;
            frame.Bleeding = Math.Max(frame.Bleeding, Unit(circulation.BleedingRate));
            frame.InternalBleeding = Math.Max(frame.InternalBleeding, Unit(circulation.InternalBleedingIntensity));
            frame.Wounds = Math.Max(frame.Wounds, Unit((circulation.StabWoundCount + circulation.GunshotWoundCount + circulation.BleedingPointCount) / 8f));
            frame.Disconnected = Math.Max(frame.Disconnected, circulation.IsDisconnected || !circulation.HasCirculation ? 1f : 0f);
        }

        private void RecordImpact(Collision2D collision, LimbBehaviour? source)
        {
            if (collision == null || collision.collider == null || !IsExternal(collision.collider.transform) || !IsFinite(collision.relativeVelocity.x) || !IsFinite(collision.relativeVelocity.y)) return;
            var damage = DamageFor(collision.relativeVelocity.magnitude);
            if (damage <= 0f) return;
            if (experienceMemory == null) experienceMemory = gameObject.GetComponent<FlyExperienceMemory>();
            experienceMemory?.RecordHarm(collision.collider.gameObject, damage);
            if (source == null) RegisterImpact(collision.collider.ClosestPoint((Vector2)transform.position), collision.relativeVelocity.magnitude);
        }

        private bool IsExternal(Transform other)
        {
            if (other == null || other == transform || other.IsChildOf(transform)) return false;
            var ownPerson = GetComponentInParent<PersonBehaviour>();
            return ownPerson == null || (other != ownPerson.transform && !other.IsChildOf(ownPerson.transform));
        }
        private bool IsBound(LimbBehaviour candidate) { foreach (var limbs in boundLimbs.Values) if (limbs.Contains(candidate)) return true; return false; }
        private void ApplyDamageToAll(float amount) { for (var index = 0; index < regionHealth.Length; index++) ApplyDamage((FlyHealthRegion)index, amount); }
        private static float DamageFor(float speed) => !IsFinite(speed) || speed <= 2f ? 0f : Math.Min(.65f, (speed - 2f) * .025f);
        private static FlyHealthRegion RegionAt(Vector3 local)
        {
            if (local.x < -.22f) return FlyHealthRegion.Head;
            if (local.x > .18f) return FlyHealthRegion.Abdomen;
            if (local.y > .05f) return local.y > .14f ? FlyHealthRegion.LeftWing : FlyHealthRegion.RightWing;
            if (local.y < -.07f) return local.x < -.08f ? FlyHealthRegion.FrontLeftLeg : local.x > .08f ? FlyHealthRegion.HindRightLeg : FlyHealthRegion.MiddleLeftLeg;
            return FlyHealthRegion.Thorax;
        }
        private static string RegionName(FlyHealthRegion region)
        {
            switch (region)
            {
                case FlyHealthRegion.Head: return "head";
                case FlyHealthRegion.Thorax: return "thorax";
                case FlyHealthRegion.Abdomen: return "abdomen";
                case FlyHealthRegion.LeftWing: return "left-wing";
                case FlyHealthRegion.RightWing: return "right-wing";
                case FlyHealthRegion.FrontLeftLeg: return "front-left-leg";
                case FlyHealthRegion.FrontRightLeg: return "front-right-leg";
                case FlyHealthRegion.MiddleLeftLeg: return "middle-left-leg";
                case FlyHealthRegion.MiddleRightLeg: return "middle-right-leg";
                case FlyHealthRegion.HindLeftLeg: return "hind-left-leg";
                case FlyHealthRegion.HindRightLeg: return "hind-right-leg";
                default: return "unknown";
            }
        }
        private static float Unit(float value) => IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
