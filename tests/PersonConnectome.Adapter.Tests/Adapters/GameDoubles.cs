using System;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using ShadowNineX.PersonConnectome.UI;

// Test doubles model only the public surface used by the linked production adapter.
// They establish command/sensor contracts, not Unity physics or rendering behavior.
namespace UnityEngine
{
    // Unity's Object equality intentionally treats destroyed instances as null.
#pragma warning disable S3875 // Compatibility double must preserve Unity's overloaded equality semantics.
    public class Object
    {
        public bool Destroyed;
        public static void Destroy(Object target) { if (target != null) target.Destroyed = true; }
        public static bool operator ==(Object a, Object b) =>
            (ReferenceEquals(a, null) || a.Destroyed) ? ReferenceEquals(b, null) || b.Destroyed : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
#pragma warning restore S3875
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
    public class MonoBehaviour : Component
    {
        public bool enabled = true;
    }
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
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
        public Vector3 localScale { get => lossyScale; set => lossyScale = value; }
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
        public bool IsChildOf(Transform root)
        {
            for (var t = this; t != null; t = t.parent)
            {
                if (t == root)
                {
                    return true;
                }
            }

            return false;
        }
    }
    public struct Vector2(float x, float y)
    {
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public float x = x, y = y;
        public readonly float magnitude => MathF.Sqrt(x * x + y * y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 down => new(0f, -1f, 0f);
        public static explicit operator Vector2(Vector3 v) => new(v.x, v.y);
    }
    public struct Quaternion
    {
        private float Degrees;
        public static Quaternion Euler(float x, float y, float z) => new() { Degrees = z };
        public static Vector3 operator *(Quaternion rotation, Vector3 vector)
        {
            var radians = rotation.Degrees * MathF.PI / 180f;
            return new(vector.x * MathF.Cos(radians) - vector.y * MathF.Sin(radians),
                vector.x * MathF.Sin(radians) + vector.y * MathF.Cos(radians), vector.z);
        }
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
    public class Texture : Object { }
    public class Material : Object
    {
        private readonly Dictionary<string, Texture> textures = [];
        public void SetTexture(string name, Texture texture) => textures[name] = texture;
        public Texture GetTexture(string name) => textures.TryGetValue(name, out var texture) ? texture : null;
    }
    public class Sprite : Object { public Bounds bounds; public Texture texture; public float pixelsPerUnit; }
    public class SpriteRenderer : Component
    {
        public bool enabled = true, flipX, flipY;
        public Color color = new(1f, 1f, 1f, 1f);
        public Sprite sprite;
        public SpriteDrawMode drawMode;
        public Vector2 size;
        public Material material = new();
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
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
        public const float Rad2Deg = 180f / MathF.PI;
        public static float DeltaAngle(float current, float target)
        {
            var delta = (target - current) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            return delta;
        }
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
#pragma warning disable S2342 // Exact Unity API type name is required by linked production source.
    public enum RigidbodyType2D { Dynamic, Kinematic, Static }
#pragma warning restore S2342
    public struct Bounds { public Vector3 center, extents; }
    public class Rigidbody2D : Component
    {
        public Vector2 velocity;
        public float angularVelocity, rotation, gravityScale = 1f, mass = 1f, AppliedTorque;
        public RigidbodyType2D bodyType = RigidbodyType2D.Dynamic;
        public void AddTorque(float torque) => AppliedTorque += torque;
    }
    public struct JointMotor2D
    {
        public float motorSpeed, maxMotorTorque;
    }
    public class HingeJoint2D : Component
    {
        public Rigidbody2D connectedBody;
        public float jointAngle, jointSpeed;
        public float breakForce = float.PositiveInfinity, breakTorque = float.PositiveInfinity;
        public JointMotor2D motor;
        public bool useMotor;
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
        public static readonly HashSet<(Collider2D, Collider2D)> IgnoredCollisions = [];
        public static Vector2 gravity = new(0f, -9.81f);
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
            RaycastHit2D[] hits;
            if (LinecastHandler != null)
            {
                hits = LinecastHandler(start, end);
            }
            else if (LinecastHits.Length > 0)
            {
                hits = LinecastHits;
            }
            else
            {
                hits = LinecastResult.collider == null ? [] : [LinecastResult];
            }
            var count = Math.Min(hits.Length, buffer.Length); Array.Copy(hits, buffer, count); return count;
        }
        public static void IgnoreCollision(Collider2D left, Collider2D right, bool ignore)
        {
            if (left == null || right == null) return;
            if (ignore) IgnoredCollisions.Add((left, right));
            else { IgnoredCollisions.Remove((left, right)); IgnoredCollisions.Remove((right, left)); }
        }
        public static bool GetIgnoreCollision(Collider2D left, Collider2D right) =>
            IgnoredCollisions.Contains((left, right)) || IgnoredCollisions.Contains((right, left));
    }
#pragma warning disable S2342 // Exact Unity API type name is required by linked production source.
    public enum FFTWindow { Rectangular }
#pragma warning restore S2342
    public static class AudioSettings { public static int outputSampleRate = 48000; }
    public class UnityException : Exception { }
    public class AudioSource : Component
    {
        public bool isPlaying, mute; public bool isActiveAndEnabled = true; public float volume = 1; public float[] Spectrum = []; public int SpectrumCalls; public float[] LastSpectrumBuffer; public bool ThrowSpectrum;
        public void GetSpectrumData(float[] samples, int channel, FFTWindow window)
        {
            SpectrumCalls++;
            LastSpectrumBuffer = samples;
            if (ThrowSpectrum)
            {
                throw new UnityException();
            }
            Array.Clear(samples);
            Array.Copy(Spectrum, samples, Math.Min(Spectrum.Length, samples.Length));
        }
    }
    public class Collision2D { public Collider2D collider; public Vector2 relativeVelocity; }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public static class Time { public static float fixedDeltaTime = .02f, deltaTime = .02f, unscaledDeltaTime = .02f, realtimeSinceStartup, time; }
    public static class Debug
    {
        // Logging is intentionally ignored by the test double.
#pragma warning disable S1186 // No-op mirrors Unity's test-facing logging boundary.
        public static void Log(string message) { }
#pragma warning restore S1186
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> Values = [];
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static string GetString(string key, string fallback = "") => Values.TryGetValue(key, out var value) ? value : fallback;
        public static void SetString(string key, string value) => Values[key] = value;
        public static void DeleteKey(string key) => Values.Remove(key);
        // Persistence is intentionally ignored by the test double.
#pragma warning disable S1186 // No-op mirrors Unity's test-facing persistence boundary.
        public static void Save() { }
#pragma warning restore S1186
    }
}
// These global names mirror the game API consumed by linked production sources.
#pragma warning disable S3903 // Game doubles must remain in the global namespace.
#pragma warning disable S101 // External game API names are intentionally exact.
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
    public float BreakingThreshold = 1f, JointStress, Numbness, RegenerationSpeed, MotorSpeed;
    public float FakeUprightForce = 10f, BalanceMuscleMovement = 1f;
    public int MotorCalls;
    public GripBehaviour GripBehaviour;
    public UnityEngine.HingeJoint2D Joint;
    public CirculationBehaviour CirculationBehaviour;
    public PhysicalBehaviour PhysicalBehaviour;
    public SkinMaterialHandler SkinMaterialHandler;
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
    // Setter-only because the native API exposes assignment as its contract.
#pragma warning disable S2376 // Compatibility surface intentionally exposes a setter-only property.
    public float OriginalBloodAmount
    {
        set => LiquidDistribution[OriginalBlood] = new RefFloat { Raw = value };
    }
#pragma warning restore S2376
    public bool HasBloodFlow = true, HasCirculation = true, IsDisconnected;
    public float BleedingRate, InternalBleedingIntensity, BloodRegenerationPerSecond;
    public int StabWoundCount, GunshotWoundCount, BleedingPointCount;
    public float HeartRate;
    public float GetHeartRate() => HeartRate;
    public float GetAmountOfBlood() => LiquidDistribution.TryGetValue(OriginalBlood, out var value) ? value.Raw : 0f;
}
public class SpawnableAsset : UnityEngine.Object { public string name; }
public class JukeboxBehaviour : UnityEngine.Component { public UnityEngine.AudioSource audioSource; }
public class SerialiseInstructions : UnityEngine.Component { public SpawnableAsset OriginalSpawnableAsset; }
public static class ModAPI
{
    public static SpawnableAsset Pumpkin;
    public static SpawnableAsset FindSpawnable(string name) => name == "Pumpkin" ? Pumpkin : null;
    public static bool ThrowOnAssetLoad;
    public static int LoadSpriteCalls, LoadTextureCalls;
    public static UnityEngine.Vector2 SpritePixelSize;
    public static UnityEngine.Sprite CachedSprite;
    public static UnityEngine.Sprite LoadSprite(string path, float scale = 1f, bool pixelated = true)
    {
        LoadSpriteCalls++;
        if (ThrowOnAssetLoad) throw new InvalidOperationException("Path loading is forbidden in this test.");
        if (CachedSprite != null) return CachedSprite;
        // Exact installed PPG ModAPI contract: scale is multiplied by 35.
        var pixelsPerUnit = 35f * scale;
        return new UnityEngine.Sprite
        {
            pixelsPerUnit = pixelsPerUnit,
            bounds = new UnityEngine.Bounds
            {
                extents = new UnityEngine.Vector3(SpritePixelSize.x / (2f * pixelsPerUnit), SpritePixelSize.y / (2f * pixelsPerUnit), 0f)
            }
        };
    }
    public static UnityEngine.Texture LoadTexture(string path)
    {
        LoadTextureCalls++;
        if (ThrowOnAssetLoad) throw new InvalidOperationException("Path loading is forbidden in this test.");
        return new UnityEngine.Texture();
    }
}
public class SkinMaterialHandler : UnityEngine.Component
{
    public int SyncCalls;
    public void Sync() => SyncCalls++;
}
public class ShatteredObjectSpriteInitialiser : UnityEngine.Component
{
    public int UpdateCalls;
    public LimbSpriteCache.LimbSprites LastLayers;
    public void UpdateSprites(in LimbSpriteCache.LimbSprites layers) { UpdateCalls++; LastLayers = layers; }
}
public static class LimbSpriteCache
{
    public readonly struct LimbSprites
    {
        public LimbSprites(UnityEngine.Sprite skin, UnityEngine.Sprite flesh, UnityEngine.Sprite bone)
        {
            Skin = skin; Flesh = flesh; Bone = bone;
        }
        public UnityEngine.Sprite Skin { get; }
        public UnityEngine.Sprite Flesh { get; }
        public UnityEngine.Sprite Bone { get; }
    }
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
// Marker types: the game API supplies no members.
#pragma warning disable S2094 // Empty marker doubles intentionally model game types.
public class ProjectileBehaviour : UnityEngine.Component { }
public class GorseProjectileBehaviour : UnityEngine.Component { }
public class GenericScifiProjectileBehaviour : UnityEngine.Component { }
public class MachineGunProjectileBehaviour : UnityEngine.Component { }
public class LaunchedRocketBehaviour : UnityEngine.Component { }
#pragma warning restore S2094
// Marker type: the game API supplies no members.
#pragma warning disable S2094 // Empty marker double intentionally models the game type.
public class ActivationPropagation { }
#pragma warning restore S2094
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
namespace ShadowNineX.PersonConnectome
{
    internal enum StatusDisplayBodyKind
    {
        Person,
        Fly
    }

