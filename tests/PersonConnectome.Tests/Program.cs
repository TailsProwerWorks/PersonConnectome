using PersonConnectome;

namespace PersonConnectome.Tests;

internal static class Program
{
    private static IEnumerable<(string Name, Action Run)> GetTests()
    {
        yield return ("determinism", Determinism);
        yield return ("refractory", Refractory);
        yield return ("compact graph", CompactGraph);
        yield return ("metadata populations", MetadataPopulations);
        yield return ("input validation", InputValidation);
        yield return ("state validation", StateValidation);
        yield return ("pending state persistence", PendingStatePersistence);
        yield return ("effect aliases and unknown telemetry", EffectAliasesAndUnknownTelemetry);
        yield return ("controller applies active default", ControllerAppliesActiveDefault);
        yield return ("controller stops terminal state", ControllerStopsTerminalState);
        yield return ("scheduler bound", SchedulerBound);
        yield return ("adapter null-safe categories", AdapterCategories);
        yield return ("game bridge contract", GameBridgeContract);
        yield return ("game bridge shady API guard", GameBridgeShadyApiGuard);
        yield return ("forbidden using guard coverage", ForbiddenUsingGuardCoverage);
        yield return ("bundled MaleCNS asset", BundledMaleCnsAsset);
    }

    public static async Task<int> Main()
    {
        var failures = 0;
        foreach (var (Name, Run) in GetTests())
        {
            try
            {
                Run();
                await Console.Out.WriteLineAsync("PASS " + Name);
            }
            catch (Exception exception)
            {
                failures++;
                await Console.Error.WriteLineAsync("FAIL " + Name + ": " + exception.Message);
            }
        }

        return failures;
    }

    private static void Determinism()
    {
        var graph = FixtureGraph();
        var first = new LifSimulator(graph);
        var second = new LifSimulator(graph);
        for (var i = 0; i < 12; i++)
        {
            var firstResult = first.Step(Input(10, 1.3f));
            var secondResult = second.Step(Input(10, 1.3f));
            Equal(string.Join(',', firstResult), string.Join(',', secondResult));
        }
    }

    private static void Refractory()
    {
        var simulator = new LifSimulator(new SparseGraph([new(1, 1, 2)], []));
        True(simulator.Step(Input(1, 2)).Count == 1);
        True(simulator.Step(Input(1, 2)).Count == 0);
        True(simulator.Step(Input(1, 2)).Count == 0);
    }

    private static void CompactGraph()
    {
        var graph = new SparseGraph([new(999), new(-7)], []);
        True(graph.TryCompactId(-7, out var first) && first == 0);
        True(graph.TryCompactId(999, out var second) && second == 1);
    }

    private static void MetadataPopulations()
    {
        var graph = new SparseGraph(
            [new(10), new(20)],
            [],
            metadata:
            [
                new(10, "R1-R6", "ol_sensory", Side: "L"),
                new(20, "DNp09", "descending_neuron", Side: "R")
            ]);

        True(graph.FindPopulation("type:R1-R6").Count == 1);
        True(graph.FindPopulation("superclass:ol_sensory").Count == 1);
        True(graph.FindPopulation("sensory").Count == 1);
        True(graph.FindPopulation("motor").Count == 1);
        True(graph.FindPopulation("side:R").Count == 1);
    }

