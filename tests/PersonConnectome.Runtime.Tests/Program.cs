using Mod;
using System.Diagnostics;
using UnityEngine;

if (args.Contains("--benchmark"))
{
    BundledPayloadIdentity();
    BenchmarkScheduler();
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("refractory expires after silent ticks", RefractoryExpiresAfterSilentTicks),
    ("refractory propagation resumes on exact recovery tick", RefractoryPropagationRecovers),
    ("same-tick refractory targets do not consume the budget", SameTickRefractoryTargets),
    ("refractory work cannot starve fresh input", RefractoryWorkCannotStarveFreshInput),
    ("sensory drive resumes on exact refractory recovery", SensoryRefractoryRecovery),
    ("overload preserves fresh propagated input", OverloadPreservesFreshPropagatedInput),
    ("overload drops stale work for realtime control", OverloadDropsStaleWorkForRealtimeControl),
    ("overload cursor includes interspersed priority positions", OverloadCursorIncludesInterspersedPriorityPositions),
    ("fresh sensory inputs bypass recurrent backlog", FreshSensoryInputsBypassRecurrentBacklog),
    ("terminal reset clears state and recovers", TerminalResetClearsStateAndRecovers),
    ("healthy standing leaves sensory headroom", HealthyStateLeavesSensoryHeadroom),
    ("injury and native adrenaline do not synthesize endocrine commands", NativeStressDoesNotDriveChemistry),
    ("liquid exposure and native zombie state reach neural populations", LiquidExposureReachesNeurons),
    ("normalized blood and vitality drive injury", NormalizedBloodAndVitalityDriveInjury),
    ("submersion without neural motor activity is still", SubmersionWithoutNeuralMotorActivityIsStill),
    ("supported shallow water keeps normal control", SupportedShallowWaterKeepsNormalControl),
    ("hazard cannot force walking without motor activity", HazardCannotForceWalkingWithoutMotorActivity),
    ("lost movement authority clears motor requests", LostMovementAuthorityClearsMotorRequests),
    ("motor reversals are rate limited", MotorReversalsAreRateLimited),
    ("R7 R8 variants receive light drive", RetinaVariantsReceiveLightDrive),
    ("bundled payload identity", BundledPayloadIdentity),
    ("real graph repeats identical input histories deterministically", RealGraphIsDeterministic),
    ("sustained full-payload load stays realtime bounded", SustainedFullPayloadLoadStaysRealtimeBounded),
    ("portable SHA-256 vectors and padding boundaries", PayloadChecksumVectors),
    ("malformed dataset and counts reject", MalformedDatasetAndCountsReject),
    ("soma sample retains real IDs and omits missing positions", SomaSample),
    ("diagnostic spikes match actual runtime and reset", DiagnosticSpikes),
    ("telemetry fits supported screens with corner clearance", TelemetryFitsScreens),
    ("telemetry drag stays reachable and resizing can shrink", TelemetryDragAndResize)
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

static void TelemetryFitsScreens()
{
    foreach (var (width, height) in new[] { (800, 600), (1280, 720), (1920, 1080), (3840, 2160) })
    {
        foreach (var textScale in new[] { .8f, 1f, 1.6f })
        {
            var expanded = TelemetryLayout.Create(width, height, textScale, false);
            var collapsed = TelemetryLayout.Create(width, height, textScale, true);
            var cornerSpace = (width - expanded.Width * expanded.Scale) / 2f;
            if (cornerSpace < 159.9f) throw new Exception("Telemetry covers the reserved screen corners.");
            if (expanded.Height * expanded.Scale > height - 15.9f) throw new Exception("Telemetry extends off-screen.");
            if (expanded.Width < 399.9f || expanded.Height < 359.9f) throw new Exception("Controls or viewport lose their minimum space.");
            if (collapsed.Width != expanded.Width || collapsed.Scale != expanded.Scale || collapsed.Height != 48f)
                throw new Exception("Collapse moves or resizes the header.");
        }
    }
}

static void BenchmarkScheduler()
{
    for (var run = 1; run <= 3; run++)
    {
        var brain = ConnectomeBrain.TryCreate(out var status);
        True(brain is not null, status);
        var sensory = Healthy(velocity: 1f, light: 1f, sound: 1f, touch: 1f, physicalContact: 1f, heartbeat: 1f);
        for (var tick = 0; tick < 30; tick++) brain.Step(sensory);
        var times = new double[120];
        long processed = 0, dropped = 0, refractory = 0, deferred = 0;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < times.Length; tick++)
        {
            var started = Stopwatch.GetTimestamp();
            brain.Step(sensory);
            times[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            processed += brain.ProcessedCount;
            dropped += brain.DroppedCount;
            refractory += brain.TestRefractoryActiveCount;
            deferred += brain.ActiveCount - brain.PendingCount;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Array.Sort(times);
        Console.WriteLine(FormattableString.Invariant($"BENCH run={run} meanMs={times.Average():0.00} p95Ms={times[113]:0.00} allocatedBytes={allocated} processed={processed} dropped={dropped} nextRefractory={refractory} nextWithoutInput={deferred}"));
    }
}

static void TelemetryDragAndResize()
{
    foreach (var (width, height) in new[] { (800, 600), (1280, 720), (1920, 1080), (3840, 2160) })
    {
        foreach (var textScale in new[] { .8f, 1f, 1.6f })
        {
            var normal = TelemetryLayout.Create(width, height, textScale, false);
            var small = TelemetryLayout.Create(width, height, textScale, false, 360f, 300f);
            True(small.Width * small.Scale < normal.Width * normal.Scale && small.Height * small.Scale < normal.Height * normal.Scale, "resize cannot shrink the panel");
            Equal(normal.Scale, small.Scale);
            foreach (var offset in new[] { -100000f, 0f, 100000f })
            {
                var x = small.ClampHorizontal(width, offset);
                var y = small.ClampVertical(height, offset);
                var half = small.Width * small.Scale * .5f;
                True(width * .5f + x - half >= 7.9f && width * .5f + x + half <= width - 7.9f, "drag loses a horizontal edge");
                True(y >= 7.9f && y + small.Height * small.Scale <= height - 7.9f, "drag loses the header or resize handle");
            }
        }
    }
}

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

static void OverloadPreservesFreshPropagatedInput()
{
    const int count = 24001;
    var rows = new int[count + 1];
    for (var i = 1; i < rows.Length; i++) rows[i] = 1;
    var brain = ConnectomeBrain.CreateForTest(count, rows, [count - 1], [.5f]);
    brain.SetTestActiveRange(count, 0f);
    brain.SetTestPending(0, 1f);
    brain.SetTestPending(count - 1, .25f);
    brain.Step(Healthy());
    Equal(.5f, brain.TestPendingValue(count - 1));
}

static void OverloadDropsStaleWorkForRealtimeControl()
{
    const int count = 24001;
    var brain = ConnectomeBrain.CreateForTest(count, new int[count + 1], [], []);
    brain.SetTestActiveRange(count, .1f);
    brain.Step(Healthy());
    Equal(1, brain.TestDroppedCount);
    Equal(0f, brain.TestPotentialValue(count - 1));

    brain.Step(Healthy());
    Equal(0, brain.TestDroppedCount);
    Equal(0f, brain.TestPotentialValue(count - 1));
}

static void OverloadCursorIncludesInterspersedPriorityPositions()
{
    const int count = 24001;
    var brain = ConnectomeBrain.CreateForTest(count, new int[count + 1], [], []);
    brain.SetTestActiveRange(count, .1f);
    brain.SetTestPopulation("superclass:ol_sensory", 12000);
    brain.Step(Healthy(touch: 1f));
    Equal(24000L, brain.TestBacklogCursor);
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
    brain.SetTestNeuronMetadata(0, "descending", "R");
    var moving = brain.Step(Healthy());
    Equal(1, brain.TestFiredCount);
    True(moving.Walk > 0f, "motor activity should create a request before terminal reset");
    var stopped = brain.Step(new SensoryFrame { Alive = true, BrainDead = true });
    Equal(0f, stopped.Walk);
    True(brain.IsStopped, "brain-dead state must stop the runtime");
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
        HealthValid = true,
        Health = 1f,
        Oxygen = 1f,
        OxygenValid = true,
        Consciousness = 1f,
        ConsciousnessValid = true,
        Blood = 0f,
        Vitality = 1f,
        Circulation = 1f
    });
    Equal(0f, brain.TestSensoryDrive);

    brain.Step(new SensoryFrame
    {
        Alive = true,
        HealthValid = true,
        Health = 1f,
        Oxygen = 1f,
        OxygenValid = true,
        Consciousness = 1f,
        ConsciousnessValid = true,
        Blood = 1f,
        BloodValid = true,
        Vitality = 0f,
        VitalityValid = true,
        Circulation = 1f
    });
    Equal(1f, brain.TestSensoryDrive);

    brain.Step(new SensoryFrame
    {
        Alive = true,
        Vitality = 1f
    });
    Equal(0f, brain.TestSensoryDrive);
}

