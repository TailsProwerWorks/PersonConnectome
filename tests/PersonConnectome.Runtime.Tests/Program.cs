using Mod;
using System.Diagnostics;
using UnityEngine;

if (args.Contains("--benchmark"))
{
    BundledPayloadIdentity();
    BenchmarkScheduler();
    return 0;
}

if (args.Contains("--mapping-report"))
{
    MappingReport();
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("signed input accumulation is order independent", SignedInputAccumulationIsOrderIndependent),
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
    ("invalid health suspends without resetting neural state", InvalidHealthSuspendsWithoutReset),
    ("healthy standing leaves sensory headroom", HealthyStateLeavesSensoryHeadroom),
    ("injury and native adrenaline do not synthesize endocrine commands", NativeStressDoesNotDriveChemistry),
    ("internal chemistry does not fabricate a sensory receptor", InternalStatesDoNotFabricateReceptors),
    ("blood and vitality do not fabricate visual input", NormalizedBloodAndVitalityDriveInjury),
    ("submersion without neural motor activity is still", SubmersionWithoutNeuralMotorActivityIsStill),
    ("supported shallow water keeps normal control", SupportedShallowWaterKeepsNormalControl),
    ("hazard cannot force walking without motor activity", HazardCannotForceWalkingWithoutMotorActivity),
    ("lost movement authority clears motor requests", LostMovementAuthorityClearsMotorRequests),
    ("motor reversals are rate limited", MotorReversalsAreRateLimited),
    ("R7 R8 variants receive light drive", RetinaVariantsReceiveLightDrive),
    ("bundled payload identity", BundledPayloadIdentity),
    ("bundled sensory and locomotor annotations resolve", BundledSensoryMappings),
    ("modalities reach distinct input populations with signed lateralization", SensoryModalities),
    ("global luminance changes require a valid continuous baseline", GlobalLuminanceChanges),
    ("damage events and regional contact stay in their tactile routes", InjuryAndRegionalRoutes),
    ("geometric visual channels reject unavailable and nonfinite features", GeometricVisualRoutes),
    ("auditory band weighting uses measured bands or broad fallback", AuditoryBandRouting),
    ("neural locomotion modes bridge pulses and expire without input", LocomotionTemporalBehavior),
    ("turning populations map lateral activity with immediate safety clearing", NeuralTurning),
    ("named locomotor populations exclude feeding and wing activity", NamedMotorReadout),
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

static void MappingReport()
{
    BundledPayloadIdentity();
    Console.WriteLine("Fresh real graph per case; 40 ticks at 0.05 game seconds. Amplitudes are engineered, not Hz.");
    var cases = new (string Name, string Population, Action<SensoryFrameBox> Configure)[]
    {
        ("quiet", "input:light", _ => { }),
        ("light", "input:light", x => x.Frame.Light = 1f),
        ("light-on", "type:Mi1", x => x.Frame.Light = 1f),
        ("light-off", "type:L2", x => x.Frame.Light = 0f),
        ("audio-left", "input:auditory", x => { x.Frame.Sound = 1f; x.Frame.SoundDirection = -1f; x.Frame.SoundDirectionValid = true; }),
        ("audio-right", "input:auditory", x => { x.Frame.Sound = 1f; x.Frame.SoundDirection = 1f; x.Frame.SoundDirectionValid = true; }),
        ("impact", "input:tactile", x => x.Frame.Impact = 1f),
        ("damage-half-pulse", "input:touch-head", x => x.Frame.DamageEvent = .5f),
        ("damage-full-pulse", "input:touch-head", x => x.Frame.DamageEvent = 1f),
        ("head-contact", "input:touch-head", x => { x.Frame.RegionalTouchValid = true; x.Frame.TouchHead = 1f; }),
        ("arm-contact", "input:touch-arms", x => { x.Frame.RegionalTouchValid = true; x.Frame.TouchArms = 1f; }),
        ("leg-contact", "input:touch-legs", x => { x.Frame.RegionalTouchValid = true; x.Frame.TouchLegs = 1f; }),
        ("core-contact", "input:touch-core", x => { x.Frame.RegionalTouchValid = true; x.Frame.TouchCore = 1f; }),
        ("loom", "type:LPLC2", x => { x.Frame.Vision = 1f; x.Frame.VisualGeometryValid = true; x.Frame.VisualAngularSize = 60f; x.Frame.VisualExpansion = 200f; }),
        ("small-moving", "type:LC11", x => { x.Frame.Vision = 1f; x.Frame.VisualGeometryValid = true; x.Frame.VisualAngularSize = 10f; x.Frame.VisualAngularSpeed = 100f; }),
        ("roll", "input:optic-roll", x => { x.Frame.Light = 1f; x.Frame.AngularVelocityValid = true; x.Frame.AngularVelocity = 300f; }),
        ("audio-low", "input:auditory", x => { x.Frame.Sound = 1f; x.Frame.SoundSpectrumValid = true; x.Frame.SoundLow = 1f; }),
        ("audio-high", "input:auditory", x => { x.Frame.Sound = 1f; x.Frame.SoundSpectrumValid = true; x.Frame.SoundHigh = 1f; }),
        ("hot", "input:hot", x => x.Frame.Heat = 1f),
        ("cold", "input:cold", x => x.Frame.Cold = 1f),
        ("joint-motion", "input:joint-motion", x => { x.Frame.JointMotion = 1f; x.Frame.JointSensingValid = true; }),
        ("tilt-left", "input:gravity", x => { x.Frame.SignedTilt = -1f; x.Frame.TiltValid = true; }),
        ("tilt-right", "input:gravity", x => { x.Frame.SignedTilt = 1f; x.Frame.TiltValid = true; }),
        ("approach-left", "type:LC4", x => { x.Frame.VisualApproach = 1f; x.Frame.VisionDirection = -1f; x.Frame.VisionDirectionValid = true; }),
        ("approach-right", "type:LC4", x => { x.Frame.VisualApproach = 1f; x.Frame.VisionDirection = 1f; x.Frame.VisionDirectionValid = true; })
    };
    foreach (var (name, population, configure) in cases)
    {
        var brain = ConnectomeBrain.TryCreate(out var status);
        True(brain is not null, status);
        var input = new SensoryFrameBox { Frame = Healthy() };
        configure(input);
        if (name == "light-on") brain.Step(Healthy(light: 0f));
        if (name == "light-off") brain.Step(Healthy(light: 1f));
        long spikes = 0, inputSpikes = 0, dropped = 0;
        var peakWalk = 0f;
        for (var tick = 0; tick < 40; tick++)
        {
            var command = brain.Step(input.Frame, .05f);
            spikes += brain.FiredCount;
            inputSpikes += brain.PopulationFiredCount(population);
            dropped += brain.DroppedCount;
            peakWalk = Math.Max(peakWalk, Math.Abs(command.Walk));
            if (name.EndsWith("-pulse", StringComparison.Ordinal)) input.Frame.DamageEvent = 0f;
        }
        var connectivity = brain.TestPopulationConnectivity(population);
        Console.WriteLine(FormattableString.Invariant($"MAP {name}: members={brain.PopulationCount(population)} edges={connectivity.Edges} signedMembers={connectivity.NonzeroSignMembers} inputSpikes={inputSpikes} graphSpikes={spikes} dropped={dropped} peakAbsWalk={peakWalk:0.000}"));
    }
}

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
    brain.SetTestPopulation("input:light", 12000);
    brain.Step(Healthy(light: 1f));
    Equal(24000L, brain.TestBacklogCursor);
}