    private static void InputValidation()
    {
        Throws<ArgumentNullException>(() =>
        {
            _ = new SparseGraph(null!, []);
        });
        Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new SparseGraph([], [], 0);
        });
        Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new SparseGraph([], [], maximumSynapses: -1);
        });

        var scheduler = new FixedRateScheduler(float.NaN);
        Equal(1, scheduler.Advance(.05));
        Equal(0, scheduler.Advance(double.NaN));
        Equal(0, scheduler.Advance(1, 0));
    }

    private static void StateValidation()
    {
        var simulator = new LifSimulator(FixtureGraph());
        True(LocalStateStore.TrySave(new PersistedState(2, simulator.ExportState()), out var json));
        True(LocalStateStore.TryLoad(json, 5, out _));
        True(!LocalStateStore.TryLoad("{", 5, out _));
        True(!simulator.TryImportState(new SimulatorState(2, 0, new float[1], new int[1], [])));
    }

    private static void PendingStatePersistence()
    {
        var graph = new SparseGraph([new(1), new(2)], [new(1, 2, 2, 2)]);
        var original = new LifSimulator(graph);
        original.Step(Input(1, 2));
        var restored = new LifSimulator(graph);
        True(restored.TryImportState(original.ExportState()));
        Equal(string.Join(',', original.Step()), string.Join(',', restored.Step()));
        Equal(string.Join(',', original.Step()), string.Join(',', restored.Step()));
    }

    private static void EffectAliasesAndUnknownTelemetry()
    {
        var registry = new EffectAliasRegistry();
        foreach (var effect in TestData.KnownEffects)
        {
            True(registry.Resolve(effect) is not null);
        }

        True(registry.Resolve(TestData.UnknownEffectName) is null && registry.UnknownEffects.Contains(TestData.UnknownEffectName));
    }

    private static void ControllerAppliesActiveDefault()
    {
        var game = new CountingGame();
        new PersonConnectomeController(FixtureGraph()).Update(game, 1);
        True(game.ApplyCount > 0);
    }

    private static void ControllerStopsTerminalState()
    {
        var game = new CountingGame { Alive = false };
        new PersonConnectomeController(FixtureGraph()).Update(game, 1);
        Equal(0, game.ApplyCount);
    }

    private static void SchedulerBound()
    {
        var scheduler = new FixedRateScheduler(10);
        Equal(0, scheduler.Advance(.09));
        Equal(1, scheduler.Advance(.02));
        Equal(4, scheduler.Advance(99));
    }

    private static void AdapterCategories()
    {
        var adapter = new ReflectionPersonCapabilities(new Stub());
        var frame = adapter.Read();
        True(frame.Alive && frame.Damage > 0 && frame.Fire > 0 && frame.Wetness > 0 && frame.Contact > 0 && frame.Sedation > 0 && frame.Infection > 0);
        True(adapter.UnknownEffectNames.Contains(TestData.UnknownEffectName));
    }

    private static void GameBridgeContract()
    {
        var script = string.Join("\n", FindModSources().Select(File.ReadAllText));
        foreach (var member in TestData.BridgeMembers)
        {
            True(script.Contains(member, StringComparison.Ordinal));
        }

        True(!script.Contains("ActiveControl", StringComparison.Ordinal));
        True(!script.Contains("OnGUI", StringComparison.Ordinal));
        True(!script.Contains("GUI.", StringComparison.Ordinal));
        True(!script.Contains("F7", StringComparison.Ordinal));
        foreach (var sensor in TestData.WiredGameSensors)
        {
            True(script.Contains("sensory." + sensor, StringComparison.Ordinal), $"game sensor was not routed: {sensor}");
        }
    }

    private static void GameBridgeShadyApiGuard()
    {
        var script = string.Join("\n", FindModSources().Select(File.ReadAllText));
        var bannedPatterns = new[]
        {
            "File.",
            "Directory.",
            "FileStream",
            "Assembly.Load",
            "System.Reflection",
            "Process.Start",
            "DllImport",
            "UnityWebRequest",
            "WebClient",
            "HttpClient",
            "Thread.Start",
            "Socket"
        };

        True(!HasForbiddenGameUsing(script), "game bridge contains a forbidden using directive");
        foreach (var pattern in bannedPatterns)
        {
            True(!script.Contains(pattern, StringComparison.Ordinal), $"game bridge contains restricted API pattern: {pattern}");
        }
    }

    private static bool HasForbiddenGameUsing(string source) => System.Text.RegularExpressions.Regex.IsMatch(source,
        @"\busing\s+(?:(?:static|global)\s+)?(?:global\s*::\s*)?(?:System\s*\.\s*(?:Security|Web)\b|UnityEngine\s*\.\s*Networking\b|Steamworks\b)|\busing\s+\w+\s*=");

    private static void ForbiddenUsingGuardCoverage()
    {
        foreach (var directive in new[]
        {
            "using System.Security.Cryptography;", "using System.Security;", "using System . Security . Cryptography;",
            "using static System.Security.Cryptography.SHA256;", "using global::System.Security.Cryptography;",
            "using System.Web;", "using UnityEngine.Networking;", "using Steamworks;", "using Alias = UnityEngine.Random;"
        })
            True(HasForbiddenGameUsing(directive), "guard missed: " + directive);
        True(!HasForbiddenGameUsing("using System; using System.IO; using System.IO.Compression; using UnityEngine;"));
    }

    private static void BundledMaleCnsAsset()
    {
        using var stream = File.OpenRead(FindAsset());
        True(FlybConnectomeLoader.TryLoad(stream, new ConnectomeLoadOptions(ExpectedSha256: TestData.ExpectedAssetSha256), out var graph, out var failure), failure?.Message ?? "asset load returned false");
        True(graph is not null, "graph was null");
        True(graph!.Count == 176422, $"neuron count was {graph.Count}");
        True(graph.EdgeCount == 6287749, $"edge count was {graph.EdgeCount}");
        True(graph.FindPopulation("superclass:ol_sensory").Count > 0, "sensory population was empty");

        using var bad = File.OpenRead(FindAsset());
        True(!FlybConnectomeLoader.TryLoad(bad, new ConnectomeLoadOptions(ExpectedSha256: "00"), out _, out _));
    }

    private static void True(bool value, string message = "assertion failed")
    {
        if (!value)
        {
            throw new TestAssertionFailure(message);
        }
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestAssertionFailure($"expected {expected}, got {actual}");
        }
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new TestAssertionFailure($"expected {typeof(TException).Name}");
    }

    private static IEnumerable<string> FindModSources()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var modDirectory = Path.Combine(directory.FullName, "Mod");
            if (Directory.Exists(modDirectory))
            {
                return [.. Directory.GetFiles(modDirectory, "*.cs").OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
            }
        }

        throw new DirectoryNotFoundException("Mod source directory was not found from the test working directory.");
    }

    private static Dictionary<int, float> Input(int id, float value) => new() { [id] = value };

    private static SparseGraph FixtureGraph() => new(
        [new(10), new(20), new(30), new(40), new(50)],
        [new(10, 20, 1.2f), new(20, 30, 1.1f), new(20, 40, .8f), new(50, 20, -1f)]);

    private static string FindAsset()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Mod", "connectome", "malecns-v1.0.flyb.gz");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The bundled MaleCNS asset was not found from the test working directory.");
    }
}

