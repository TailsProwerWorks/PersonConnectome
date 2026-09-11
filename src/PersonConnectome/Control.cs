namespace PersonConnectome;

public sealed class FixedRateScheduler(float hertz = 20f)
{
    private readonly double _periodSeconds = 1d / NormalizeHertz(hertz);
    private double _accumulator;

    private static float NormalizeHertz(float hertz) => float.IsFinite(hertz) ? Math.Clamp(hertz, 1f, 120f) : 20f;

    public int Advance(double elapsedSeconds, int maximumSteps = 4)
    {
        if (maximumSteps <= 0)
        {
            return 0;
        }

        var elapsed = double.IsNaN(elapsedSeconds) || elapsedSeconds < 0d ? 0d : elapsedSeconds;
        _accumulator = Math.Min(elapsed + _accumulator, _periodSeconds * maximumSteps);
        var steps = 0;
        while (_accumulator >= _periodSeconds && steps < maximumSteps)
        {
            _accumulator -= _periodSeconds;
            steps++;
        }

        return steps;
    }
}

public sealed class MotorCommand
{
    public float Attention { get; init; }
    public float Approach { get; init; }
    public float Avoid { get; init; }
    public float LeftRight { get; init; }
    public float Locomotion { get; init; }
    public float ReachGrab { get; init; }
    public float Flee { get; init; }
    public float Freeze { get; init; }
    public float SeekEnergy { get; init; }
    public float Rest { get; init; }
    // These are intentionally restorative-only chemical requests.  The adapter may
    // ignore them when the target build does not expose a safe public capability.
    public float Heal { get; init; }
    public float Stimulate { get; init; }
    public float Calm { get; init; }
    public float Extinguish { get; init; }
}
public interface IGameCapabilities
{
    SensoryFrame Read();
    bool CanApplySupported { get; }
    void ApplySupported(MotorCommand command);
}

public sealed class PersonConnectomeController(IConnectomeGraph graph, float hertz = 20f)
{
    private readonly LifSimulator _simulator = new(graph);
    private readonly FixedRateScheduler _scheduler = new(hertz);


    public void Update(IGameCapabilities game, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(game);
        var steps = _scheduler.Advance(elapsedSeconds);
        for (var step = 0; step < steps; step++)
        {
            var sensory = game.Read();
            if (sensory.Exists && !sensory.Alive)
            {
                continue;
            }

            _simulator.StepCompact(BuildInput(sensory));
            var desired = BuildCommand(sensory);
            if (game.CanApplySupported) game.ApplySupported(desired);
        }
    }

    private Dictionary<int, float> BuildInput(SensoryFrame sensory)
    {
        Dictionary<int, float> input = [];
        var threat = ThreatSignal(sensory);
        var body = BodySignal(sensory);
        var visual = sensory.Light + sensory.NearbyEntity + sensory.LineOfSight;
        AddPopulationInput(input, "superclass:ol_sensory", threat + body, 512);
        AddPopulationInput(input, "type:R1-R6", visual, 256);
        AddPopulationInput(input, "type:LC4", threat + sensory.NearbyEntity, 128);
        AddPopulationInput(input, "type:LPLC2", threat + sensory.NearbyEntity, 128);
        AddPopulationInput(input, "type:MDN", sensory.Pain + sensory.Damage + sensory.Drowning + sensory.Knockout + sensory.Stun + sensory.Impact, 128);
        AddPopulationInput(input, "type:DNp09", sensory.NearbyEntity * (1f - sensory.Aversive) + sensory.LineOfSight + sensory.Novelty, 128);
        return input;
    }