static void TerminalResetClearsStateAndRecovers()
{
    var brain = OneNeuronBrain();
    brain.SetTestPending(0, 1f);
    brain.Step(new SensoryFrame { Alive = false, HealthValid = true });
    Equal(0, brain.TestPendingCount);
    Equal(0L, brain.TestSimulationTick);
    Equal(0L, brain.TestBacklogCursor);
    brain.SetTestPending(0, 1f);
    brain.SetTestNeuronMetadata(0, "descending_neuron", "R");
    brain.SetTestPopulation("type:DNp09", 0);
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
    Equal(0f, brain.TestSensoryDrive);

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

static void SignedInputAccumulationIsOrderIndependent()
{
    var orders = new[]
    {
        new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
        new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
    };
    foreach (var order in orders)
    {
        var brain = ConnectomeBrain.CreateForTest(4, [0, 1, 2, 3, 3], [3, 3, 3], [4f, 4f, -4f]);
        foreach (var source in order) brain.SetTestPending(source, 1f);
        brain.Step(Healthy());
        Equal(4f, brain.TestPendingValue(3));
        brain.Step(Healthy());
        True(brain.DidFire(3), "mixed excitation/inhibition should produce the same target spike for every source order");
    }
}

static void InvalidHealthSuspendsWithoutReset()
{
    var brain = OneNeuronBrain();
    brain.SetTestPending(0, .5f);
    brain.Step(Healthy());
    var tick = brain.TestSimulationTick;
    var potential = brain.TestPotentialValue(0);
    var invalid = new SensoryFrame { Alive = false, HealthValid = false };
    Equal(0f, brain.Step(invalid).Walk);
    Equal(tick, brain.TestSimulationTick);
    Equal(potential, brain.TestPotentialValue(0));
    True(!brain.IsStopped, "unavailable health should suspend output rather than stop the neural state");
    brain.Step(Healthy());
    Equal(tick + 1, brain.TestSimulationTick);
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
    Equal(0f, brain.TestSensoryDrive); // Hypoxia is a body reading, not an external receptor.
}

static void MotorReversalsAreRateLimited()
{
    var brain = ConnectomeBrain.CreateForTest(2, [0, 0, 0], [], []);
    brain.SetTestNeuronMetadata(0, "descending_neuron", "R");
    brain.SetTestPopulation("type:DNp09", 0);
    foreach (var population in new[] { "type:DNg100", "type:DNge053", "type:DNge050", "type:DNg97" }) brain.SetTestPopulation(population, 0);
    brain.SetTestNeuronMetadata(1, "descending_neuron", "L");
    brain.SetTestPopulation("type:MDN", 1);
    brain.SetTestPending(0, 1f);
    var forward = brain.Step(Healthy(), .05f);
    brain.SetTestPending(1, 1f);
    var reversing = brain.Step(Healthy(), .05f);
    True(forward.Walk > 0f, "named walking population activity should request forward movement");
    True(MathF.Abs(reversing.Walk - forward.Walk) <= .4001f, "motor reversal exceeded the per-step rate limit");
    True(MathF.Abs(reversing.Walk) <= 1f, "rate-limited motor output must remain bounded");
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
    brain.SetTestNeuronMetadata(0, "descending_neuron", "R");
    brain.SetTestPopulation("type:DNp09", 0);
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
    brain.SetTestPopulation("input:light", count - 1);
    brain.Step(Healthy(light: 1f));
    True(brain.DidFire(count - 1) || brain.TestPotentialValue(count - 1) > .1f, "fresh sensory input was dropped behind the recurrent load");
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

static void InternalStatesDoNotFabricateReceptors()
{
    var brain = OneNeuronBrain();
    foreach (var name in new[] { "input:light", "input:auditory", "input:tactile", "input:hot", "input:cold", "type:LC4", "type:LPLC2", "type:MDN", "type:DNp09" })
        brain.SetTestPopulation(name, 0);
    var sensory = Healthy();
    sensory.Infection = 1f; sensory.LiquidExposure = 1f; sensory.LiquidHazard = 1f;
    sensory.LiquidHealing = 1f; sensory.LiquidWater = 1f; sensory.Adrenaline = 1f;
    sensory.Pain = 1f; sensory.Damage = 1f; sensory.Shock = 1f; sensory.Blood = 1f;
    sensory.Oxygen = 0f; sensory.SubmergedHypoxia = 1f; sensory.Nearby = 1f;
    brain.Step(sensory);
    Equal(0f, brain.TestSensoryDrive); Equal(0f, brain.TestPotentialValue(0)); Equal(0, brain.FiredCount);
}

static void NativeStressDoesNotDriveChemistry()
{
    var brain = OneNeuronBrain();
    var sensory = Healthy();
    sensory.Pain = 1f; sensory.Shock = 1f; sensory.Adrenaline = 1f;
    sensory.DamageValid = true; sensory.Damage = .5f; sensory.LimbLoss = .5f;
    var command = brain.Step(sensory);
    Equal(0f, brain.TestSensoryDrive); // No validated internal injury-to-receptor mapping.
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
    brain.SetTestPopulation("input:light", 0, 1);
    brain.Step(Healthy(light: .25f));
    Equal(.25f, brain.TestPotentialValue(0));
    Equal(.25f, brain.TestPotentialValue(1));
}

static void BundledSensoryMappings()
{
    var brain = ConnectomeBrain.TryCreate(out var status); True(brain is not null, status);
    foreach (var population in new[] { "input:light", "input:auditory", "input:tactile", "input:gravity", "input:joint-position", "input:joint-motion", "input:joint-load", "input:hot", "input:cold", "motor:leg", "type:DNp09", "type:DNg100", "type:DNge053", "type:DNge050", "type:DNg97", "type:MDN", "type:DNg60", "type:DNg74_a", "type:DNg74_b", "type:AN19A018", "type:LC4", "type:LPLC2", "type:Mi1", "type:L2", "type:L3", "input:touch-head", "input:touch-arms", "input:touch-legs", "input:touch-core", "input:touch-other", "input:optic-roll", "type:LC11", "type:LC18", "type:DNa02", "type:DNg13", "type:DNa01" })
    {
        var count = brain.PopulationCount(population);
        Console.WriteLine("MAPPING " + population + "=" + count);
        True(count > 0, "missing real-asset mapping: " + population);
    }
    Equal(7, brain.PopulationCount("input:hot"));
    Equal(7, brain.PopulationCount("input:cold"));
    Equal(114, brain.PopulationCount("input:auditory"));
    Equal(863, brain.PopulationCount("input:touch-head"));
    Equal(266, brain.PopulationCount("input:touch-arms"));
    Equal(1611, brain.PopulationCount("input:touch-legs"));
    Equal(303, brain.PopulationCount("input:touch-core"));
    Equal(378, brain.PopulationCount("input:touch-other"));
    Equal(34, brain.PopulationCount("input:optic-roll"));
    // A fresh first tick has no recurrent input. All 3,421 disjoint tactile/head
    // members should fire once, proving the real asset routes cover that union.
    brain.Step(Healthy() with { DamageEvent = .5f });
    Equal(3421, brain.FiredCount);
}

static void SensoryModalities()
{
    // Isolated targets make wrong cross-modal injections visible, independently
    // of recurrent connectivity. Full-asset membership is checked separately.
    ConnectomeBrain Create()
    {
        var brain = ConnectomeBrain.CreateForTest(14, new int[15], [], []);
        var groups = new[] { "input:light", "input:tactile", "input:hot", "input:cold", "input:joint-position", "input:joint-motion", "input:joint-load" };
        for (var i = 0; i < groups.Length; i++) brain.SetTestPopulation(groups[i], i);
        brain.SetTestPopulation("input:auditory", 7, 8);
        brain.SetTestPopulation("input:gravity", 9, 10);
        brain.SetTestPopulation("type:LC4", 11, 12);
        brain.SetTestPopulation("type:MDN", 13); brain.SetTestPopulation("type:DNp09", 13);
        foreach (var i in new[] { 7, 9, 11 }) brain.SetTestNeuronMetadata(i, "cb_sensory", "L");
        foreach (var i in new[] { 8, 10, 12 }) brain.SetTestNeuronMetadata(i, "cb_sensory", "R");
        return brain;
    }
    var cases = new (SensoryFrame Frame, int[] Expected)[]
    {
        (Healthy(light: .5f), [0]),
        (Healthy(touch: 1f), [1]),
        (Healthy() with { Heat = .5f }, [2]),
        (Healthy() with { Cold = .5f }, [3]),
        (Healthy() with { JointSensingValid = true, JointPosition = .5f }, [4]),
        (Healthy() with { JointSensingValid = true, JointMotion = .5f }, [5]),
        (Healthy() with { JointSensingValid = true, NeuralJointLoad = .5f }, [6]),
        (Healthy(sound: .5f) with { SoundDirectionValid = true, SoundDirection = -1f }, [7]),
        (Healthy(sound: .5f) with { SoundDirectionValid = true, SoundDirection = 1f }, [8]),
        (Healthy(sound: .5f) with { SoundDirection = 1f }, [7, 8]),
        (Healthy() with { TiltValid = true, SignedTilt = -1f }, [9]),
        (Healthy() with { TiltValid = true, SignedTilt = 1f }, [10]),
        (Healthy() with { VisualApproach = .5f, VisionDirectionValid = true, VisionDirection = -1f }, [11]),
        (Healthy() with { VisualApproach = .5f, VisionDirectionValid = true, VisionDirection = 1f }, [12]),
        (Healthy() with { JointPosition = 1f, JointMotion = 1f, NeuralJointLoad = 1f, SignedTilt = 1f }, []),
        (Healthy() with { Sound = float.NaN, Heat = float.NaN, Cold = float.PositiveInfinity, VisualApproach = float.NaN }, [])
    };
    foreach (var (frame, expected) in cases)
    {
        var brain = Create(); brain.Step(frame);
        for (var id = 0; id < 14; id++)
            True((brain.DidFire(id) || brain.TestPotentialValue(id) > 0f) == expected.Contains(id), "wrong input route, neuron " + id);
    }
}

static void GlobalLuminanceChanges()
{
    var brain = ConnectomeBrain.CreateForTest(3, new int[4], [], []);
    brain.SetTestPopulation("type:Mi1", 0);
    brain.SetTestPopulation("type:L2", 1);
    brain.SetTestPopulation("type:L3", 2);
    brain.Step(Healthy(light: 0f)); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: 1f)); True(brain.DidFire(0)); Equal(1, brain.FiredCount);
    brain.Step(Healthy(light: 1f)); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: 0f)); True(brain.DidFire(1) && brain.DidFire(2));
    brain.Step(default);
    brain.Step(Healthy(light: 1f)); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: float.NaN)); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: 0f)); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: 1f) with { LightValid = false }); Equal(0, brain.FiredCount);
    brain.Step(Healthy(light: 0f)); Equal(0, brain.FiredCount);
}

