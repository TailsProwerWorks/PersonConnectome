using System;
using Mod.Adapters;
using Mod.Core;
using Mod.UI;

// Test doubles model only the public surface used by the linked production adapter.
// They establish command/sensor contracts, not Unity physics or rendering behavior.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a, Object b) =>
            (ReferenceEquals(a, null) || a.Destroyed) ? ReferenceEquals(b, null) || b.Destroyed : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : Component
        {
            for (var t = transform; t != null; t = t.parent)
            {
                var c = t.GetComponent<T>();
                if (c != null) return c;
            }
            return null;
        }
        public void GetComponents<T>(List<T> result) where T : Component => gameObject.GetComponents(result);
    }
    public class MonoBehaviour : Component { }
    public sealed class DefaultExecutionOrderAttribute : Attribute
    {
        public readonly int Order;
        public DefaultExecutionOrderAttribute(int order) { Order = order; }
    }
    public class GameObject : Object
    {
        public string name;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public readonly Transform transform;
        private readonly List<Component> components = [];
        public GameObject(string name = "") { this.name = name; transform = new Transform { gameObject = this }; components.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; components.Add(c); return c; }
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault(c => c != null);
        public void GetComponents<T>(List<T> result) where T : Component
        {
            result.Clear();
            result.AddRange(components.OfType<T>().Where(c => c != null));
        }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) where T : Component
        {
            result.Clear(); Collect(this, includeInactive, result);
        }
        private static void Collect<T>(GameObject go, bool includeInactive, List<T> result) where T : Component
        {
            if (go == null || (!includeInactive && !go.activeSelf)) return;
            result.AddRange(go.components.OfType<T>().Where(c => c != null));
            foreach (var child in go.transform.Children) Collect(child.gameObject, includeInactive, result);
        }
    }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> Children = [];
        public int childCount => Children.Count;
        public Vector3 position;
        public Vector3 lossyScale = new(1f, 1f, 1f);
        public float RotationDegrees;
        public Vector3 TransformVector(Vector3 vector)
        {
            var angle = RotationDegrees * MathF.PI / 180f;
            var x = vector.x * lossyScale.x; var y = vector.y * lossyScale.y;
            return new(x * MathF.Cos(angle) - y * MathF.Sin(angle), x * MathF.Sin(angle) + y * MathF.Cos(angle), vector.z * lossyScale.z);
        }
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            var angle = -RotationDegrees * MathF.PI / 180f;
            var x = point.x - position.x; var y = point.y - position.y;
            return new((x * MathF.Cos(angle) - y * MathF.Sin(angle)) / lossyScale.x,
                (x * MathF.Sin(angle) + y * MathF.Cos(angle)) / lossyScale.y, 0f);
        }
        public void SetParent(Transform value) { parent?.Children.Remove(this); parent = value; value?.Children.Add(this); }
        public bool IsChildOf(Transform root) { for (var t = this; t != null; t = t.parent) if (t == root) return true; return false; }
    }
    public struct Vector2(float x, float y)
    {
        public float x = x, y = y;
        public readonly float magnitude => MathF.Sqrt(x * x + y * y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static explicit operator Vector2(Vector3 v) => new(v.x, v.y);
    }
    public struct Color
    {
        public float grayscale, r, g, b, a;
        public Color(float red, float green, float blue, float alpha)
        {
            r = red; g = green; b = blue; a = alpha; grayscale = .299f * r + .587f * g + .114f * b;
        }
    }
    public enum SpriteDrawMode { Simple, Sliced, Tiled }
    public class Sprite : Object { public Bounds bounds; }
    public class SpriteRenderer : Component
    {
        public bool enabled = true, flipX, flipY;
        public Color color = new(1f, 1f, 1f, 1f);
        public Sprite sprite;
        public SpriteDrawMode drawMode;
        public Vector2 size;
    }
    public static class RenderSettings { public static Color ambientLight; }
    public static class Mathf
    {
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Abs(float v) => MathF.Abs(v);
        public static float Sign(float v) => v >= 0 ? 1 : -1;
    }
    public class Collider2D : Component
    {
        public bool enabled = true, isTrigger;
        public Collider2D Touching;
        public bool IsTouching(Collider2D other) => Touching == other;
        public Vector2 Surface;
        public Bounds bounds;
        public Vector2 ClosestPoint(Vector2 origin) => Surface;
    }
    public enum RigidbodyType2D { Dynamic, Kinematic, Static }
    public struct Bounds { public Vector3 center, extents; }
    public class Rigidbody2D : Component { public Vector2 velocity; public float angularVelocity; public RigidbodyType2D bodyType = RigidbodyType2D.Dynamic; }
    public class HingeJoint2D : Component
    {
        public Rigidbody2D connectedBody;
        public float jointAngle, jointSpeed;
    }
    public class AmbientTemperatureGridBehaviour : Component
    {
        public static AmbientTemperatureGridBehaviour Instance;
        public float Temperature = 20;
        public float GetTemperatureAtPoint(Vector2 point) => Temperature;
    }
    public struct RaycastHit2D { public Collider2D collider; }
    public static class Physics2D
    {
        public static Collider2D[] Hits = [];
        public static RaycastHit2D LinecastResult;
        public static RaycastHit2D[] LinecastHits = [];
        public static Func<Vector2, Vector2, RaycastHit2D[]> LinecastHandler;
        public static int LinecastCalls;
        public static float LastOverlapRadius;
        public static int OverlapCircleNonAlloc(Vector2 position, float radius, Collider2D[] buffer)
        { LastOverlapRadius = radius; var count = Math.Min(Hits.Length, buffer.Length); Array.Copy(Hits, buffer, count); return count; }
        public static RaycastHit2D Linecast(Vector2 start, Vector2 end) => LinecastResult;
        public static int LinecastNonAlloc(Vector2 start, Vector2 end, RaycastHit2D[] buffer)
        {
            LinecastCalls++;
            var hits = LinecastHandler != null ? LinecastHandler(start, end) : LinecastHits.Length > 0 ? LinecastHits : LinecastResult.collider == null ? [] : [LinecastResult];
            var count = Math.Min(hits.Length, buffer.Length); Array.Copy(hits, buffer, count); return count;
        }
    }
    public enum FFTWindow { Rectangular }
    public static class AudioSettings { public static int outputSampleRate = 48000; }
    public class UnityException : Exception { }
    public class AudioSource : Component
    {
        public bool isPlaying, mute; public bool isActiveAndEnabled = true; public float volume = 1; public float[] Spectrum = []; public int SpectrumCalls; public float[] LastSpectrumBuffer; public bool ThrowSpectrum;
        public void GetSpectrumData(float[] samples, int channel, FFTWindow window) { SpectrumCalls++; LastSpectrumBuffer = samples; if (ThrowSpectrum) throw new UnityException(); Array.Clear(samples); Array.Copy(Spectrum, samples, Math.Min(Spectrum.Length, samples.Length)); }
    }
    public class Collision2D { public Collider2D collider; public Vector2 relativeVelocity; }
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public static class Time { public static float fixedDeltaTime = .02f, deltaTime = .02f, unscaledDeltaTime = .02f, realtimeSinceStartup, time; }
    public static class Debug { public static void Log(string message) { } }
}
public class RagdollPose
{
    public bool ShouldStandUpright = true, ShouldStumble = true;
    public float UprightForceMultiplier = 1f, ForceMultiplier = 1f;
}
public class PersonBehaviour : UnityEngine.Component
{
    public LimbBehaviour[] Limbs = [];
    public List<RagdollPose> Poses = [];
    public RagdollPose ActivePose;
    public bool Braindead, BrainDamaged, IsTouchingFloor;
    public float AverageHealth = 1, Consciousness = 1, OxygenLevel = 1;
    public float PainLevel, ShockLevel, AdrenalineLevel, AverageFireIntensity, AverageWetness, AverageSpeed, AngleOffset, BalanceOffset, Heartbeat, SeizureTime, BrainDamagedTime, DesiredWalkingDirection;
}
public class LimbBehaviour : UnityEngine.Component
{
    public bool IsCapable = true, HasJoint = true;
    public bool DoBalanceJerk = true, DoStumble = true, IsActiveInCurrentPose = true;
    public bool HasBrain, IsDismembered, Broken, Frozen, IsParalysed, HasLungs, LungsPunctured, IsOnFloor, IsZombie;
    public PersonBehaviour Person;
    public int CurrentlyShattered;
    public float Health = 100, InitialHealth = 100, Vitality = 1, BodyTemperature = 37, InternalTemperature = 37;
    public float JointStress, Numbness, RegenerationSpeed, MotorSpeed;
    public float FakeUprightForce = 10f, BalanceMuscleMovement = 1f;
    public int MotorCalls;
    public GripBehaviour GripBehaviour;
    public UnityEngine.HingeJoint2D Joint;
    public CirculationBehaviour CirculationBehaviour;
    public PhysicalBehaviour PhysicalBehaviour;
    public void InfluenceMotorSpeed(float speed, float influence)
    {
        if (!HasJoint) return;
        MotorCalls++;
        // Mirrors the interpolation confirmed in installed game IL.
        MotorSpeed += (speed - MotorSpeed) * UnityEngine.Mathf.Clamp01(influence);
    }
}
public class CirculationBehaviour : UnityEngine.Component
{
    public class RefFloat { public float Raw; }
    public Dictionary<Liquid, RefFloat> LiquidDistribution = [];
    public float TotalLiquidAmount = 1, BloodFlow = 1;
    // Native BloodAmount aliases all liquid; GetAmountOfBlood selects original blood.
    public float BloodAmount => TotalLiquidAmount;
    public static readonly Liquid OriginalBlood = new("BLOOD");
    public float OriginalBloodAmount
    {
        set => LiquidDistribution[OriginalBlood] = new RefFloat { Raw = value };
    }
    public bool HasBloodFlow = true, HasCirculation = true, IsDisconnected;
    public float BleedingRate, InternalBleedingIntensity, BloodRegenerationPerSecond;
    public int StabWoundCount, GunshotWoundCount, BleedingPointCount;
    public float GetHeartRate() => 0;
    public float GetAmountOfBlood() => LiquidDistribution.TryGetValue(OriginalBlood, out var value) ? value.Raw : 0f;
}
public class SpawnableAsset : UnityEngine.Object { public string name; }
public class JukeboxBehaviour : UnityEngine.Component { public UnityEngine.AudioSource audioSource; }
public class SerialiseInstructions : UnityEngine.Component { public SpawnableAsset OriginalSpawnableAsset; }
public static class ModAPI
{
    public static SpawnableAsset Pumpkin;
    public static SpawnableAsset FindSpawnable(string name) => name == "Pumpkin" ? Pumpkin : null;
}
public class PhysicalBehaviour : UnityEngine.Component
{
    public bool OnFire, IsUnderWater, IsInLava, IsBeingStabbed, IsTouchingSomething, beingHeldByGripper, IsWeightless, isSliding, isDisintegrated, BulletPenetration;
    public float BurnIntensity, BurnProgress, Wetness, Charge;
    public float Temperature = 37;
    public UnityEngine.Rigidbody2D rigidbody;
    public UnityEngine.AudioSource MainAudioSource;
}
public class Liquid(string identity)
{
    public readonly string Identity = identity;
    public string DisplayName = identity;
    public static string GetIdentity(Liquid liquid) => liquid.Identity;
    public string GetDisplayName() => DisplayName;
}
public class AcidPoolBehaviour : UnityEngine.Component
{
    public float AcidProgress, PainIntensity;
}
public class LavaBehaviour : UnityEngine.Component
{
    public float LavaTemperature = 100;
}
public class ProjectileBehaviour : UnityEngine.Component { }
public class GorseProjectileBehaviour : UnityEngine.Component { }
public class GenericScifiProjectileBehaviour : UnityEngine.Component { }
public class MachineGunProjectileBehaviour : UnityEngine.Component { }
public class LaunchedRocketBehaviour : UnityEngine.Component { }
public class ActivationPropagation { }
public class ContextMenuOptionComponent : UnityEngine.Component
{
    public List<ContextMenuButton> Buttons = [];
}
public struct ContextMenuButton
{
    public string Identity;
    public string Description;
    public ContextMenuButton(string identity, string description = "") { Identity = identity; Description = description; }
}
public class LightSprite : UnityEngine.Component
{
    public UnityEngine.SpriteRenderer SpriteRenderer;
    public float Brightness = 1f;
}
public class GlowtubeBehaviour : UnityEngine.Component { public UnityEngine.SpriteRenderer LightSprite; }
public class BulbBehaviour : UnityEngine.Component { public UnityEngine.SpriteRenderer LightSprite; }
public class LEDBulbBehaviour : UnityEngine.Component { public UnityEngine.SpriteRenderer LightSprite; }
public class ActivationToggleBehaviour : UnityEngine.Component { public UnityEngine.GameObject LightObject; }
public class SingleFloodlightBehaviour : UnityEngine.Component { public UnityEngine.GameObject ToToggle; }
public class FlashlightAttachmentBehaviour : UnityEngine.Component { public UnityEngine.SpriteRenderer[] Lights; }
public class GripBehaviour
{
    public bool isHolding;
    public int GrabCalls, DropCalls;
    public void Use(ActivationPropagation propagation) { GrabCalls++; isHolding = true; }
    public void DropObject() { DropCalls++; isHolding = false; }
}
namespace Mod
{
    // Lifecycle tests only need a neutral brain; neural-source tests use their own harness.
    internal class LifBrain
    {
        public int StepCount;
        public float LastElapsed;
        public string Status => "test";
        public static LifBrain TryCreate(out string status) { status = "test"; return new(); }
        public MotorCommand Step(SensoryFrame frame) => default;
        public MotorCommand Step(SensoryFrame frame, float elapsed) { StepCount++; LastElapsed = elapsed; return default; }
        public MotorCommand Step(SensoryFrame frame, float elapsed, ManualInputState manualInput) { StepCount++; LastElapsed = elapsed; return default; }
        public void Stop() { }
    }
    internal class PersonConnectomeStatusDisplay
    {
        private static int activeCount;
        private static int renderedUpdates;
        private bool active;
        public static int ActiveCount => activeCount;
        public static int RenderedUpdates => renderedUpdates;
        public static void ResetForTest() { activeCount = 0; renderedUpdates = 0; }
        public PersonConnectomeStatusDisplay(UnityEngine.Transform anchor, ManualInputState manualInput, Func<bool> isDirectFlyControlEnabled = null, Action<bool> setDirectFlyControl = null) { }
        public void Update(float elapsed, LifBrain brain, PeoplePlaygroundPersonAdapter adapter) { if (active) renderedUpdates++; }
        public void SetActive(bool value)
        {
            if (active == value) return;
            active = value;
            activeCount += value ? 1 : -1;
        }
        public void RecordTick(float sensorMs, float brainMs, float actuatorMs, float skipped, LifBrain brain) { }
        public void Dispose() { SetActive(false); }
    }
}