static void HazardCannotForceWalkingWithoutMotorActivity()
{
    var brain = OneNeuronBrain();
    brain.SetTestPopulation("type:LC4", 0);
    brain.SetTestPending(0, 1f);
    var command = brain.Step(new SensoryFrame
    {
        Alive = true,
        HealthValid = true,
        Health = 1f,
        Oxygen = 1f,
        OxygenValid = true,
        Consciousness = 1f,
        ConsciousnessValid = true,
        Vitality = 1f,
        Circulation = 1f,
        Nearby = 1f,
        NearbyDirection = 1f,
        Fire = 1f
    });
    Equal(0f, command.Walk);
}

static void SubmersionWithoutNeuralMotorActivityIsStill()
{
    var brain = OneNeuronBrain();
    var sensory = new SensoryFrame
    {
        Alive = true,
        HealthValid = true,
        Health = 1f,
        Oxygen = .93f,
        OxygenValid = true,
        Consciousness = 1f,
        ConsciousnessValid = true,
        Vitality = 1f,
        Circulation = 1f,
        UnderWater = 1f,
        SubmergedHypoxia = .07f
    };

    var command = brain.Step(sensory);
    Equal(0f, command.Walk);
    Equal(0f, command.LeftArm);
    Equal(0f, command.RightArm);
    Equal(0f, command.LeftLeg);
    Equal(0f, command.RightLeg);
    Equal(0f, command.Avoid);
    True(brain.TestSensoryDrive > 0f, "hypoxia remains available to the neural input drive");
}