static ConnectomeBrain SensoryFeatureFixture()
{
    var brain = ConnectomeBrain.CreateForTest(14, new int[15], [], []);
    var groups = new[] { "input:touch-head", "input:touch-arms", "input:touch-legs", "input:touch-core", "input:touch-other", "input:light", "type:DNp09", "type:LC4", "type:LPLC2", "type:LC11", "type:LC18", "input:optic-roll", "input:auditory", "type:MDN" };
    for (var id = 0; id < groups.Length; id++) brain.SetTestPopulation(groups[id], id);
    brain.SetTestPopulation("input:tactile", 1, 2, 3, 4);
    return brain;
}

static void InjuryAndRegionalRoutes()
{
    var brain = SensoryFeatureFixture();
    brain.Step(Healthy() with { DamageEvent = .05f, Impact = .1f, RegionalTouchValid = true, TouchArms = 1f });
    for (var id = 0; id < 5; id++) True(Math.Abs(.22222222f - brain.TestPotentialValue(id)) < .00001f);
    for (var id = 5; id < 14; id++) Equal(0f, brain.TestPotentialValue(id));
    True(brain.DisplayInputSummary.Contains("injury-proxy=0.22"));
    brain.Step(Healthy()); Equal(0f, brain.TestSensoryDrive);
    True(brain.TestPotentialValue(0) < .22222222f, "past damage must decay rather than reinject");
    foreach (var (frame, target) in new[] {
        (Healthy() with { TouchHead = 1f }, 0), (Healthy() with { TouchArms = 1f }, 1),
        (Healthy() with { TouchLegs = 1f }, 2), (Healthy() with { TouchCore = 1f }, 3) })
    {
        brain = SensoryFeatureFixture();
        brain.Step(frame with { RegionalTouchValid = true, Touch = 1f, PhysicalContact = 1f });
        for (var id = 0; id < 14; id++) Equal(id == target ? .15f : 0f, brain.TestPotentialValue(id));
    }
    brain = SensoryFeatureFixture(); brain.Step(Healthy() with { DamageEvent = float.NaN, TouchHead = 1f });
    Equal(0f, brain.TestSensoryDrive); Equal(0, brain.ActiveCount);
    brain.Step(Healthy() with { DamageEvent = .5f }); Equal(5, brain.FiredCount);
    brain.Step(default); Equal(0f, brain.TestSensoryDrive);
}