    private MotorCommand BuildCommand(SensoryFrame sensory)
    {
        var danger = IsDangerous(sensory);
        return new MotorCommand
        {
            Attention = Numbers.Clamp(sensory.Novelty + sensory.NearbyEntity + sensory.LineOfSight + sensory.Sound, 0f, 1f),
            Approach = Numbers.Clamp(sensory.NearbyEntity * (1f - sensory.Aversive), 0f, 1f),
            Avoid = danger ? 1f : 0f,
            LeftRight = Numbers.Clamp(sensory.NearbyDirection.X, -1f, 1f),
            Locomotion = danger ? -1f : NeuralWalk(),
            ReachGrab = Fired("type:MN9") && !danger ? sensory.NearbyEntity : 0f,
            Flee = danger ? 1f : 0f,
            Freeze = Numbers.Clamp(Math.Max(sensory.Sedation, sensory.Knockout), 0f, 1f),
            Rest = Numbers.Clamp(sensory.Fatigue, 0f, 1f),
            Heal = Numbers.Clamp(sensory.Damage + sensory.Bleeding + sensory.Healing, 0f, 1f),
            Stimulate = Numbers.Clamp(sensory.Stimulation + sensory.Reward, 0f, 1f),
            Calm = Numbers.Clamp(sensory.Shock + sensory.Pain, 0f, 1f),
            Extinguish = Numbers.Clamp(sensory.Fire, 0f, 1f)
        };
    }

    private bool IsDangerous(SensoryFrame sensory)
    {
        return ThreatSignal(sensory) > .5f || Fired("type:LC4") || Fired("type:LPLC2");
    }

    private static float ThreatSignal(SensoryFrame sensory)
    {
        var terminal = sensory.Exists && !sensory.Alive ? 1f : 0f;
        return sensory.Pain + sensory.Damage + sensory.Bleeding + sensory.Fire + sensory.Heat + sensory.Cold + sensory.Shock + sensory.Stun + sensory.Knockout + sensory.Impact + sensory.Fall + sensory.Drowning + sensory.Projectile + sensory.MaterialHazard + sensory.Toxicity + sensory.Corrosion + sensory.Infection + sensory.Dismemberment + sensory.Aversive + terminal;
    }

    private static float BodySignal(SensoryFrame sensory)
    {
        return sensory.Grounded + sensory.Contact + sensory.Touch + sensory.Pressure + sensory.Sound + sensory.Vibration + sensory.NearbyEntity + sensory.Orientation + sensory.Balance + sensory.Velocity.Length + sensory.Acceleration.Length + sensory.Hunger + sensory.Thirst + sensory.Energy + sensory.Fatigue + sensory.Air;
    }

    private void AddPopulationInput(Dictionary<int, float> input, string name, float value, int maximum)
    {
        value = Numbers.Clamp(value, 0f, 4f);
        if (value <= .001f)
        {
            return;
        }

        var ids = _simulator.Graph.FindPopulation(name);
        for (var i = 0; i < Math.Min(maximum, ids.Count); i++)
        {
            input[ids[i]] = input.TryGetValue(ids[i], out var current) ? Numbers.Clamp(current + value, -4f, 4f) : value;
        }
    }

    private bool Fired(string name)
    {
        var ids = _simulator.Graph.FindPopulation(name);
        for (var i = 0; i < ids.Count; i++)
        {
            if (_simulator.LastFiredCompactIds.Contains(ids[i]))
            {
                return true;
            }
        }

        return false;
    }

    private float NeuralWalk()
    {
        var left = 0;
        var right = 0;
        foreach (var id in _simulator.LastFiredCompactIds)
        {
            var metadata = _simulator.Graph.GetMetadata(id);
            if (!metadata.Superclass.Contains("descending", StringComparison.OrdinalIgnoreCase) && !metadata.Superclass.Contains("motor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (metadata.Side.Contains('L', StringComparison.OrdinalIgnoreCase))
            {
                left++;
            }

            if (metadata.Side.Contains('R', StringComparison.OrdinalIgnoreCase))
            {
                right++;
            }
        }

        return Numbers.Clamp((right - left) * .25f, -1f, 1f);
    }
}
