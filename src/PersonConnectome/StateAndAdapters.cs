using System.Reflection;
using System.Text.Json;

namespace PersonConnectome;

public sealed record PersistedState(int Version, SimulatorState Simulator, bool ObserveOnly);
public static class LocalStateStore
{
    public const int CurrentVersion = 2;
    public const int MaximumBytes = 65_536;
    public static bool TrySave(PersistedState state, out string json) { json = JsonSerializer.Serialize(state); return json.Length <= MaximumBytes; }
    public static bool TryLoad(string? json, int expectedNeurons, out PersistedState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumBytes) return false;
        try { state = JsonSerializer.Deserialize<PersistedState>(json); return state is { Version: CurrentVersion } && state.Simulator.Version == CurrentVersion && state.Simulator.Potential.Length == expectedNeurons && state.Simulator.Refractory.Length == expectedNeurons && state.Simulator.Pending is { Length: <= 4096 }; }
        catch (JsonException) { return false; }
    }
}

// Reflection keeps this assembly independent of volatile game API types. Missing members simply yield zero.
public sealed class ReflectionPersonCapabilities : IGameCapabilities
{
    private readonly object _person;
    private readonly Action<MotorCommand>? _supportedAction;
    private readonly EffectAliasRegistry _effects;
    public IReadOnlyCollection<string> UnknownEffectNames => _effects.UnknownEffects;
    public bool CanApplySupported => _supportedAction is not null;
    public ReflectionPersonCapabilities(object person, Action<MotorCommand>? supportedAction = null, EffectAliasRegistry? effects = null) { _person = person ?? throw new ArgumentNullException(nameof(person)); _supportedAction = supportedAction; _effects = effects ?? new EffectAliasRegistry(); }
    public SensoryFrame Read()
    {
        ObserveEffects();
        return new SensoryFrame
        {
        Exists = true, Alive = Bool("Alive", "IsAlive"), BodyPosition = Vector("Position", "BodyPosition"), Velocity = Vector("Velocity", "LinearVelocity"), Orientation = Value("Orientation", "Rotation"), Balance = Value("Balance", "Stability"), Grounded = FlagOrValue("Grounded", "OnGround"), Contact = FlagOrValue("Contact", "Touching"), Touch = Value("Touch", "Touched"), Pressure = Value("Pressure"), Damage = Value("Damage", "Injury"), Bleeding = Value("Bleeding", "BleedRate"), Pain = Value("Pain"), Dismemberment = Value("Dismemberment", "LimbLoss"), Fire = FlagOrValue("OnFire", "Burning"), Heat = Value("Heat", "Temperature"), Cold = Value("Cold", "Freezing"), Shock = Value("Shock", "Electricity"), Stun = Value("Stun", "Stunned"), Knockout = FlagOrValue("Knockout", "Unconscious"), Impact = Value("Impact", "CollisionImpact"), Fall = Value("Fall", "FallDamage"), Acceleration = Vector("Acceleration"), Drowning = Value("Drowning", "Suffocating"), Air = Value("Oxygen", "Air"), Hunger = Value("Hunger"), Thirst = Value("Thirst"), Energy = Value("Energy", "Stamina"), Fatigue = Value("Fatigue", "Tiredness"), Sound = Value("Sound", "Noise"), Vibration = Value("Vibration"), NearbyEntity = FlagOrValue("NearbyEntity", "HasNearbyEntity"), NearbyDirection = Vector("NearbyDirection"), LineOfSight = FlagOrValue("LineOfSight", "CanSeeTarget"), Light = Value("Light", "Illumination"), Projectile = FlagOrValue("Projectile", "WasHitByProjectile"), MaterialHazard = Value("MaterialHazard", "Hazard"), Wetness = Value("Wetness", "Water"), Blood = Value("Blood"), Toxicity = Value("Toxicity", "Poison"), Corrosion = Value("Corrosion", "Acid"), Sedation = FlagOrValue("Sedation", "Sedated", "Anesthetic", "Anaesthetic"), Healing = Value("Healing", "Regeneration"), Stimulation = Value("Stimulation", "Adrenaline", "Serum", "Syringe"), Infection = FlagOrValue("Infection", "Zombie", "Infected"), Immortality = FlagOrValue("Immortality", "DeathPrevention")
        };
    }
    public void ApplySupported(MotorCommand command) => _supportedAction?.Invoke(command);
    private bool Bool(params string[] names) => names.Select(Member).Any(raw => raw is bool value && value);
    private float FlagOrValue(params string[] names) => Bool(names) ? 1f : Value(names);
    private float Value(params string[] names) { foreach (var raw in names.Select(Member)) if (raw is not null && float.TryParse(raw.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) return Vec2.Clamp01(value); return 0; }
    private Vec2 Vector(params string[] names) { foreach (var raw in names.Select(Member)) { if (raw is null) continue; var type = raw.GetType(); var x = type.GetProperty("x", BindingFlags.Instance | BindingFlags.Public)?.GetValue(raw) ?? type.GetProperty("X", BindingFlags.Instance | BindingFlags.Public)?.GetValue(raw); var y = type.GetProperty("y", BindingFlags.Instance | BindingFlags.Public)?.GetValue(raw) ?? type.GetProperty("Y", BindingFlags.Instance | BindingFlags.Public)?.GetValue(raw); if (x is not null && y is not null && float.TryParse(x.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vx) && float.TryParse(y.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vy)) return new Vec2(Vec2.Signed(vx), Vec2.Signed(vy)); } return default; }
    private void ObserveEffects()
    {
        if (Member("Effects") is not System.Collections.IEnumerable values) return;
        foreach (var effect in values)
        {
            if (effect is string name) _effects.Resolve(name);
            else if (effect is not null) _effects.Resolve(effect.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(effect)?.ToString());
        }
    }
    private object? Member(string name) => _person.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(_person) ?? _person.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(_person);
}
