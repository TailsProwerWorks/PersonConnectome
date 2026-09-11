using Mod;
using System.Diagnostics;
using UnityEngine;

var tests = new (string Name, Action Run)[]
{
    ("refractory expires after silent ticks", RefractoryExpiresAfterSilentTicks),
    ("deferred backlog preserves propagated input", DeferredBacklogPreservesPropagatedInput),
    ("overload scheduling advances fairly", OverloadSchedulingAdvancesFairly),
    ("fresh sensory inputs bypass recurrent backlog", FreshSensoryInputsBypassRecurrentBacklog),
    ("terminal reset clears state and recovers", TerminalResetClearsStateAndRecovers),
    ("healthy standing leaves sensory headroom", HealthyStateLeavesSensoryHeadroom),
    ("normalized blood and vitality drive injury", NormalizedBloodAndVitalityDriveInjury),
    ("nearby stimulus does not force escape walking", NearbyStimulusDoesNotForceEscapeWalking),
    ("R7 R8 variants receive light drive", RetinaVariantsReceiveLightDrive),
    ("bundled payload identity", BundledPayloadIdentity),
    ("sustained full-payload load reports backlog", SustainedFullPayloadLoadReportsBacklog),
    ("portable SHA-256 vectors and padding boundaries", PayloadChecksumVectors),
    ("malformed dataset and counts reject", MalformedDatasetAndCountsReject)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
    }
}

return failures;

static void RefractoryExpiresAfterSilentTicks()
{
    var brain = OneNeuronBrain();
    brain.SetTestPending(0, 1f);
    brain.Step(Healthy());
    Equal(1, brain.TestFiredCount);

    brain.SetTestPending(0, 1f);
    brain.Step(Healthy());
    Equal(0, brain.TestFiredCount);

    for (var i = 0; i < 4; i++) brain.Step(Healthy());
    brain.SetTestPending(0, 1f);
    brain.Step(Healthy());
    Equal(1, brain.TestFiredCount);
}

static void DeferredBacklogPreservesPropagatedInput()
{
    const int count = 24001;
    var rows = new int[count + 1];
    for (var i = 1; i < rows.Length; i++) rows[i] = 1;
    var brain = ConnectomeBrain.CreateForTest(count, rows, [count - 1], [.5f]);
    brain.SetTestActiveRange(count, 0f);
    brain.SetTestPending(0, 1f);
    brain.SetTestPending(count - 1, .25f);
    brain.Step(Healthy());
    Equal(.75f, brain.TestPendingValue(count - 1));
}

static void OverloadSchedulingAdvancesFairly()
{
    const int count = 24001;
    var brain = ConnectomeBrain.CreateForTest(count, new int[count + 1], [], []);
    brain.SetTestActiveRange(count, .1f);
    brain.Step(Healthy());
    Equal(1, brain.TestDeferredCount);
    Equal(0f, brain.TestPotentialValue(count - 1));

    brain.Step(Healthy());
    Equal(1, brain.TestDeferredCount);
    Equal(.1f, brain.TestPotentialValue(count - 1));
}

static void TerminalResetClearsStateAndRecovers()
{
    var brain = OneNeuronBrain();
    brain.SetTestPending(0, 1f);
    brain.Step(new SensoryFrame { Alive = false });
    Equal(0, brain.TestPendingCount);
    Equal(0L, brain.TestSimulationTick);
    Equal(0L, brain.TestBacklogCursor);
    brain.SetTestPending(0, 1f);
    brain.Step(Healthy());
    Equal(1, brain.TestFiredCount);
}

static void HealthyStateLeavesSensoryHeadroom()
{
    var brain = OneNeuronBrain();
    brain.Step(Healthy(touch: 1f, physicalContact: 1f, heartbeat: 1f));
    var standing = brain.TestSensoryDrive;
    True(standing > 0f && standing < 1f, "healthy standing must retain sensory headroom");
    brain.Step(Healthy(touch: 1f, physicalContact: 1f, heartbeat: 1f, velocity: .2f, sound: .2f));
    True(brain.TestSensoryDrive > standing && brain.TestSensoryDrive < 1f, "movement and sound must modulate healthy standing drive");
}

static void NormalizedBloodAndVitalityDriveInjury()
{
    var brain = OneNeuronBrain();
    brain.Step(new SensoryFrame
    {
        Alive = true,
        Health = 1f,
        Oxygen = 1f,
        Consciousness = 1f,
        Blood = 0f,
        Vitality = 1f,
        Circulation = 1f
    });
    Equal(0f, brain.TestSensoryDrive);

    brain.Step(new SensoryFrame
    {
        Alive = true,
        Health = 1f,
        Oxygen = 1f,
        Consciousness = 1f,
        Blood = 1f,
        Vitality = 0f,
        Circulation = 1f
    });
    Equal(1f, brain.TestSensoryDrive);
}

static void NearbyStimulusDoesNotForceEscapeWalking()
{
    var brain = OneNeuronBrain();
    brain.SetTestPopulation("type:LC4", 0);
    brain.SetTestPending(0, 1f);
    var command = brain.Step(new SensoryFrame
    {
        Alive = true,
        Health = 1f,
        Oxygen = 1f,
        Consciousness = 1f,
        Vitality = 1f,
        Circulation = 1f,
        Nearby = 1f,
        NearbyDirection = 1f
    });
    Equal(0f, command.Walk);
}