static void GeometricVisualRoutes()
{
    var visible = Healthy() with { Vision = 1f, VisualGeometryValid = true, VisualAngularSize = 60f, VisualExpansion = 200f };
    var brain = SensoryFeatureFixture(); brain.Step(visible);
    Equal(.5f, brain.TestPotentialValue(7)); True(brain.DidFire(8)); Equal(0f, brain.TestPotentialValue(9));
    brain = SensoryFeatureFixture(); brain.Step(visible with { VisualExpansion = 0f, VisualAngularSize = 10f, VisualAngularSpeed = 100f });
    Equal(0f, brain.TestPotentialValue(7)); Equal(0f, brain.TestPotentialValue(8));
    Equal(.5f, brain.TestPotentialValue(9)); Equal(.5f, brain.TestPotentialValue(10));
    True(brain.TestSensoryDrive > 0f && brain.DisplayInputSummary.Contains("small-visual=0.50"));
    foreach (var frame in new[] {
        visible with { Vision = 0f }, visible with { VisualAngularSize = float.NaN },
        visible with { VisualExpansion = float.PositiveInfinity }, visible with { VisualAngularSpeed = float.NaN },
        visible with { VisualAngularSize = -10f }, visible with { VisualAngularSize = 181f },
        visible with { VisualAngularSize = 0f }, visible with { VisualGeometryValid = false } })
    {
        brain = SensoryFeatureFixture(); brain.Step(frame); Equal(0, brain.ActiveCount); Equal(0, brain.FiredCount);
    }
    brain = SensoryFeatureFixture(); brain.Step(visible with { VisualExpansion = -10f }); Equal(0, brain.ActiveCount);
    foreach (var speed in new[] { -300f, 300f })
    {
        brain = SensoryFeatureFixture(); brain.Step(Healthy(light: 1f) with { AngularVelocity = speed, AngularVelocityValid = true });
        Equal(.5f, brain.TestPotentialValue(11)); True(brain.DisplayInputSummary.Contains("optic-roll=0.50"));
    }
    foreach (var frame in new[] {
        Healthy() with { AngularVelocity = 300f, AngularVelocityValid = true },
        Healthy(light: 1f) with { AngularVelocity = 300f },
        Healthy(light: 1f) with { AngularVelocity = float.PositiveInfinity, AngularVelocityValid = true } })
    { brain = SensoryFeatureFixture(); brain.Step(frame); Equal(0f, brain.TestPotentialValue(11)); }
}