static void MotorReversalsAreRateLimited()
{
    var brain = ConnectomeBrain.CreateForTest(2, [0, 0, 0], [], []);
    brain.SetTestNeuronMetadata(0, "descending", "R");
    brain.SetTestNeuronMetadata(1, "descending", "L");
    brain.SetTestPending(0, 1f);
    var forward = brain.Step(Healthy(), .05f);
    brain.SetTestPending(1, 1f);
    var reversing = brain.Step(Healthy(), .05f);
    True(forward.Walk > 0f, "right-side descending activity should request forward movement");
    True(MathF.Abs(reversing.Walk - forward.Walk) <= .4001f, "motor reversal exceeded the per-step rate limit");
    True(reversing.Walk >= 0f, "a one-tick reversal should pass through neutral instead of flipping sign");
}

static void LostMovementAuthorityClearsMotorRequests()
{
    AssertImmediateMotorClear(frame =>
    {
        frame.ConsciousnessValid = false;
        return frame;
    });
    AssertImmediateMotorClear(frame =>
    {
        frame.Consciousness = .7f;
        frame.Unconscious = .3f;
        return frame;
    });
    AssertImmediateMotorClear(frame =>
    {
        frame.LiquidSedation = .5f;
        return frame;
    });
}

static void AssertImmediateMotorClear(Func<SensoryFrame, SensoryFrame> restrict)
{
    var brain = OneNeuronBrain();
    brain.SetTestNeuronMetadata(0, "descending", "R");
    brain.SetTestPending(0, 1f);
    True(brain.Step(Healthy()).Walk > 0f, "test setup did not produce a motor request");
    var command = brain.Step(restrict(Healthy()));
    Equal(0f, command.Walk);
    Equal(0f, command.LeftArm);
    Equal(0f, command.RightArm);
    Equal(0f, command.LeftLeg);
    Equal(0f, command.RightLeg);
    Equal(0f, command.Core);
    Equal(0f, command.Head);
    Equal(0f, command.ReachGrab);
    Equal(0f, command.LeftGrip);
    Equal(0f, command.RightGrip);
}