static void FreshSensoryInputsBypassRecurrentBacklog()
{
    const int count = 24001;
    var brain = ConnectomeBrain.CreateForTest(count, new int[count + 1], [], []);
    brain.SetTestActiveRange(count, .1f);
    brain.SetTestPopulation("superclass:ol_sensory", count - 1);
    brain.Step(Healthy(touch: 1f));
    True(brain.TestPotentialValue(count - 1) > .1f, "fresh sensory input was deferred behind the recurrent backlog");
}

static void SustainedFullPayloadLoadReportsBacklog()
{
    var brain = ConnectomeBrain.TryCreate(out var status);
    True(brain is not null, status);
    for (var i = 0; i < 20; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        brain.Step(Healthy(velocity: 1f, light: 1f, sound: 1f, touch: 1f, physicalContact: 1f, heartbeat: 1f));
        Console.WriteLine("LOAD step=" + (i + 1) + " ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("0.0") + " active=" + brain.TestActiveCount + " pending=" + brain.TestPendingCount + " processed=" + brain.TestProcessedCount + " deferred=" + brain.TestDeferredCount + " fired=" + brain.TestFiredCount);
    }
    True(brain.TestProcessedCount <= 24000, "processed work exceeded the configured cap");
}

static void MalformedDatasetAndCountsReject()
{
    Throws(() => ConnectomeBrain.ReadAssetForTest(Header("not-male-cns:v1.0", 176422, 6287749, 9619)));
    Throws(() => ConnectomeBrain.ReadAssetForTest(Header("male-cns:v1.0", 1, 6287749, 9619)));
}

static void RetinaVariantsReceiveLightDrive()
{
    var brain = ConnectomeBrain.CreateForTest(2, [0, 0, 0], [], []);
    brain.SetTestPopulation("type:R7d", 0);
    brain.SetTestPopulation("type:R8y", 1);
    brain.Step(Healthy(light: .25f));
    Equal(.25f, brain.TestPotentialValue(0));
    Equal(.25f, brain.TestPotentialValue(1));
}

static void PayloadChecksumVectors()
{
    Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", ConnectomeBrain.PayloadDigestForTest([]));
    Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", ConnectomeBrain.PayloadDigestForTest(System.Text.Encoding.ASCII.GetBytes("abc")));
    var random = new Random(873);
    foreach (var length in new[] { 1, 7, 55, 56, 57, 63, 64, 65, 119, 120, 127, 128, 129, 4096, 1000000 })
    {
        var data = new byte[length];
        random.NextBytes(data);
        var reference = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
        Equal(reference, ConnectomeBrain.PayloadDigestForTest(data));
    }
}

static void BundledPayloadIdentity()
{
    var bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "Mod", "connectome", "malecns-v1.0.flyb.gz"));
    ModAPI.Texture = Carrier(bytes);
    Equal(bytes.Length, ConnectomeBrain.DecodePayloadForTest(ModAPI.Texture).Length);
    var brain = ConnectomeBrain.TryCreate(out var status);
    True(brain is not null, status);
    True(status.Contains("loaded", StringComparison.OrdinalIgnoreCase), status);

    var corrupted = Carrier(bytes);
    var firstPayloadPixel = (corrupted.height - 1) * corrupted.width;
    corrupted.GetPixels32()[firstPayloadPixel].r ^= 1;
    Throws(() => ConnectomeBrain.DecodePayloadForTest(corrupted));
}

static ConnectomeBrain OneNeuronBrain() => ConnectomeBrain.CreateForTest(1, [0, 0], [], []);

static SensoryFrame Healthy(float velocity = 0f, float light = 0f, float sound = 0f, float touch = 0f, float physicalContact = 0f, float heartbeat = 0f) => new()
{
    Alive = true,
    Health = 1f,
    Oxygen = 1f,
    Consciousness = 1f,
    Vitality = 1f,
    Circulation = 1f,
    Velocity = velocity,
    Light = light,
    Sound = sound,
    Touch = touch,
    PhysicalContact = physicalContact,
    Heartbeat = heartbeat
};

static MemoryStream Header(string dataset, uint neurons, uint edges, uint retina)
{
    var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
    {
        writer.Write(new byte[] { (byte)'F', (byte)'L', (byte)'Y', (byte)'B' });
        writer.Write(1u);
        writer.Write(neurons);
        writer.Write(edges);
        writer.Write(retina);
        var bytes = System.Text.Encoding.UTF8.GetBytes(dataset);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }
    stream.Position = 0;
    return stream;
}

static Texture2D Carrier(byte[] payload)
{
    const int width = 4096;
    var height = (payload.Length + 2) / 3 / width + 1;
    var pixels = new Color32[width * height];
    for (var i = 0; i < payload.Length; i++)
    {
        var sourcePixel = i / 3;
        var sourceRow = sourcePixel / width;
        var sourceColumn = sourcePixel % width;
        var unityRow = height - 1 - sourceRow;
        var index = unityRow * width + sourceColumn;
        var pixel = pixels[index];
        if (i % 3 == 0) pixel.r = payload[i];
        else if (i % 3 == 1) pixel.g = payload[i];
        else pixel.b = payload[i];
        pixels[index] = pixel;
    }
    return new Texture2D(width, height, pixels);
}

static string RepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "PersonConnectome.sln"))) current = current.Parent;
    return current?.FullName ?? throw new InvalidOperationException("repository root not found");
}

static void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual)) throw new InvalidOperationException("expected " + expected + ", got " + actual);
}

static void True(bool value, string message = "assertion failed")
{
    if (!value) throw new InvalidOperationException(message);
}

static void Throws(Action action)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }

    throw new InvalidOperationException("expected asset rejection");
}