static void AuditoryBandRouting()
{
    var brain = SensoryFeatureFixture();
    brain.Step(Healthy(sound: 1f) with { SoundSpectrumValid = true, SoundLow = 1f }); Equal(.7f, brain.TestPotentialValue(12));
    brain = SensoryFeatureFixture(); brain.Step(Healthy(sound: 1f) with { SoundSpectrumValid = true, SoundHigh = 1f }); True(brain.DidFire(12));
    brain = SensoryFeatureFixture(); brain.Step(Healthy(sound: 1f) with { SoundSpectrumValid = true }); Equal(0f, brain.TestPotentialValue(12));
    brain = SensoryFeatureFixture(); brain.Step(Healthy(sound: 1f)); True(brain.DidFire(12));
    brain.Step(Healthy()); Equal(0f, brain.TestSensoryDrive);
}

static ConnectomeBrain MotorFeatureFixture()
{
    var brain = ConnectomeBrain.CreateForTest(5, new int[6], [], []);
    brain.SetTestPopulation("type:DNp09", 0); brain.SetTestPopulation("type:MDN", 1);
    brain.SetTestPopulation("type:DNa02", 2, 3); brain.SetTestPopulation("type:DNg74_b", 4);
    for (var id = 0; id < 5; id++) brain.SetTestNeuronMetadata(id, "descending_neuron", id == 2 ? "L" : "R");
    return brain;
}