internal static class TestData
{
    public const string UnknownEffectName = "future-effect";
    public const string ExpectedAssetSha256 = "E33DF182BED7A6F3EA279DAF4790A82B05706D3D41E819A6A80C0473E8C559F3";
    public static readonly string[] KnownEffects = ["water", "blood", "acid", "poison", "anesthetic", "syringe", "zombie", "regeneration", "adrenaline", "immortality"];
    public static readonly string[] BridgeMembers = ["DesiredWalkingDirection", "InfluenceMotorSpeed", "GripBehaviour", "PersonConnectomeLimbController", "limb.name", "LeftArm", "RightArm", "LeftLeg", "RightLeg", "Core", "Head", "LeftGrip", "RightGrip", "LiveBodySummary", "LiveEnvironmentSummary", "LiveLimbSummary", "DisplayMotorSummary", "PositionLabel", "BloodRegenerationPerSecond", "BurnIntensity", "LiquidDistribution", "entry.Value.Raw", "Liquid.GetIdentity", "GetHeartRate", "Physics2D.OverlapCircleNonAlloc", "ClosestPoint", "GetComponentInParent", "audio.volume", "OwnerRoot", "GunshotWoundCount", "beingHeldByGripper", "TextMeshPro", "Camera.main", "malecns-v1.0.png", "ModAPI.LoadTexture", "texture.height - 1 - sourceRow", "no fallback graph", "adapter.Apply(brain.Step(sensory), true)", "AverageHealth", "BrainDamaged", "Braindead", "Alive", "BrainDead", "IsTerminal", "BRAIN DEAD", "BRAIN INJURED", "BRAIN DEATH", "Stop()", "REQUEST: STOPPED", "NEURAL: STOPPED"];
    public static readonly string[] WiredGameSensors = ["Pain", "Damage", "Bleeding", "Health", "Fire", "Heat", "Cold", "Shock", "Oxygen", "SubmergedHypoxia", "Wetness", "Blood", "Infection", "Charge", "AcidExposure", "LiquidExposure", "LiquidHazard", "LiquidSedation", "LiquidStimulation", "LiquidHealing", "LiquidWater", "Nearby", "NearbyDirection", "Light", "Touch", "Impact", "Sound", "LimbLoss", "Breakage", "JointStress", "Consciousness", "Adrenaline", "Velocity", "Rotation", "Unconscious", "Balance", "Heartbeat", "BrainDamage", "Seizure", "Frozen", "Paralysis", "Numbness", "Vitality", "LungDamage", "InternalBleeding", "Circulation", "Disconnected", "Wounds", "Lava", "BurnProgress", "Stabbed", "PhysicalContact", "Weightless", "Sliding"];
}

internal sealed class Stub
{
    public bool Alive = true;
    public float Damage = .4f;
    public float OnFire = 1;
    public float Wetness = .5f;
    public float Contact = .3f;
    public bool Sedated = true;
    public bool Zombie = true;
    public string[] Effects = ["water", TestData.UnknownEffectName];
}

internal sealed class CountingGame : IGameCapabilities
{
    public int ApplyCount;
    public bool CanApplySupported => true;
    public bool Alive = true;
    public SensoryFrame Read() => new() { Exists = true, Alive = this.Alive };
    public void ApplySupported(MotorCommand command) => ApplyCount++;
}

internal sealed class TestAssertionFailure(string message) : Exception(message);