    internal sealed class TrainingControlBindings
    {
        public Func<string> Status;
        public Action Start, End, TogglePause, Good, Bad, Undo, RestoreBest, Reset, Save, Load;
    }

    internal sealed class StatusDisplayBindings
    {
        public Func<bool> IsDirectFlyControlEnabled;
        public Action<bool> SetDirectFlyControl;
        public TrainingControlBindings Training;
    }

    // Lifecycle tests only need a neutral brain; neural-source tests use their own harness.
    // Lifecycle tests use this intentionally state-light brain double.
#pragma warning disable S2325 // Instance members mirror the production brain contract.
#pragma warning disable S1186 // No-op lifecycle methods intentionally preserve the production contract.
    internal class LifBrain
    {
        public int StepCount;
        public float LastElapsed;
        public SensoryFrame LastFrame;
        public ManualInputState LastManualInput;
        public string Status => "test";
        public string LearningStatusText => "PLASTICITY: test";
        public int LearnedSynapseCount { get; set; }
        public ShadowNineX.PersonConnectome.Core.ConnectomeLearningMode LearningMode { get; private set; }
        public int FeedbackCalls { get; private set; }
        public float LastFeedback { get; private set; }
        public float AdvancedLearningSeconds { get; private set; }
        public string Memory { get; private set; } = "memory-0";
        public static LifBrain TryCreate(out string status) { status = "test"; return new(); }
        public FlyMotorCommand Step(SensoryFrame frame) => default;
        public FlyMotorCommand Step(SensoryFrame frame, float elapsed) { StepCount++; LastElapsed = elapsed; return default; }
        public FlyMotorCommand Step(SensoryFrame frame, float elapsed, ManualInputState manualInput) { StepCount++; LastElapsed = elapsed; LastFrame = frame; LastManualInput = manualInput; return default; }
        public void SetLearningMode(ShadowNineX.PersonConnectome.Core.ConnectomeLearningMode mode) { LearningMode = mode; }
        public void AdvanceLearningTime(float elapsedSeconds) { AdvancedLearningSeconds += elapsedSeconds; }
        public int ApplyReinforcement(float reward, float elapsedSeconds = .05f) => 0;
        public int ApplyFeedbackReinforcement(float reward) { FeedbackCalls++; LastFeedback = reward; LearnedSynapseCount++; Memory = "memory-" + LearnedSynapseCount; return 1; }
        public void ResetTransientLearningState() { }
        public void ResetLearnedMemory() { LearnedSynapseCount = 0; Memory = "memory-0"; }
        public string SerializeLearnedMemory() => Memory;
        public bool TryLoadLearnedMemory(string serialized)
        {
            if (String.IsNullOrEmpty(serialized))
            {
                return false;
            }
            Memory = serialized;
            return true;
        }
        public void Stop() { }
    }
#pragma warning restore S1186
#pragma warning restore S2325
    // The production display implements IDisposable; the double retains that lifecycle contract.
#pragma warning disable S3881 // Minimal test double does not own unmanaged resources.
    internal class PersonConnectomeStatusDisplay : IDisposable
    {
        private static int activeCount;
        private static int renderedUpdates;
        public static StatusDisplayBindings LastBindings { get; private set; }
        public static StatusDisplayBodyKind LastBodyKind { get; private set; }
        private bool active;
        public static int ActiveCount => activeCount;
        public static int RenderedUpdates => renderedUpdates;
        public static void ResetForTest() { activeCount = 0; renderedUpdates = 0; LastBindings = null; LastBodyKind = StatusDisplayBodyKind.Person; }
        public PersonConnectomeStatusDisplay(UnityEngine.Transform anchor, ManualInputState manualInput, StatusDisplayBindings bindings = null, UnityEngine.Transform hostRoot = null, int configuredId = 0, StatusDisplayBodyKind bodyKind = StatusDisplayBodyKind.Person)
        {
            LastBindings = bindings;
            LastBodyKind = bodyKind;
        }
        public void Update(float elapsed, LifBrain brain, PeoplePlaygroundPersonAdapter adapter) { if (active) renderedUpdates++; }
        public void Update(float elapsed, LifBrain brain, PeoplePlaygroundFlyAdapter adapter) { if (active) renderedUpdates++; }
        public void SetActive(bool value)
        {
            if (active == value) return;
            active = value;
            activeCount += value ? 1 : -1;
        }
        // Metrics are intentionally ignored by the lifecycle double.
#pragma warning disable S1186 // No-op preserves the production call contract.
        public void RecordTick(float sensorMs, float brainMs, float actuatorMs, float skipped, LifBrain brain) { }
#pragma warning restore S1186
        public void Dispose() { SetActive(false); }
    }
#pragma warning restore S3881
}
#pragma warning restore S101
#pragma warning restore S3903