static void LocomotionTemporalBehavior()
{
    var brain = MotorFeatureFixture(); brain.SetTestPending(0, 1f);
    var command = brain.Step(Healthy()); Equal(.3f, command.Walk);
    True(brain.DisplayMotorSummary.Contains("REQUEST (FORWARD)"));
    True(brain.DisplayMotorSummary.Contains("forward=0.09"), "filtered neural fraction must not be replaced by the 0.3 actuator floor");
    for (var tick = 0; tick < 4; tick++) True(brain.Step(Healthy()).Walk >= .3f, "neural mode should bridge a short firing gap");
    for (var tick = 0; tick < 20; tick++) command = brain.Step(Healthy());
    Equal(0f, command.Walk);
    brain = MotorFeatureFixture(); brain.SetTestPending(0, 1f); brain.Step(Healthy());
    brain.SetTestPending(1, 1f); True(brain.Step(Healthy()).Walk < .3f, "MDN must preempt established forward mode");
    True(brain.Step(Healthy()).Walk < 0f);
    foreach (var terminal in new[] { false, true })
    {
        brain = MotorFeatureFixture(); brain.SetTestPending(0, 1f); brain.Step(Healthy());
        command = brain.Step(terminal ? default : Healthy() with { ConsciousnessValid = false }); Equal(0f, command.Walk);
        Equal(0f, brain.Step(Healthy()).Walk);
    }
    brain = MotorFeatureFixture(); brain.SetTestPending(0, 1f); brain.Step(Healthy());
    brain.SetTestPending(4, 1f); command = brain.Step(Healthy()); Equal(0f, command.Walk); Equal(0f, command.Core);
    Equal(0f, brain.Step(Healthy()).Walk);
}

