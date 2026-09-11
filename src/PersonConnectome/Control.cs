namespace PersonConnectome;

public sealed class FixedRateScheduler
{
    private readonly double _periodSeconds;
    private double _accumulator;
    public FixedRateScheduler(float hertz = 20f) { _periodSeconds = 1d / Math.Clamp(hertz, 1f, 120f); }
    public int Advance(double elapsedSeconds, int maximumSteps = 4)
    {
        _accumulator = Math.Min(Math.Max(0, elapsedSeconds) + _accumulator, _periodSeconds * maximumSteps);
        var steps = 0; while (_accumulator >= _periodSeconds && steps < maximumSteps) { _accumulator -= _periodSeconds; steps++; } return steps;
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
public sealed class SafeMotorGate
{
    private MotorCommand _previous = new();
    public bool ObserveOnly { get; set; }
    public bool EmergencyDisabled { get; private set; }
    public void EmergencyDisable() => EmergencyDisabled = true;
    public void ResetEmergency() { EmergencyDisabled = false; _previous = new(); }
    public MotorCommand Filter(MotorCommand? desired, float smoothing = .2f)
    {
        if (ObserveOnly || EmergencyDisabled || desired is null) return _previous = new();
        var a = Numbers.Clamp(smoothing, .01f, 1f);
        return _previous = new MotorCommand { Attention = Numbers.Smooth(_previous.Attention, Vec2.Clamp01(desired.Attention), a), Approach = Numbers.Smooth(_previous.Approach, Vec2.Clamp01(desired.Approach), a), Avoid = Numbers.Smooth(_previous.Avoid, Vec2.Clamp01(desired.Avoid), a), LeftRight = Numbers.Smooth(_previous.LeftRight, Vec2.Signed(desired.LeftRight), a), Locomotion = Numbers.Smooth(_previous.Locomotion, Vec2.Signed(desired.Locomotion), a), ReachGrab = Numbers.Smooth(_previous.ReachGrab, Vec2.Clamp01(desired.ReachGrab), a), Flee = Numbers.Smooth(_previous.Flee, Vec2.Clamp01(desired.Flee), a), Freeze = Numbers.Smooth(_previous.Freeze, Vec2.Clamp01(desired.Freeze), a), SeekEnergy = Numbers.Smooth(_previous.SeekEnergy, Vec2.Clamp01(desired.SeekEnergy), a), Rest = Numbers.Smooth(_previous.Rest, Vec2.Clamp01(desired.Rest), a), Heal = Numbers.Smooth(_previous.Heal, Vec2.Clamp01(desired.Heal), a), Stimulate = Numbers.Smooth(_previous.Stimulate, Vec2.Clamp01(desired.Stimulate), a), Calm = Numbers.Smooth(_previous.Calm, Vec2.Clamp01(desired.Calm), a), Extinguish = Numbers.Smooth(_previous.Extinguish, Vec2.Clamp01(desired.Extinguish), a) };
    }
}

public interface IGameCapabilities
{
    SensoryFrame Read();
    bool CanApplySupported { get; }
    void ApplySupported(MotorCommand command);
}

public sealed class PersonConnectomeController
{
    private readonly LifSimulator _simulator;
    private readonly FixedRateScheduler _scheduler;
    private readonly SafeMotorGate _gate;
    public PersonConnectomeController(SparseGraph? graph = null, float hertz = 20f) { _simulator = new LifSimulator(graph ?? DemoCircuit.Create()); _scheduler = new FixedRateScheduler(hertz); _gate = new SafeMotorGate(); }
    public SafeMotorGate Safety => _gate;
    public void Update(IGameCapabilities game, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(game);
        foreach (var _ in Enumerable.Range(0, _scheduler.Advance(elapsedSeconds)))
        {
            var s = game.Read(); var input = new Dictionary<int, float> { [10] = s.Pain + s.Damage + s.Bleeding + s.Fire + s.Shock + s.Drowning + s.Aversive, [50] = s.Knockout + s.Sedation, [20] = s.NearbyEntity + s.Light + s.Sound + s.Touch };
            var fired = _simulator.Step(input);
            var danger = fired.Contains(30) || s.Pain + s.Fire + s.Drowning + s.Shock > .5f;
            var desired = new MotorCommand { Attention = s.Novelty + s.NearbyEntity, Approach = s.NearbyEntity * (1f - s.Aversive), Avoid = danger ? 1 : 0, LeftRight = s.NearbyDirection.X < 0 ? 1 : -1, Locomotion = danger ? -1 : s.NearbyEntity, ReachGrab = s.NearbyEntity * (1f - s.Aversive), Flee = danger ? 1 : 0, Freeze = Math.Max(s.Sedation, s.Knockout), Rest = s.Fatigue, Heal = s.Damage + s.Bleeding, Stimulate = s.Sedation + s.Knockout, Calm = s.Shock + s.Pain, Extinguish = s.Fire };
            var command = _gate.Filter(desired);
            if (!_gate.ObserveOnly && !_gate.EmergencyDisabled && game.CanApplySupported) game.ApplySupported(command);
        }
    }
}