static void SupportedShallowWaterKeepsNormalControl()
{
    var brain = OneNeuronBrain();
    var command = brain.Step(new SensoryFrame
    {
        Alive = true,
        HealthValid = true,
        Health = 1f,
        Oxygen = 1f,
        OxygenValid = true,
        Consciousness = 1f,
        ConsciousnessValid = true,
        Vitality = 1f,
        Circulation = 1f,
        UnderWater = 1f,
        Touch = 1f
    });
    Equal(0f, command.Avoid);
    Equal(0f, command.Walk);
    Equal(0f, command.LeftArm);
    Equal(0f, command.RightArm);
}

static void FreshSensoryInputsBypassRecurrentBacklog()
{
    const int count = 24001;
    var brain = ConnectomeBrain.CreateForTest(count, new int[count + 1], [], []);
    brain.SetTestActiveRange(count, .1f);
    brain.SetTestPopulation("superclass:ol_sensory", count - 1);
    brain.Step(Healthy(touch: 1f));
    True(brain.TestPotentialValue(count - 1) > .1f, "fresh sensory input was dropped behind the recurrent load");
}

static void RealGraphIsDeterministic()
{
    var first = ConnectomeBrain.TryCreate(out var status);
    var second = ConnectomeBrain.TryCreate(out _);
    True(first is not null && second is not null, status);
    for (var tick = 0; tick < 12; tick++)
    {
        var input = Healthy(light: tick < 6 ? .2f : 1f, sound: tick % 3 == 0 ? .5f : 0f, touch: 1f, heartbeat: 1f);
        var a = first.Step(input);
        var b = second.Step(input);
        Equal(a.Walk, b.Walk); Equal(a.LeftArm, b.LeftArm); Equal(a.RightArm, b.RightArm);
        Equal(a.LeftLeg, b.LeftLeg); Equal(a.RightLeg, b.RightLeg); Equal(a.Core, b.Core); Equal(a.Head, b.Head);
        Equal(first.ProcessedCount, second.ProcessedCount); Equal(first.DroppedCount, second.DroppedCount);
        for (var id = 0; id < first.BrainMap.NeuronCount; id++)
            True(first.DidFire(id) == second.DidFire(id), "identical native-frame histories produced different spikes");
    }
}

static void LiquidExposureReachesNeurons()
{
    foreach (var nativeZombie in new[] { false, true })
    {
        var brain = OneNeuronBrain();
        brain.SetTestPopulation("superclass:ol_sensory", 0);
        var sensory = Healthy();
        if (nativeZombie) sensory.Infection = .4f;
        else sensory.LiquidExposure = .4f;
        var command = brain.Step(sensory);
        True(brain.TestPotentialValue(0) > 0f || brain.TestFiredCount > 0, "liquid/zombie input did not reach the authoritative neural router");
        Equal(0f, command.Walk); Equal(0f, command.Calm); Equal(0f, command.Stimulate);
        True(brain.TestSensoryDrive > 0f, "exposure input must not be visual-only");
    }
}

