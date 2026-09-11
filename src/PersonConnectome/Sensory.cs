namespace PersonConnectome;

public sealed class SensoryFrame
{
    // All values are bounded [0,1] except direction and acceleration.
    public bool Exists { get; init; }
    public bool Alive { get; init; }
    public Vec2 BodyPosition { get; init; }
    public Vec2 Velocity { get; init; }
    public float Orientation { get; init; }
    public float Balance { get; init; }
    public float Grounded { get; init; }
    public float Contact { get; init; }
    public float Touch { get; init; }
    public float Pressure { get; init; }
    public float Damage { get; init; }
    public float Bleeding { get; init; }
    public float Pain { get; init; }
    public float Dismemberment { get; init; }
    public float Heat { get; init; }
    public float Cold { get; init; }
    public float Fire { get; init; }
    public float Shock { get; init; }
    public float Stun { get; init; }
    public float Knockout { get; init; }
    public float Impact { get; init; }
    public float Fall { get; init; }
    public Vec2 Acceleration { get; init; }
    public float Drowning { get; init; }
    public float Air { get; init; }
    public float Hunger { get; init; }
    public float Thirst { get; init; }
    public float Energy { get; init; }
    public float Fatigue { get; init; }
    public float Sound { get; init; }
    public float Vibration { get; init; }
    public float NearbyEntity { get; init; }
    public Vec2 NearbyDirection { get; init; }
    public float LineOfSight { get; init; }
    public float Light { get; init; }
    public float Projectile { get; init; }
    public float MaterialHazard { get; init; }
    public float Wetness { get; init; }
    public float Blood { get; init; }
    public float Toxicity { get; init; }
    public float Corrosion { get; init; }
    public float Sedation { get; init; }
    public float Healing { get; init; }
    public float Stimulation { get; init; }
    public float Infection { get; init; }
    public float Immortality { get; init; }
    public float Reward { get; init; }
    public float Aversive { get; init; }
    public float Novelty { get; init; }
    public float Internal { get; init; }

    public float[] ToChannels() => new[] { Alive ? 1f : 0f, Balance, Grounded, Contact, Damage, Pain, Fire, Shock, Impact, Drowning, Hunger, Thirst, Energy, Fatigue, NearbyEntity, LineOfSight, Light, Wetness, Blood, Toxicity, Corrosion, Sedation, Healing, Stimulation, Infection, Reward, Aversive, Novelty, Internal };
}

public sealed class EffectAliasRegistry
{
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["water"] = "wetness", ["wet"] = "wetness", ["blood"] = "blood", ["acid"] = "corrosion", ["corrosive"] = "corrosion",
        ["poison"] = "toxicity", ["toxin"] = "toxicity", ["knockout"] = "sedation", ["anesthetic"] = "sedation", ["anaesthetic"] = "sedation",
        ["syringe"] = "stimulation", ["serum"] = "stimulation", ["zombie"] = "infection", ["infection"] = "infection",
        ["regeneration"] = "healing", ["healing"] = "healing", ["adrenaline"] = "stimulation", ["stimulant"] = "stimulation",
        ["immortality"] = "immortality", ["death prevention"] = "immortality"
    };
    public IReadOnlyCollection<string> UnknownEffects => _unknown;
    private readonly SortedSet<string> _unknown = new(StringComparer.OrdinalIgnoreCase);
    public string? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = name.Trim();
        if (_aliases.TryGetValue(key, out var mapped)) return mapped;
        _unknown.Add(key.Length <= 64 ? key : key[..64]);
        return null;
    }
    public void AddAlias(string name, string channel) { if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(channel)) _aliases[name.Trim()] = channel.Trim(); }
}