static void NeuralTurning()
{
    foreach (var id in new[] { 2, 3 })
    {
        var brain = MotorFeatureFixture(); brain.SetTestPending(id, 1f);
        var command = brain.Step(Healthy()); Equal(0f, command.Walk);
        True(id == 2 ? command.Head < 0f && command.Core < 0f : command.Head > 0f && command.Core > 0f);
        command = brain.Step(Healthy() with { Consciousness = .1f }); Equal(0f, command.Head); Equal(0f, command.Core);
        Equal(0f, brain.Step(Healthy()).Head);
    }
}

static void NamedMotorReadout()
{
    ConnectomeBrain Create()
    {
        var brain = ConnectomeBrain.CreateForTest(4, new int[5], [], []);
        brain.SetTestPopulation("type:DNp09", 0); brain.SetTestPopulation("type:MDN", 1);
        brain.SetTestPopulation("type:DNg60", 2); brain.SetTestPopulation("type:MN9", 3);
        for (var i = 0; i < 4; i++) brain.SetTestNeuronMetadata(i, i == 3 ? "cb_motor" : "descending_neuron", "R");
        return brain;
    }
    var brain = Create(); brain.SetTestPending(0, 1f); True(brain.Step(Healthy()).Walk > 0f, "walking DN must drive forward");
    brain = Create(); brain.SetTestPending(0, 1f); brain.SetTestPending(1, 1f); True(brain.Step(Healthy()).Walk < 0f, "MDN must take priority over forward");
    brain = Create(); brain.SetTestPending(0, 1f); brain.SetTestPending(2, 1f); Equal(0f, brain.Step(Healthy()).Walk);
    brain = Create(); brain.SetTestPending(3, 1f); var command = brain.Step(Healthy() with { Nearby = 1f });
    Equal(0f, command.Walk); Equal(0f, command.ReachGrab); Equal(0f, command.LeftGrip); Equal(0f, command.RightGrip);
    foreach (var stopPopulation in new[] { "type:DNg60", "type:AN19A018" })
    {
        brain = Create();
        foreach (var forwardPopulation in new[] { "type:DNp09", "type:DNg100", "type:DNge053", "type:DNge050", "type:DNg97" })
            brain.SetTestPopulation(forwardPopulation, 0);
        brain.SetTestPending(0, 1f);
        True(brain.Step(Healthy(), .25f).Walk > .8f, "forward channel should rise through its 150 ms temporal filter");
        brain.SetTestPopulation(stopPopulation, 2); brain.SetTestPending(2, 1f);
        command = brain.Step(Healthy());
        Equal(0f, command.Walk); Equal(0f, command.LeftLeg); Equal(0f, command.Core);
    }
    brain = Create(); brain.SetTestPopulation("subclass:wm", 3); brain.SetTestPending(3, 1f);
    command = brain.Step(Healthy()); Equal(0f, command.Walk); Equal(0f, command.LeftArm); Equal(0f, command.RightArm);
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
    brain.SetTestPopulation("input:light", 0);
    var stimulus = Healthy(light: 1f);
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
    LightValid = true,
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

internal sealed class SensoryFrameBox
{
    public SensoryFrame Frame;
}