static void NativeStressDoesNotDriveChemistry()
{
    var brain = OneNeuronBrain();
    var sensory = Healthy();
    sensory.Pain = 1f; sensory.Shock = 1f; sensory.Adrenaline = 1f;
    sensory.DamageValid = true; sensory.Damage = .5f; sensory.LimbLoss = .5f;
    var command = brain.Step(sensory);
    True(brain.TestSensoryDrive > 0f, "injury must still reach the neural input path");
    Equal(0f, command.Calm); Equal(0f, command.Stimulate);
    sensory.LiquidStimulation = .4f; sensory.LiquidSedation = .2f;
    command = brain.Step(sensory);
    Equal(.4f, command.Stimulate); Equal(.2f, command.Calm);
}

static void SustainedFullPayloadLoadStaysRealtimeBounded()
{
    var brain = ConnectomeBrain.TryCreate(out var status);
    True(brain is not null, status);
    for (var i = 0; i < 20; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        brain.Step(Healthy(velocity: 1f, light: 1f, sound: 1f, touch: 1f, physicalContact: 1f, heartbeat: 1f));
        Console.WriteLine("LOAD step=" + (i + 1) + " ms=" + stopwatch.Elapsed.TotalMilliseconds.ToString("0.0") + " active=" + brain.TestActiveCount + " pending=" + brain.TestPendingCount + " processed=" + brain.TestProcessedCount + " dropped=" + brain.TestDroppedCount + " fired=" + brain.TestFiredCount);
        True(brain.TestProcessedCount <= 24000, "processed work exceeded the configured cap");
    }
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
    True(brain.BrainMap != null, "real asset soma map unavailable");
    Equal(176422, brain.BrainMap.NeuronCount);
    Equal(8192, brain.BrainMap.Points.Length);
    Console.WriteLine("SOMA located=" + brain.BrainMap.LocatedCount + " sampled=" + brain.BrainMap.Points.Length);

    var corrupted = Carrier(bytes);
    var firstPayloadPixel = (corrupted.height - 1) * corrupted.width;
    corrupted.GetPixels32()[firstPayloadPixel].r ^= 1;
    Throws(() => ConnectomeBrain.DecodePayloadForTest(corrupted));
}

static void SomaSample()
{
    var sample = BrainMapSample.Create([1, 2, 3, float.NaN, 2, 3, 4, float.PositiveInfinity, 6, 7, 8, 9], ["ol_sensory", "", "", "vnc_intrinsic"]);
    Equal(2, sample.LocatedCount);
    Equal(0, sample.Points[0].NeuronId);
    Equal(3, sample.Points[1].NeuronId);
    Equal(7f, sample.Points[1].X);
    Equal(9f, sample.Points[1].Z);
    Equal(2, sample.Points[1].Category);
    Equal(0, BrainMapSample.Create([float.NaN, 0, 0], [""]).Points.Length);
    var soma = new float[20000 * 3];
    for (var i = 0; i < soma.Length; i++) soma[i] = i;
    sample = BrainMapSample.Create(soma, new string[20000]);
    Equal(8192, sample.Points.Length);
    Equal(20000, sample.LocatedCount);
    for (var i = 1; i < sample.Points.Length; i++)
    {
        True(sample.Points[i].NeuronId > sample.Points[i - 1].NeuronId);
        Equal(soma[sample.Points[i].NeuronId * 3], sample.Points[i].X);
    }
}

