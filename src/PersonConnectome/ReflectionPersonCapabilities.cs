using System.Globalization;
using System.Linq;
using System.Reflection;

namespace PersonConnectome;

// Reflection keeps this assembly independent of volatile game API types. Missing members simply yield zero.
public sealed class ReflectionPersonCapabilities(
    object person,
    Action<MotorCommand>? supportedAction = null,
    EffectAliasRegistry? effects = null) : IGameCapabilities
{
    private readonly object _person = person ?? throw new ArgumentNullException(nameof(person));
    private readonly Action<MotorCommand>? _supportedAction = supportedAction;
    private readonly EffectAliasRegistry _effects = effects ?? new();
    public IReadOnlyCollection<string> UnknownEffectNames => _effects.UnknownEffects;
    public bool CanApplySupported => _supportedAction is not null;
    public SensoryFrame Read()
    {
        ObserveEffects();
        return new SensoryFrame
        {
            Exists = true,
            Alive = Bool("Alive", "IsAlive"),
            BodyPosition = Vector("Position", "BodyPosition"),
            Velocity = Vector("Velocity", "LinearVelocity"),
            Orientation = Value("Orientation", "Rotation"),
            Balance = Value("Balance", "Stability"),
            Grounded = FlagOrValue("Grounded", "OnGround"),
            Contact = FlagOrValue("Contact", "Touching"),
            Touch = Value("Touch", "Touched"),
            Pressure = Value("Pressure"),
            Damage = Value("Damage", "Injury"),
            Bleeding = Value("Bleeding", "BleedRate"),
            Pain = Value("Pain"),
            Dismemberment = Value("Dismemberment", "LimbLoss"),
            Fire = FlagOrValue("OnFire", "Burning"),
            Heat = Value("Heat", "Temperature"),
            Cold = Value("Cold", "Freezing"),
            Shock = Value("Shock", "Electricity"),
            Stun = Value("Stun", "Stunned"),
            Knockout = FlagOrValue("Knockout", "Unconscious"),
            Impact = Value("Impact", "CollisionImpact"),
            Fall = Value("Fall", "FallDamage"),
            Acceleration = Vector("Acceleration"),
            Drowning = Value("Drowning", "Suffocating"),
            Air = Value("Oxygen", "Air"),
            Hunger = Value("Hunger"),
            Thirst = Value("Thirst"),
            Energy = Value("Energy", "Stamina"),
            Fatigue = Value("Fatigue", "Tiredness"),
            Sound = Value("Sound", "Noise"),
            Vibration = Value("Vibration"),
            NearbyEntity = FlagOrValue("NearbyEntity", "HasNearbyEntity"),
            NearbyDirection = Vector("NearbyDirection"),
            LineOfSight = FlagOrValue("LineOfSight", "CanSeeTarget"),
            Light = Value("Light", "Illumination"),
            Projectile = FlagOrValue("Projectile", "WasHitByProjectile"),
            MaterialHazard = Value("MaterialHazard", "Hazard"),
            Wetness = Value("Wetness", "Water"),
            Blood = Value("Blood"),
            Toxicity = Value("Toxicity", "Poison"),
            Corrosion = Value("Corrosion", "Acid"),
            Sedation = FlagOrValue("Sedation", "Sedated", "Anesthetic", "Anaesthetic"),
            Healing = Value("Healing", "Regeneration"),
            Stimulation = Value("Stimulation", "Adrenaline", "Serum", "Syringe"),
            Infection = FlagOrValue("Infection", "Zombie", "Infected"),
            Immortality = FlagOrValue("Immortality", "DeathPrevention")
        };
    }
    public void ApplySupported(MotorCommand command)
    {
        _supportedAction?.Invoke(command);
    }
    private bool Bool(params string[] names) => names.Select(Member).Any(raw => raw is bool value && value);
    private float FlagOrValue(params string[] names) => Bool(names) ? 1f : Value(names);
    private float Value(params string[] names)
    {
        foreach (var raw in names.Select(Member))
        {
            if (raw is not null && TryReadFloat(raw, out var value))
            {
                return Vec2.Clamp01(value);
            }
        }

        return 0f;
    }

    private static bool TryReadFloat(object raw, out float value)
    {
        value = 0f;
        if (raw is IConvertible convertible)
        {
            try
            {
                value = Convert.ToSingle(convertible, CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException)
            {
                value = 0f;
            }
            catch (InvalidCastException)
            {
                value = 0f;
            }
        }

        return float.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private Vec2 Vector(params string[] names)
    {
        foreach (var raw in names.Select(Member))
        {
            if (raw is not null && TryReadVector(raw, out var vector))
            {
                return vector;
            }
        }

        return default;
    }

    private static bool TryReadVector(object raw, out Vec2 vector)
    {
        var type = raw.GetType();
        var x = ReadCoordinate(type, raw, "x", "X");
        var y = ReadCoordinate(type, raw, "y", "Y");
        if (x is null || y is null || !TryReadFloat(x, out var xValue) || !TryReadFloat(y, out var yValue))
        {
            vector = default;
            return false;
        }

        vector = new(Vec2.ClampSigned(xValue), Vec2.ClampSigned(yValue));
        return true;
    }

    private static object? ReadCoordinate(Type type, object value, string lowerName, string upperName)
    {
        return type.GetProperty(lowerName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value)
            ?? type.GetProperty(upperName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
    }

    private void ObserveEffects()
    {
        if (Member("Effects") is not System.Collections.IEnumerable values)
        {
            return;
        }

        foreach (var effect in values)
        {
            if (effect is string name)
            {
                _effects.Resolve(name);
                continue;
            }

            if (effect is not null)
            {
                _effects.Resolve(ReadCoordinate(effect.GetType(), effect, "Name", "name")?.ToString());
            }
        }
    }

    private object? Member(string name)
    {
        var type = _person.GetType();
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property is not null)
        {
            return property.GetValue(_person);
        }

        return type.GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(_person);
    }
}