static void DiagnosticSpikes()
{
    var brain = OneNeuronBrain();
    brain.SetTestPopulation("test", 0);
    brain.SetTestPending(0, 1f);
    brain.Step(Healthy());
    Equal(1, brain.PopulationCount("test"));
    Equal(1, brain.PopulationFiredCount("test"));
    True(brain.DidFire(0));
    True(!brain.DidFire(-1) && !brain.DidFire(1));
    brain.Step(default);
    Equal(0, brain.FiredCount);
    Equal(0, brain.DroppedCount);
    Equal(0, brain.PopulationFiredCount("test"));
    True(!brain.DidFire(0));
    brain = ConnectomeBrain.CreateForTest(24001, new int[24002], [], []);
    brain.SetTestActiveRange(24001, .1f);
    brain.Step(Healthy());
    True(brain.DroppedCount > 0);
    brain.Step(default);
    Equal(0, brain.DroppedCount);
}

static void RefractoryPropagationRecovers()
{
    // Source 1 targets neuron 0; only arrivals on tick 7 may reach a
    // neuron that fired on tick 1 (five intervening refractory ticks).
    for (var sourceTick = 2; sourceTick <= 6; sourceTick++)
    {
        var brain = ConnectomeBrain.CreateForTest(2, [0, 0, 1], [0], [1f]);
        brain.SetTestPending(0, 1f);
        brain.Step(Healthy());
        Equal(0, brain.ActiveCount); // A firing neuron must not requeue itself.
        while (brain.SimulationTick < sourceTick - 1) brain.Step(Healthy());
        brain.SetTestPending(1, 1f);
        brain.Step(Healthy());
        Equal(sourceTick == 6 ? 1 : 0, brain.PendingCount);
        brain.Step(Healthy());
        Equal(sourceTick == 6 ? 1 : 0, brain.FiredCount);
    }
}

static void SameTickRefractoryTargets()
{
    var brain = ConnectomeBrain.CreateForTest(2, [0, 1, 1], [1], [.5f]);
    brain.SetTestPending(0, 1f);
    brain.SetTestPending(1, 1f);
    brain.Step(Healthy());
    Equal(2, brain.FiredCount);
    Equal(0, brain.PendingCount);
    Equal(0, brain.ActiveCount);
    brain.Step(Healthy());
    Equal(0, brain.ProcessedCount);
    Equal(0, brain.DroppedCount);
    Equal(0, brain.FiredCount);
}

static void RefractoryWorkCannotStarveFreshInput()
{
    var brain = ConnectomeBrain.CreateForTest(24001, new int[24002], [], []);
    brain.SetTestActiveRange(24000, 1f);
    brain.Step(Healthy());
    Equal(24000, brain.FiredCount);
    brain.SetTestActiveRange(24001, .1f);
    brain.Step(Healthy());
    Equal(1, brain.ProcessedCount);
    Equal(0, brain.DroppedCount);
    Equal(.1f, brain.TestPotentialValue(24000));
}

static void SensoryRefractoryRecovery()
{
    var brain = OneNeuronBrain();
    brain.SetTestPopulation("superclass:ol_sensory", 0);
    var stimulus = Healthy(sound: 1f, touch: 1f, physicalContact: 1f, heartbeat: 1f);
    brain.Step(stimulus);
    Equal(1, brain.FiredCount);
    for (var tick = 2; tick <= 6; tick++)
    {
        brain.Step(stimulus);
        Equal(0, brain.ProcessedCount);
        Equal(0, brain.FiredCount);
    }
    brain.Step(stimulus);
    Equal(1, brain.FiredCount);
}

static ConnectomeBrain OneNeuronBrain() => ConnectomeBrain.CreateForTest(1, [0, 0], [], []);

static SensoryFrame Healthy(float velocity = 0f, float light = 0f, float sound = 0f, float touch = 0f, float physicalContact = 0f, float heartbeat = 0f) => new()
{
    Alive = true,
    HealthValid = true,
    Health = 1f,
    Oxygen = 1f,
    OxygenValid = true,
    Consciousness = 1f,
    ConsciousnessValid = true,
    Vitality = 1f,
    VitalityValid = true,
    DamageValid = true,
    BloodValid = true,
    Circulation = 1f,
    CirculationValid = true,
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
