using Mod;
using Mod.Adapters;
using Mod.Core;
using Mod.UI;
using UnityEngine;

internal static class Program
{
    internal static int RunLegacy()
    {
        (string, Action)[] tests =
        [
            ("food cues require original stock identity and actual head contact", FoodItemCues),
            ("blood readings distinguish raw amount, relative baseline and detached limbs", BloodMeasurementContract),
            ("explicit limb loss emits one measured injury event", LimbLossEvents),
            ("jukebox and child playback feed current directional audio", JukeboxAudio),
            ("audio source scans stay bounded and exclude self sources", BoundedAudioSources),
            ("component discovery refreshes child components after bounded expiry", ComponentDiscoveryRefresh),
            ("component discovery cleanup and refresh budgets stay bounded and fair", ComponentDiscoveryBudgets),
            ("terminal motors and grips clear immediately", TerminalStop),
            ("native pose context actions are suppressed", ContextMenuPoseActions),
            ("direct fly control suppresses native balance assists and restores them", DirectFlyControl),
            ("unconscious and locally damaged limbs clear old commands", IncapableStop),
            ("brain injury remains alive with matching signal value", BrainInjury),
            ("invalid health stops control without inventing death", InvalidHealth),
            ("destroyed and newly added inactive limbs", LimbLifecycle),
            ("all 41 stock liquid IDs have explicit exposure routes", Liquids),
            ("unknown and invalid liquids cannot fabricate effects", UnknownAndInvalidLiquids),
            ("liquid snapshots and native zombie state stay separate", LiquidSnapshots),
            ("acid pools produce an explicit acid signal", AcidPools),
            ("paralysis, breakage and limb loss stay distinct", LimbDamageCategories),
            ("blood baseline and derived minimum limb health", BloodAndVitality),
            ("invalid limb health and blood remain unknown", InvalidLimbSamples),
            ("small health baselines preserve normalized health and injury events", SmallHealthBaselines),
            ("hypoxia and submersion remain distinct", Oxygen),
            ("invalid oxygen remains unknown without hypoxia", InvalidOxygen),
            ("invalid consciousness suspends output without an unconscious claim", InvalidConsciousness),
            ("circulation reports unknown when native flow is unavailable", InvalidCirculation),
        ("nearby temperatures provide bounded ambient heat and cold", AmbientTemperature),
        ("nearby lava provides heat and hazard", NearbyLava),
        ("external audio excludes own limbs, mute and invalid distances", Audio),
        ("numbered and cloned Root audio remains conservatively filtered", NumberedRootAudio),
        ("last-heard audio persists without stimulating stale sound", AudioHistory),
            ("native light owners affect the combined light sensor", NativeLightSources),
            ("local light respects state, transformed footprint and invalid data", LocalLightGeometry),
            ("local light scan reports truncation and avoids duplicate strength", LocalLightLimits),
            ("visible external objects produce a vision proxy", Vision),
            ("head rotation and mirroring change the observed field", HeadRelativeVision),
            ("visible target search skips blocked and out-of-view candidates", VisibleTargetSelection),
            ("visual search stays bounded and reports incomplete queries", VisualSearchLimits),
            ("head rotation produces sweep without inventing approach", HeadRelativeSweep),
            ("stationary head requests reach only the healthy native head joint", StationaryHeadMotor),
            ("surroundings retain independent visible bands as value snapshots", SpatialSurroundings),
            ("audio spectra use one current external source and reject invalid data", SpectrumValidity),
            ("visual geometry and rotation follow finite native measurements", GeometryAndRotation),
            ("directional native sensory readings clear and stay source-bound", DirectionalSensors),
            ("damage events and regional contact are observed edge samples", ObservedBodySensors),
            ("expanded sensing preserves environment telemetry", EnvironmentTelemetry),
            ("vibration, proprioception and projectile channels stay distinct", AdditionalSenses),
            ("falling is distinct from walking and floor contact", Falling),
            ("contact impacts do not fabricate hearing", Impacts),
            ("detached owned limbs remain excluded from collision signals", DetachedOwnCollisions),
            ("detached source probes cannot report collisions", DetachedSourceCollisions),
            ("connected source probes report projectiles", ConnectedSourceProjectiles),
            ("detached limbs remain diagnostic but cannot feed body control", DetachedLimbsDoNotControl),
            ("control clock preserves rate and caps catch-up", ControlClock),
            ("suspension clears transient events", SuspensionClearsTransientEvents),
            ("initially disabled controllers reject events", InitiallyDisabledControllersRejectEvents),
            ("disabled controllers do not claim telemetry", DisabledControllersDoNotClaimTelemetry),
            ("regeneration ownership and cleanup", Chemistry),
            ("front-back hierarchy routes separate channels", SideRouting),
            ("missing grip and joint do not interrupt other limbs", OptionalControls),
            ("zero grip leaves an existing hold unchanged", NeutralGrip),
            ("broken limbs do not suppress healthy limb control", LocalLimbDamage),
            ("dead limb health stops only its own actuator", LocalDeadLimb),
            ("finite boundary sanitization", FiniteInputs),
            ("telemetry follows native readings independently of requests", NativeTelemetryTrace),
            ("native motor requests use bounded walking and angular units", NativeMotorUnits),
            ("freeze cutoff clears actuators consistently", FreezeCutoff),
            ("walking requests persist between neural ticks", WalkingRequestMaintenance),
            ("walking maintenance stops on current native state", WalkingMaintenanceChecksCurrentState),
            ("component disable clears commands and chemistry", Disable),
            ("never-activated cleanup preserves game state", NeverActivated),
            ("neutral chemistry preserves adrenaline", NeutralChemistry),
            ("active chemistry respects native adrenaline range", NativeAdrenalineRange),
            ("hazard walking keeps the native pose gate", HazardWalkingKeepsNativeGate),
            ("chemistry interventions scale with elapsed time", ChemistryScalesWithElapsedTime),
            ("future fly adapter reports unsupported capabilities safely", FlyAdapterIsDisabled),
            ("vision radius updates the live adapter without recreation", VisionRadiusUpdates)
        ];
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { Physics2D.LinecastHandler = null; Physics2D.LinecastCalls = 0; Physics2D.Hits = []; Physics2D.LinecastHits = []; Physics2D.LinecastResult = default; test(); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
        }
        return failures;
    }
    private static void True(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static void Equal(float expected, float actual) { if (float.IsNaN(actual) || MathF.Abs(expected - actual) > .00001f) throw new Exception($"expected {expected}, actual {actual}"); }
    private static void Equal(string expected, string actual) { if (expected != actual) throw new Exception($"expected {expected}, actual {actual}"); }
    private static MotorCommand Moving => new() { Walk = 1, RightArm = 1, LeftArm = -1, RightLeg = 1, LeftLeg = -1, Head = .5f, Core = .5f, RightGrip = 1, LeftGrip = 1, ReachGrab = 1, Heal = .8f, Avoid = 0, Freeze = 0, Stimulate = 0, Calm = 0, Extinguish = 0 };
    private static void FlyAdapterIsDisabled()
    {
        using var adapter = new PeoplePlaygroundFlyAdapter();
        True(!adapter.IsUsable && !adapter.HasSample && !adapter.IsTerminal && !adapter.IsBrainDead);
        True(!adapter.Read().HealthValid);
        adapter.Apply(default, true, 30f, 2f, .05f);
        adapter.RefreshWalkingRequest(); adapter.Suspend(); adapter.Stop();
    }
    private static void TerminalStop()
    {
        var f = new Fixture(); f.Adapter.Read(); f.Adapter.Apply(Moving, true);
        f.Limb.MotorSpeed = 10; f.Limb.GripBehaviour.isHolding = true; f.Limb.IsCapable = false;
        f.Person.Braindead = true;
        var frame = f.Adapter.Read(); True(!frame.Alive && frame.BrainDead); Equal("BRAIN DEAD", f.Adapter.LiveState);
        True(f.Adapter.LiveLimbSummary.Contains("CONTROL: stopped (brain dead; per-limb capability suppressed)"));
        True(!f.Adapter.LiveLimbSummary.Contains("LowerArmFront:incapable"));
        f.Adapter.Apply(Moving, true); Equal(0, f.Limb.MotorSpeed); Equal(0, f.Person.DesiredWalkingDirection);
        True(!f.Limb.GripBehaviour.isHolding); Equal(0, f.Limb.RegenerationSpeed); Equal(0, f.Limb.CirculationBehaviour.BloodRegenerationPerSecond);
        f.Person.Braindead = false; f.Person.AverageHealth = .0005f; f.Adapter.Read(); Equal("DEAD", f.Adapter.LiveState);
        f.Person.AverageHealth = 1; f.Limb.IsCapable = true; True(f.Adapter.Read().Alive); f.Adapter.Apply(Moving, false); Equal(1, f.Person.DesiredWalkingDirection);
    }
    private static void IncapableStop()
    {
        var f = new Fixture(); f.Person.Consciousness = .7f; f.Limb.MotorSpeed = 8; f.Limb.GripBehaviour.isHolding = true;
        f.Adapter.Read(); Equal("UNCONSCIOUS", f.Adapter.LiveState); f.Adapter.Apply(Moving, true); Equal(0, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);
        f.Person.Consciousness = 1; f.Limb.IsCapable = false; f.Limb.MotorSpeed = 3; f.Adapter.Read(); f.Adapter.Apply(Moving, true); True(f.Limb.MotorSpeed != 0); Equal(0.8f, f.Limb.RegenerationSpeed);
        True(!f.Adapter.LiveLimbSummary.Contains("LowerArmFront:incapable"));
        f.Limb.Broken = true; f.Limb.MotorSpeed = 3; f.Adapter.Read(); f.Adapter.Apply(Moving, true); Equal(0, f.Limb.MotorSpeed);
        True(f.Adapter.LiveLimbSummary.Contains("LowerArmFront:broken"));
    }
    private static void BrainInjury()
    {
        var f = new Fixture(); f.Person.BrainDamaged = true; True(f.Adapter.Read().Alive);
        Equal("BRAIN INJURED", f.Adapter.LiveState); Equal("BRAIN INJURY", f.Adapter.LiveSignal); Equal(1, f.Adapter.LiveSignalValue);
    }
    private static void InvalidHealth()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var f = new Fixture(); f.Person.AverageHealth = bad; var frame = f.Adapter.Read();
            True(!frame.Alive && !frame.HealthValid && !f.Adapter.IsTerminal); Equal("INVALID DATA", f.Adapter.LiveState);
            True(f.Adapter.LiveBodySummary.Contains("hp=unknown")); f.Adapter.Apply(Moving, true); Equal(0, f.Person.DesiredWalkingDirection);
        }
    }
    private static void LimbLifecycle()
    {
        var f = new Fixture(); f.Limb.Destroyed = true; f.Adapter.Read(); f.Adapter.Apply(Moving, true); Equal("OFFLINE", f.Adapter.LiveState);
        var extra = Fixture.AddLimb(f.Root, "FootBack"); extra.gameObject.activeSelf = false;
        True(f.Adapter.Read().Alive); f.Adapter.Apply(Moving, false); True(extra.MotorSpeed < 0); True(f.Adapter.LiveLimbSummary.Contains("LIMBS: total=1"));
    }
    private static void Liquids()
    {
        var groups = new (string Kind, string[] Ids)[]
        {
            ("Blood", ["BLOOD"]),
            ("Hazard", ["GORSE BLOOD", "OIL", "NITRO", "TRITIUM", "COOLANT", "REANIMATION AGENT", "ACID", "BONE EATING POISON", "INSTANT DEATH POISON", "FREEZE POISON", "OSTEOMORPHOSIS AGENT", "VESTIBULAR POISON", "MUSCLE POISON", "NUMBING POISON", "EXPLOSION POISON", "CRUSHING POISON", "DISTORTION POISON", "CIRCULATION POISON", "COMBUSTION AGENT", "TISSUE DECONSTRUCTION AGENT"]),
            ("Sedative", ["KNOCKOUT POISON"]),
            ("Stimulant", ["ADRENALINE"]),
            ("Restorative", ["COAGULATION SERUM", "LIFE SERUM", "MENDING SERUM", "IMMORTALITY SERUM", "REGENERATION SERUM"]),
            ("OtherExposure", ["ULTRA STRENGTH SERUM", "DURABILITY SERUM", "ENHANCING SERUM", "EXOTIC LIQUID", "INERT LIQUID", "BEVERAGE M04", "WATER BREATHING SERUM", "PAIN KILLER", "INERT PINK LIQUID", "MIRRORISING AGENT", "TRANSPARENCY AGENT", "MASS AGENT", "DEBUG LIQUID 001"])
        };
        var count = 0;
        foreach (var (kind, ids) in groups)
            foreach (var id in ids)
            {
                var f = new Fixture(); var c = f.Limb.CirculationBehaviour;
                c.LiquidDistribution.Clear();
                c.LiquidDistribution[new Liquid(id)] = new() { Raw = .4f };
                var frame = f.Adapter.Read();
                Equal(kind == "Blood" ? 0f : .4f, frame.LiquidExposure);
                Equal(kind == "Hazard" ? .4f : 0f, frame.LiquidHazard);
                Equal(kind == "Sedative" ? .4f : 0f, frame.LiquidSedation);
                Equal(kind == "Stimulant" ? .4f : 0f, frame.LiquidStimulation);
                Equal(kind == "Restorative" ? .4f : 0f, frame.LiquidHealing);
                Equal(0f, frame.LiquidWater); Equal(0f, frame.Infection);
                True(f.Adapter.LiveLiquidSummary.Contains(id + ": " + 40f.ToString("0.0") + "% (" + kind + ")"));
                if (kind == "Sedative") Equal("SEDATIVE EXPOSURE", f.Adapter.LiveState);
                c.LiquidDistribution.Clear();
                frame = f.Adapter.Read(); Equal(0f, frame.LiquidExposure); Equal(0f, frame.LiquidHazard);
                Equal(0f, frame.LiquidSedation); Equal(0f, frame.LiquidStimulation); Equal(0f, frame.LiquidHealing);
                True(!f.Adapter.LiveLiquidSummary.Contains(id + ":"));
                count++;
            }
        Equal(41, count);
    }
    private static void UnknownAndInvalidLiquids()
    {
        var f = new Fixture(); var c = f.Limb.CirculationBehaviour;
        foreach (var liquid in new[] { new Liquid("MOD ACID HEALING WATER"), new Liquid("Human blood"), new Liquid("acid"), new Liquid(null) { DisplayName = "ACID" } })
        {
            c.LiquidDistribution.Clear(); c.LiquidDistribution[liquid] = new() { Raw = .4f };
            var frame = f.Adapter.Read(); Equal(.4f, frame.LiquidExposure);
            Equal(0f, frame.LiquidHazard); Equal(0f, frame.LiquidHealing); Equal(0f, frame.LiquidWater);
            Equal(0f, frame.LiquidSedation); Equal(0f, frame.LiquidStimulation);
            True(f.Adapter.LiveLiquidSummary.Contains("UnknownExposure"));
        }
        c.LiquidDistribution.Clear(); var acid = new Liquid("ACID");
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            c.TotalLiquidAmount = bad; c.LiquidDistribution[acid] = new() { Raw = .4f };
            var frame = f.Adapter.Read(); Equal(0f, frame.LiquidExposure); Equal(0f, frame.LiquidHazard);
            True(f.Adapter.LiveLiquidSummary.Contains("Incomplete"));
            c.TotalLiquidAmount = 1f; c.LiquidDistribution[acid].Raw = bad;
            frame = f.Adapter.Read(); Equal(0f, frame.LiquidExposure); Equal(0f, frame.LiquidHazard);
        }
        c.LiquidDistribution[acid] = null;
        Equal(0f, f.Adapter.Read().LiquidHazard);
        c.TotalLiquidAmount = 0f; c.LiquidDistribution[acid] = new() { Raw = .4f };
        Equal(0f, f.Adapter.Read().LiquidHazard);
        c.LiquidDistribution = null;
        Equal(0f, f.Adapter.Read().LiquidExposure);
        True(f.Adapter.LiveLiquidSummary.Contains("unknown: no readable circulation"));
    }
    private static void LiquidSnapshots()
    {
        var f = new Fixture(); var c = f.Limb.CirculationBehaviour;
        var other = Fixture.AddLimb(f.Root, "FootFront");
        var agent = new Liquid("REANIMATION AGENT");
        c.TotalLiquidAmount = 2f;
        c.LiquidDistribution[agent] = new() { Raw = .5f };
        c.LiquidDistribution[new Liquid("BLOOD")] = new() { Raw = 1.5f };
        other.CirculationBehaviour.LiquidDistribution[agent] = new() { Raw = .5f };
        var frame = f.Adapter.Read(); Equal(.5f, frame.LiquidHazard); Equal(0f, frame.Infection);
        True(f.Adapter.LiveLiquidSummary.Contains("REANIMATION AGENT: " + 50f.ToString("0.0") + "%"));
        c.LiquidDistribution.Clear(); other.CirculationBehaviour.LiquidDistribution.Clear();
        f.Limb.IsZombie = true;
        frame = f.Adapter.Read(); Equal(0f, frame.LiquidHazard); Equal(1f, frame.Infection);
        True(f.Adapter.LiveInjurySummary.Contains("zombie(native)=" + 1f.ToString("0.00")));
        Equal("NATIVE ZOMBIE STATE", f.Adapter.LiveSignal);
        f.Adapter.Suspend(); True(f.Adapter.LiveLiquidSummary.Contains("not sampled"));
    }
    private static void ContextMenuPoseActions()
    {
        var f = new Fixture();
        var options = f.Root.AddComponent<ContextMenuOptionComponent>();
        var walking = new ContextMenuButton("startWalking", "Forces the walking animation override");
        var sitting = new ContextMenuButton("startSit", "Forces the sitting animation override");
        var delete = new ContextMenuButton("delete", "Delete");
        options.Buttons.Add(walking); options.Buttons.Add(sitting); options.Buttons.Add(delete);
        var unrelated = new ContextMenuButton("customProtection", "Toggle fire protection");
        options.Buttons.Add(unrelated); options.Buttons.Add(default);
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(5, options.Buttons.Count);
        type.GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(3, options.Buttons.Count); True(options.Buttons.Contains(delete)); True(options.Buttons.Contains(unrelated));
        type.GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        var lateOptions = f.Root.AddComponent<ContextMenuOptionComponent>();
        lateOptions.Buttons.Add(new ContextMenuButton("startWalking", "Forces the walking animation override"));
        lateOptions.Buttons.Add(new ContextMenuButton("startSit", "Forces the sitting animation override"));
        lateOptions.Buttons.Add(new ContextMenuButton("delete", "Delete"));
        Equal(3, lateOptions.Buttons.Count);
        type.GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(1, lateOptions.Buttons.Count); True(lateOptions.Buttons.Any(button => button.Identity == "delete"));
        type.GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(5, options.Buttons.Count); True(options.Buttons.Contains(walking)); True(options.Buttons.Contains(sitting));
        Equal(3, lateOptions.Buttons.Count);
    }
    private static void DirectFlyControl()
    {
        var f = new Fixture();
        f.Limb.FakeUprightForce = 12f;
        f.Limb.BalanceMuscleMovement = 2f;
        f.Limb.DoBalanceJerk = true;
        f.Limb.DoStumble = true;
        var pose = new RagdollPose { ShouldStandUpright = true, ShouldStumble = true, UprightForceMultiplier = 1.5f, ForceMultiplier = 2f };
        f.Person.Poses.Add(pose);
        f.Person.ActivePose = pose;
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(controller, null);
        type.GetMethod("OnEnable", flags).Invoke(controller, null);
        True((bool)type.GetProperty("DirectFlyControlEnabled").GetValue(controller));
        type.GetMethod("SetDirectFlyControl", flags).Invoke(controller, [true]);
        Equal(0f, f.Limb.FakeUprightForce);
        Equal(0f, f.Limb.BalanceMuscleMovement);
        True(!f.Limb.DoBalanceJerk && !f.Limb.DoStumble);
        True(!pose.ShouldStandUpright && !pose.ShouldStumble);
        Equal(0f, pose.UprightForceMultiplier);
        Equal(0f, pose.ForceMultiplier);
        var added = Fixture.AddLimb(f.Root, "LateArm");
        added.FakeUprightForce = 8f;
        added.BalanceMuscleMovement = 3f;
        type.GetMethod("LateUpdate", flags).Invoke(controller, null);
        Equal(0f, added.FakeUprightForce);
        Equal(0f, added.BalanceMuscleMovement);
        type.GetMethod("SetDirectFlyControl", flags).Invoke(controller, [false]);
        Equal(12f, f.Limb.FakeUprightForce);
        Equal(2f, f.Limb.BalanceMuscleMovement);
        True(f.Limb.DoBalanceJerk && f.Limb.DoStumble);
        True(pose.ShouldStandUpright && pose.ShouldStumble);
        Equal(1.5f, pose.UprightForceMultiplier);
        Equal(2f, pose.ForceMultiplier);
        Equal(8f, added.FakeUprightForce);
        Equal(3f, added.BalanceMuscleMovement);
        type.GetMethod("OnDestroy", flags).Invoke(controller, null);
    }
    private static void FoodItemCues()
    {
        ModAPI.Pumpkin = new SpawnableAsset { name = "Pumpkin" };
        var f = new Fixture(); f.Limb.HasBrain = true;
        var headCollider = f.Limb.gameObject.AddComponent<Collider2D>();
        var item = new GameObject("Pumpkin"); var physical = item.AddComponent<PhysicalBehaviour>();
        var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f);
        Physics2D.Hits = [collider];
        True(f.Adapter.Read().FoodCuesValid); Equal(0f, f.Adapter.Read().FoodNearbyCue);
        var identity = item.AddComponent<SerialiseInstructions>();
        identity.OriginalSpawnableAsset = new SpawnableAsset { name = "Pumpkin" };
        Equal(0f, f.Adapter.Read().FoodNearbyCue); // Name alone is never sufficient.
        identity.OriginalSpawnableAsset = ModAPI.Pumpkin; item.name = "Renamed stock object";
        var frame = f.Adapter.Read(); Equal(.75f, frame.FoodNearbyCue); Equal(0f, frame.FoodContactCue);
        headCollider.Touching = collider; Equal(1f, f.Adapter.Read().FoodContactCue);
        headCollider.enabled = false; Equal(0f, f.Adapter.Read().FoodContactCue); headCollider.enabled = true;
        collider.isTrigger = true; Equal(0f, f.Adapter.Read().FoodNearbyCue); collider.isTrigger = false;
        physical.isDisintegrated = true; Equal(0f, f.Adapter.Read().FoodNearbyCue); physical.isDisintegrated = false;
        item.activeSelf = false; Equal(0f, f.Adapter.Read().FoodNearbyCue); item.activeSelf = true;
        item.transform.SetParent(f.Root.transform); Equal(0f, f.Adapter.Read().FoodNearbyCue); item.transform.SetParent(null);
        // The proximity convention is independent of lighting/head direction.
        collider.Surface = new Vector2(-4f, 0f); Equal(.5f, f.Adapter.Read().FoodNearbyCue);
        collider.Surface = new Vector2(float.NaN, 0f); Equal(0f, f.Adapter.Read().FoodNearbyCue);
        collider.Surface = new Vector2(2f, 0f); f.Limb.IsDismembered = true;
        frame = f.Adapter.Read(); True(!frame.FoodCuesValid); Equal(0f, frame.FoodContactCue);
        f.Limb.IsDismembered = false; ModAPI.Pumpkin.Destroyed = true; ModAPI.Pumpkin = null;
        frame = f.Adapter.Read(); True(!frame.FoodCuesValid); Equal(0f, frame.FoodNearbyCue);
        Physics2D.Hits = [];
    }

    private static void BloodMeasurementContract()
    {
        var f = new Fixture(); var circulation = f.Limb.CirculationBehaviour;
        circulation.LiquidDistribution.Clear();
        circulation.LiquidDistribution[new Liquid("ADRENALINE")] = new() { Raw = 1f };
        var frame = f.Adapter.Read(); Equal(1, frame.BloodSampleCount); Equal(0f, frame.NativeBloodMinimum);
        True(!frame.BloodValid); // Known empty amount; unknown deficit before a positive reference.
        Equal(1f, circulation.BloodAmount); Equal(0f, circulation.GetAmountOfBlood());
        circulation.OriginalBloodAmount = .2f; frame = f.Adapter.Read(); Equal(0f, frame.Blood); Equal(.2f, frame.NativeBloodMinimum);
        circulation.TotalLiquidAmount = 2f; circulation.OriginalBloodAmount = .1f;
        frame = f.Adapter.Read(); Equal(.5f, frame.Blood); Equal(.1f, frame.NativeBloodMaximum);
        circulation.OriginalBloodAmount = .0001f; frame = f.Adapter.Read(); True(frame.Blood < 1f);
        f = new Fixture(); circulation = f.Limb.CirculationBehaviour;
        circulation.OriginalBloodAmount = .0001f; frame = f.Adapter.Read(); True(frame.BloodValid); Equal(0f, frame.Blood);
        circulation.OriginalBloodAmount = .00005f; Equal(.5f, f.Adapter.Read().Blood);
        var other = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, other];
        f.Adapter.Read(); other.CirculationBehaviour.OriginalBloodAmount = 0f; other.IsDismembered = true;
        frame = f.Adapter.Read(); Equal(.5f, frame.Blood); Equal(1, frame.BloodExpectedSamples);
        other.IsDismembered = false; other.CirculationBehaviour.OriginalBloodAmount = float.NaN;
        frame = f.Adapter.Read(); Equal(1, frame.BloodSampleCount); Equal(2, frame.BloodExpectedSamples);
    }

    private static void LimbLossEvents()
    {
        var f = new Fixture(); f.Adapter.Read(); f.Limb.PhysicalBehaviour.isDisintegrated = true;
        Equal(1f, f.Adapter.Read().DamageEvent); Equal(0f, f.Adapter.Read().DamageEvent);
        f = new Fixture(); f.Limb.IsDismembered = true; Equal(0f, f.Adapter.Read().DamageEvent);
        f = new Fixture(); f.Adapter.Read(); f.Limb.IsDismembered = true; f.Person.Braindead = true;
        Equal(0f, f.Adapter.Read().DamageEvent);
        f = new Fixture(); f.Adapter.Read(); f.Person.Consciousness = .2f;
        f.Adapter.Read(); f.Adapter.Apply(Moving, false); f.Limb.IsDismembered = true;
        Equal(1f, f.Adapter.Read().DamageEvent); Equal(0f, f.Limb.MotorSpeed);
        f = new Fixture(); f.Adapter.Read(); f.Adapter.Apply(new MotorCommand { Freeze = 1f }, false);
        f.Limb.PhysicalBehaviour.isDisintegrated = true; Equal(1f, f.Adapter.Read().DamageEvent);
        f = new Fixture(); f.Adapter.Read(); f.Adapter.Apply(Moving, false); f.Person.Consciousness = .2f;
        f.Adapter.RefreshWalkingRequest(); f.Limb.IsDismembered = true;
        Equal(1f, f.Adapter.Read().DamageEvent); Equal(0f, f.Person.DesiredWalkingDirection);
    }

    private static void JukeboxAudio()
    {
        var f = new Fixture(); var item = new GameObject("Jukebox");
        var physical = item.AddComponent<PhysicalBehaviour>();
        var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f);
        var musicObject = new GameObject("Music channel"); musicObject.transform.SetParent(item.transform);
        musicObject.transform.position = new Vector3(-2f, 0f, 0f);
        var music = musicObject.AddComponent<AudioSource>(); music.isPlaying = true; music.volume = .8f;
        music.Spectrum = new float[512]; music.Spectrum[4] = 1f;
        item.AddComponent<JukeboxBehaviour>().audioSource = music;
        physical.MainAudioSource = item.AddComponent<AudioSource>(); // Idle impact source.
        Physics2D.Hits = [collider, collider];
        var frame = f.Adapter.Read(); Equal(.6f, frame.Sound); Equal(-1f, frame.SoundDirection);
        True(frame.SoundSpectrumValid); Equal(1, music.SpectrumCalls);
        music.mute = true; Equal(0f, f.Adapter.Read().Sound); music.mute = false;
        music.isPlaying = false; Equal(0f, f.Adapter.Read().Sound); music.isPlaying = true;
        music.isActiveAndEnabled = false; Equal(0f, f.Adapter.Read().Sound); music.isActiveAndEnabled = true;
        physical.isDisintegrated = true; Equal(0f, f.Adapter.Read().Sound); physical.isDisintegrated = false;
        // Sources attached to ordinary objects work without a jukebox component.
        item.GetComponent<JukeboxBehaviour>().audioSource = null;
        musicObject.transform.position = new Vector3(4f, 0f, 0f); frame = f.Adapter.Read(); Equal(.4f, frame.Sound); Equal(1f, frame.SoundDirection);
        musicObject.activeSelf = false; Equal(0f, f.Adapter.Read().Sound);
        Physics2D.Hits = [];
    }

    private static void BoundedAudioSources()
    {
        var f = new Fixture(); var item = new GameObject("Sound fixture"); item.AddComponent<PhysicalBehaviour>();
        var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f);
        var sources = new List<AudioSource>();
        for (var i = 0; i < 70; i++)
        {
            var child = new GameObject("Source " + i); child.transform.SetParent(item.transform); child.transform.position = new Vector3(2f, 0f, 0f);
            var source = child.AddComponent<AudioSource>(); source.isPlaying = true; sources.Add(source);
        }
        Physics2D.Hits = [collider]; var frame = f.Adapter.Read(); True(frame.SoundLimited); Equal(.75f, frame.Sound);
        Equal(1, sources.Sum(source => source.SpectrumCalls));
        var ownMusic = f.Limb.gameObject.AddComponent<AudioSource>(); ownMusic.isPlaying = true;
        item.AddComponent<JukeboxBehaviour>().audioSource = ownMusic;
        foreach (var source in sources) source.isPlaying = false;
        Equal(0f, f.Adapter.Read().Sound);
        Physics2D.Hits = []; frame = f.Adapter.Read(); Equal(0f, frame.Sound); True(!frame.SoundLimited);
    }

    private static void ComponentDiscoveryRefresh()
    {
        var f = new Fixture();
        var item = new GameObject("External speaker"); item.AddComponent<PhysicalBehaviour>();
        var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f);
        var child = new GameObject("Child source"); child.transform.SetParent(item.transform); child.transform.position = new Vector3(2f, 0f, 0f);
        Physics2D.Hits = [collider];
        Time.realtimeSinceStartup = 0f; Time.time = 0f;
        Equal(0f, f.Adapter.Read().Sound);

        var source = child.AddComponent<AudioSource>(); source.isPlaying = true; source.volume = 1f;
        Time.realtimeSinceStartup = 1f; Time.time = 1f;
        Equal(0f, f.Adapter.Read().Sound); // Hints alone do not force a burst refresh.
        Time.realtimeSinceStartup = 4f; Time.time = 4f;
        True(f.Adapter.Read().Sound > 0f); // Maximum age discovers a component on an existing child.

        source.isPlaying = false;
        var replacement = child.AddComponent<AudioSource>(); replacement.isPlaying = true; replacement.volume = 1f;
        Time.realtimeSinceStartup = 8f; Time.time = 8f;
        True(f.Adapter.Read().Sound > 0f); // Refresh also sees a replacement without owner count changes.
        Physics2D.Hits = []; Time.realtimeSinceStartup = 0f; Time.time = 0f;
    }

    private static void ComponentDiscoveryBudgets()
    {
        var f = new Fixture();
        var colliders = new List<Collider2D>();
        var physicals = new List<PhysicalBehaviour>();
        var children = new List<GameObject>();
        for (var i = 0; i < 8; i++)
        {
            var item = new GameObject("Speaker " + i);
            var physical = item.AddComponent<PhysicalBehaviour>(); physicals.Add(physical);
            var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f); colliders.Add(collider);
            var child = new GameObject("Source " + i); child.transform.SetParent(item.transform); child.transform.position = new Vector3(2f, 0f, 0f); children.Add(child);
        }

        Physics2D.Hits = colliders.ToArray(); Time.time = 0f; f.Adapter.Read();
        var ownersField = typeof(PeoplePlaygroundPersonAdapter).GetField("componentCacheOwners", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var owners = (List<PhysicalBehaviour>)ownersField.GetValue(f.Adapter)!;
        Equal(8, owners.Count);
        var enqueue = typeof(PeoplePlaygroundPersonAdapter).GetMethod("EnqueueExpiredComponentDiscoveries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var refresh = typeof(PeoplePlaygroundPersonAdapter).GetMethod("RefreshQueuedComponentDiscoveries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var queueField = typeof(PeoplePlaygroundPersonAdapter).GetField("discoveryRefreshQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Time.time = 4f; enqueue.Invoke(f.Adapter, null);
        foreach (var physical in physicals) physical.Destroyed = true;
        refresh.Invoke(f.Adapter, [4]);
        Equal(4, ((Queue<PhysicalBehaviour>)queueField.GetValue(f.Adapter)!).Count); // Skipped queue entries still consume the inspection budget.
        var prune = typeof(PeoplePlaygroundPersonAdapter).GetMethod("PruneComponentDiscoveryCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        prune.Invoke(f.Adapter, [4]);
        Equal(4, owners.Count); // Four destroyed entries, not the whole cache, are removed in one pass.

        // Rebuild a fresh fixture for the fairness portion: all eight owners expire together,
        // but the last four must still refresh on the following sample.
        f = new Fixture(); colliders.Clear(); children.Clear();
        for (var i = 0; i < 8; i++)
        {
            var item = new GameObject("Fair speaker " + i); item.AddComponent<PhysicalBehaviour>();
            var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f); colliders.Add(collider);
            var child = new GameObject("Fair source " + i); child.transform.SetParent(item.transform); child.transform.position = new Vector3(2f, 0f, 0f); children.Add(child);
        }
        Physics2D.Hits = colliders.ToArray(); Time.time = 0f; Equal(0f, f.Adapter.Read().Sound);
        foreach (var child in children)
        {
            var source = child.AddComponent<AudioSource>(); source.isPlaying = true; source.volume = 1f;
        }
        Time.time = 1f; Equal(0f, f.Adapter.Read().Sound);
        Time.time = 3f; True(f.Adapter.Read().Sound > 0f); // First four refresh.
        foreach (var child in children.Take(4)) child.GetComponent<AudioSource>().isPlaying = false;
        Time.time = 4f; True(f.Adapter.Read().Sound > 0f); // Remaining four are not starved.
        var cacheField = typeof(PeoplePlaygroundPersonAdapter).GetField("componentCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var cache = (System.Collections.IDictionary)cacheField.GetValue(f.Adapter)!;
        var refreshTimes = cache.Values.Cast<object>().Select(value => (float)value.GetType().GetField("LastRefreshTime")!.GetValue(value)!).ToList();
        Equal(4, refreshTimes.Count(time => time >= 4f));
        Time.time = 7f; f.Adapter.Read();
        Time.time = 8f; f.Adapter.Read();
        refreshTimes = cache.Values.Cast<object>().Select(value => (float)value.GetType().GetField("LastRefreshTime")!.GetValue(value)!).ToList();
        Equal(8, refreshTimes.Count(time => time >= 7f)); // Multiple expiry cycles remain fair.
        Physics2D.Hits = []; Time.time = 0f;
        Physics2D.Hits = []; Time.time = 0f;

        // Topology changes on many existing owners use the same four-work budget.
        f = new Fixture(); colliders.Clear(); var topologyItems = new List<GameObject>();
        for (var i = 0; i < 8; i++)
        {
            var item = new GameObject("Topology " + i); item.AddComponent<PhysicalBehaviour>(); topologyItems.Add(item);
            var collider = item.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f); colliders.Add(collider);
        }
        Physics2D.Hits = colliders.ToArray(); Time.time = 0f; f.Adapter.Read();
        foreach (var item in topologyItems) item.AddComponent<JukeboxBehaviour>();
        Time.time = 1f; f.Adapter.Read();
        cacheField = typeof(PeoplePlaygroundPersonAdapter).GetField("componentCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        cache = (System.Collections.IDictionary)cacheField.GetValue(f.Adapter)!;
        refreshTimes = cache.Values.Cast<object>().Select(value => (float)value.GetType().GetField("LastRefreshTime")!.GetValue(value)!).ToList();
        Equal(4, refreshTimes.Count(time => time == 1f));
        Time.time = 2f; f.Adapter.Read();
        refreshTimes = cache.Values.Cast<object>().Select(value => (float)value.GetType().GetField("LastRefreshTime")!.GetValue(value)!).ToList();
        Equal(8, refreshTimes.Count(time => time >= 1f));
        Physics2D.Hits = []; Time.time = 0f;

        // Owners that are no longer in overlap results age out instead of being
        // refreshed forever from the person's encounter history.
        f = new Fixture();
        var distant = new GameObject("Distant"); distant.AddComponent<PhysicalBehaviour>();
        var distantCollider = distant.AddComponent<Collider2D>(); distantCollider.Surface = new Vector2(2f, 0f);
        Physics2D.Hits = [distantCollider]; Time.time = 0f; f.Adapter.Read();
        Physics2D.Hits = []; Time.time = 20f; f.Adapter.Read();
        owners = (List<PhysicalBehaviour>)ownersField.GetValue(f.Adapter)!;
        Equal(0, owners.Count);
    }

    private static void BloodAndVitality()
    {
        var f = new Fixture();
        Equal(0, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.OriginalBloodAmount = .5f;
        Equal(.5f, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.OriginalBloodAmount = 0;
        Equal(1, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.OriginalBloodAmount = 1;
        f.Limb.Vitality = .2f; // Native injury susceptibility is not remaining health.
        var frame = f.Adapter.Read();
        Equal(1, frame.Vitality);
        f.Limb.Health = 40;
        frame = f.Adapter.Read();
        Equal(.4f, frame.Vitality);
    }
    private static void InvalidLimbSamples()
    {
        var f = new Fixture();
        f.Adapter.Read(); // Establish the observed native blood baseline.
        f.Limb.CirculationBehaviour.OriginalBloodAmount = float.NaN;
        var frame = f.Adapter.Read(); True(!frame.BloodValid); Equal(0f, frame.Blood); True(f.Adapter.LiveInjurySummary.Contains("max-limb-blood-drop(observed peak)=unknown"));
        f.Limb.CirculationBehaviour.OriginalBloodAmount = -1f;
        frame = f.Adapter.Read(); True(!frame.BloodValid); Equal(0f, frame.Blood);
        f.Limb.CirculationBehaviour.OriginalBloodAmount = .5f;
        frame = f.Adapter.Read(); True(frame.BloodValid); Equal(.5f, frame.Blood);

        f = new Fixture(); f.Limb.Health = float.NaN;
        frame = f.Adapter.Read(); True(!frame.DamageValid); Equal(0f, frame.Damage); True(!frame.VitalityValid); Equal(0f, frame.Vitality);
        True(f.Adapter.LiveBodySummary.Contains("damage=unknown")); True(f.Adapter.LiveInjurySummary.Contains("min-limb-health=unknown"));
        f.Adapter.Apply(Moving, false); Equal(0f, f.Limb.MotorSpeed); True(f.Adapter.LiveLimbSummary.Contains("LowerArmFront:invalid-health"));

        f.Limb.Health = 100f;
        var healthy = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, healthy]; healthy.IsDismembered = true;
        frame = f.Adapter.Read(); Equal(.5f, frame.LimbLoss); True(frame.DamageValid); True(frame.VitalityValid);
    }
    private static void SmallHealthBaselines()
    {
        var f = new Fixture(); f.Limb.InitialHealth = .5f; f.Limb.Health = .5f;
        var frame = f.Adapter.Read(); True(frame.DamageValid); Equal(0f, frame.Damage); Equal(0f, frame.DamageEvent);
        f.Limb.Health = .25f; frame = f.Adapter.Read(); Equal(.5f, frame.Damage); Equal(.5f, frame.DamageEvent);
        Equal(0f, f.Adapter.Read().DamageEvent);
        f.Limb.InitialHealth = float.Epsilon; f.Limb.Health = float.MaxValue;
        frame = f.Adapter.Read(); Equal(0f, frame.Damage); Equal(0f, frame.DamageEvent);
        f.Limb.Health = -1f; frame = f.Adapter.Read(); Equal(1f, frame.Damage); Equal(1f, frame.DamageEvent);
    }
    private static void AcidPools()
    {
        var f = new Fixture(); var acidObject = new GameObject("Acid Spider Pool"); var acid = acidObject.AddComponent<AcidPoolBehaviour>(); acid.AcidProgress = .8f; acid.PainIntensity = .2f;
        var collider = acidObject.AddComponent<Collider2D>(); collider.Surface = new Vector2(2f, 0f);
        Physics2D.Hits = [collider];
        Equal(0f, f.Adapter.Read().AcidExposure);
        collider.Surface = new Vector2(.05f, 0f);
        var frame = f.Adapter.Read(); Equal(.8f, frame.AcidExposure); Equal("ACID", f.Adapter.LiveSignal); Equal(.8f, f.Adapter.LiveSignalValue);
    }
    private static void LimbDamageCategories()
    {
        var f = new Fixture();
        f.Limb.IsCapable = false;
        var frame = f.Adapter.Read(); Equal(0, frame.Paralysis); Equal(0, frame.Breakage); Equal(0, frame.LimbLoss);
        f.Limb.Broken = true;
        frame = f.Adapter.Read(); Equal(0, frame.Paralysis); Equal(1, frame.Breakage); Equal(0, frame.LimbLoss);
        f.Limb.Broken = false; f.Limb.IsParalysed = true;
        frame = f.Adapter.Read(); Equal(1, frame.Paralysis); Equal(0, frame.LimbLoss);
        f.Limb.IsDismembered = true;
        frame = f.Adapter.Read(); Equal(1, frame.LimbLoss);
        var second = Fixture.AddLimb(f.Root, "LowerArmBack");
        f.Person.Limbs = [f.Limb, second];
        frame = f.Adapter.Read(); Equal(.5f, frame.LimbLoss); True(f.Adapter.LiveLimbSummary.Contains("lost=1/2"));
    }
    private static void Oxygen()
    {
        var f = new Fixture(); f.Person.OxygenLevel = .2f;
        Equal(0, f.Adapter.Read().SubmergedHypoxia); Equal("LOW OXYGEN", f.Adapter.LiveSignal);
        f.Limb.PhysicalBehaviour.IsUnderWater = true; Equal(.8f, f.Adapter.Read().SubmergedHypoxia); Equal("SUBMERGED HYPOXIA", f.Adapter.LiveSignal);
    }
    private static void InvalidOxygen()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var f = new Fixture(); f.Person.OxygenLevel = bad; f.Limb.PhysicalBehaviour.IsUnderWater = true;
            var frame = f.Adapter.Read(); True(!frame.OxygenValid); Equal(0f, frame.Oxygen); Equal(0f, frame.SubmergedHypoxia);
            True(f.Adapter.LiveBodySummary.Contains("oxygen=unknown")); True(f.Adapter.LiveSignal != "LOW OXYGEN" && f.Adapter.LiveSignal != "SUBMERGED HYPOXIA");
        }
    }
    private static void InvalidConsciousness()
    {
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var f = new Fixture(); f.Person.Consciousness = bad; f.Adapter.Read();
            True(!f.Adapter.Read().ConsciousnessValid); Equal(0f, f.Adapter.Read().Unconscious); Equal("DATA LIMITED", f.Adapter.LiveState);
            True(f.Adapter.LiveBodySummary.Contains("conscious=unknown") && f.Adapter.LiveBodySummary.Contains("unconscious=unknown"));
            f.Adapter.Apply(Moving, true); Equal(0f, f.Person.DesiredWalkingDirection); Equal(0f, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);
        }
    }
    private static void InvalidCirculation()
    {
        var f = new Fixture(); f.Limb.CirculationBehaviour = null;
        var frame = f.Adapter.Read(); True(!frame.CirculationValid); Equal(0f, frame.Circulation); True(f.Adapter.LiveInjurySummary.Contains("circulation=unknown"));
        var healthyLimb = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, healthyLimb];
        f.Adapter.Read(); f.Adapter.Apply(Moving, false);
        Equal(0f, f.Limb.MotorSpeed); True(healthyLimb.MotorSpeed != 0f);
        True(f.Adapter.LiveLimbSummary.Contains("unknown-circulation"));

        f = new Fixture(); f.Limb.CirculationBehaviour.BloodFlow = float.NaN;
        frame = f.Adapter.Read(); True(!frame.CirculationValid); True(f.Adapter.LiveInjurySummary.Contains("circulation=unknown"));

        f = new Fixture(); f.Limb.CirculationBehaviour.HasBloodFlow = false; f.Limb.CirculationBehaviour.BloodFlow = float.NaN;
        frame = f.Adapter.Read(); True(frame.CirculationValid); Equal(0f, frame.Circulation);
    }
    private static void AmbientTemperature()
    {
        var f = new Fixture();
        var grid = new AmbientTemperatureGridBehaviour { Temperature = 20 }; AmbientTemperatureGridBehaviour.Instance = grid;
        Physics2D.Hits = []; var frame = f.Adapter.Read(); Equal(0f, frame.AmbientHeat); Equal(0f, frame.AmbientCold);
        grid.Temperature = 0; frame = f.Adapter.Read(); Equal(0f, frame.AmbientHeat); Equal(1f, frame.AmbientCold); Equal("AMBIENT COLD", f.Adapter.LiveSignal);
        grid.Temperature = 20;
        var hot = new GameObject("Hot object"); var hotPhysical = hot.AddComponent<PhysicalBehaviour>(); hotPhysical.Temperature = 100; hotPhysical.rigidbody = hot.AddComponent<Rigidbody2D>(); var hotCollider = hot.AddComponent<Collider2D>(); hotCollider.Surface = new Vector2(2, 0);
        Physics2D.Hits = [hotCollider]; frame = f.Adapter.Read(); True(frame.AmbientHeat > 0f); Equal(0f, frame.AmbientCold); Equal("AMBIENT HEAT", f.Adapter.LiveSignal);
        hotPhysical.Temperature = 0; frame = f.Adapter.Read(); Equal(0f, frame.AmbientHeat); True(frame.AmbientCold > 0f); Equal("AMBIENT COLD", f.Adapter.LiveSignal);
        hotCollider.Surface = new Vector2(8, 0); frame = f.Adapter.Read(); Equal(0f, frame.AmbientCold); Physics2D.Hits = [];
        var detachedOwn = new GameObject("Detached own limb"); var detachedPhysical = detachedOwn.AddComponent<PhysicalBehaviour>(); detachedPhysical.Temperature = 100; detachedPhysical.rigidbody = detachedOwn.AddComponent<Rigidbody2D>(); var detachedCollider = detachedOwn.AddComponent<Collider2D>(); detachedCollider.Surface = new Vector2(2, 0);
        f.Limb.PhysicalBehaviour = detachedPhysical; Physics2D.Hits = [detachedCollider]; Equal(0f, f.Adapter.Read().AmbientHeat); Physics2D.Hits = []; AmbientTemperatureGridBehaviour.Instance = null;
    }
    private static void NearbyLava()
    {
        var f = new Fixture();
        AmbientTemperatureGridBehaviour.Instance = new AmbientTemperatureGridBehaviour { Temperature = 20 };
        var lava = new GameObject("Lava"); var lavaBehaviour = lava.AddComponent<LavaBehaviour>(); lavaBehaviour.LavaTemperature = 100; var collider = lava.AddComponent<Collider2D>(); collider.Surface = new Vector2(2, 0);
        Physics2D.Hits = [collider]; var frame = f.Adapter.Read(); True(frame.Lava > 0f); True(frame.AmbientHeat > 0f); Equal("FIRE/LAVA", f.Adapter.LiveSignal);
        Physics2D.Hits = []; AmbientTemperatureGridBehaviour.Instance = null;
    }
    private static Collider2D SoundObject(out AudioSource audio)
    {
        var go = new GameObject("Radio"); var physical = go.AddComponent<PhysicalBehaviour>();
        audio = go.AddComponent<AudioSource>(); audio.isPlaying = true; physical.MainAudioSource = audio;
        go.transform.position = new Vector3(1, 0, 0);
        var collider = go.AddComponent<Collider2D>(); collider.Surface = new Vector2(1, 0); return collider;
    }
    private static void Audio()
    {
        var f = new Fixture(); var other = SoundObject(out var audio); Physics2D.Hits = [other];
        audio.Spectrum = new float[512]; audio.Spectrum[1] = 3; audio.Spectrum[3] = 1;
        var frame = f.Adapter.Read(); True(frame.Sound > 0 && frame.Nearby >= 0 && frame.Nearby <= 1); True(frame.SoundSpectrumValid && frame.SoundLow > frame.SoundHigh); Equal(1, audio.SpectrumCalls); Equal("OBJECT AUDIO", f.Adapter.LiveSignal);
        True(f.Adapter.LiveAudioSummary.Contains("external object Radio"));
        audio.mute = true; Equal(0, f.Adapter.Read().Sound); True(!f.Adapter.Read().SoundSpectrumValid); Equal("SENSING", f.Adapter.LiveState); audio.mute = false; audio.isActiveAndEnabled = false; Equal(0, f.Adapter.Read().Sound);
        audio.isActiveAndEnabled = true; other.Surface = new Vector2(float.NaN, 0); frame = f.Adapter.Read(); Equal(0, frame.Sound); Equal(0, frame.Nearby);
        var own = f.Limb.gameObject.AddComponent<Collider2D>(); f.Limb.PhysicalBehaviour.MainAudioSource = audio;
        Physics2D.Hits = [own]; Equal(0, f.Adapter.Read().Sound); Equal(0, f.Adapter.Read().Nearby);
        f.Limb.transform.SetParent(null); Equal(0, f.Adapter.Read().Sound); Equal(0, f.Adapter.Read().Nearby);
        Physics2D.Hits = []; Equal(0, f.Adapter.Read().Nearby);
        var ownRootPhysical = f.Root.AddComponent<PhysicalBehaviour>(); var ownRootAudio = f.Root.AddComponent<AudioSource>();
        ownRootAudio.isPlaying = true; ownRootPhysical.MainAudioSource = ownRootAudio;
        Physics2D.Hits = [f.Root.AddComponent<Collider2D>()]; Equal(0, f.Adapter.Read().Sound);
        var detachedOwnPhysicalObject = new GameObject("DetachedOwnRoot"); var detachedOwnPhysical = detachedOwnPhysicalObject.AddComponent<PhysicalBehaviour>(); var detachedOwnAudio = detachedOwnPhysicalObject.AddComponent<AudioSource>();
        detachedOwnAudio.isPlaying = true; detachedOwnPhysical.MainAudioSource = detachedOwnAudio; f.Limb.PhysicalBehaviour = detachedOwnPhysical;
        Physics2D.Hits = [detachedOwnPhysicalObject.AddComponent<Collider2D>()]; Equal(0, f.Adapter.Read().Sound);
        var generatedRoot = new GameObject("Root"); var generatedPhysical = generatedRoot.AddComponent<PhysicalBehaviour>(); var generatedAudio = generatedRoot.AddComponent<AudioSource>();
        generatedAudio.isPlaying = true; generatedPhysical.MainAudioSource = generatedAudio; var generatedCollider = generatedRoot.AddComponent<Collider2D>(); generatedCollider.Surface = new Vector2(2.14f, 0);
        Physics2D.Hits = [generatedCollider]; Equal(0, f.Adapter.Read().Sound); Equal("SENSING", f.Adapter.LiveState);
    }
    private static void NumberedRootAudio()
    {
        var f = new Fixture(); var source = SoundObject(out var audio);
        source.Surface = new Vector2(2.19f, 0); Physics2D.Hits = [source];
        foreach (var name in new[] { "Root", "Root (3)", "Root (123)", "Root(Clone)", "Root (3)(Clone)", "Root(Clone) (3)", "root (3)" })
        {
            source.gameObject.name = name;
            Equal(0f, f.Adapter.Read().Sound);
            True(f.Adapter.LiveAudioSummary.Contains("No external audio detected yet"));
        }
        foreach (var name in new[] { "Radio", "Root beer radio", "Rooted", "Root3", "Root ()", "Root (music)", "Root (3) radio" })
        {
            source.gameObject.name = name;
            True(f.Adapter.Read().Sound > 0f);
        }
        // The physical and audio objects can have different generated suffixes.
        source.gameObject.name = "Root (3)";
        var separateAudio = new GameObject("Root(Clone)").AddComponent<AudioSource>();
        separateAudio.isPlaying = true;
        source.GetComponent<PhysicalBehaviour>().MainAudioSource = separateAudio;
        Equal(0f, f.Adapter.Read().Sound);
        // Another identifiable person remains audible, even with a generic root name.
        var otherPerson = new GameObject("Other human"); otherPerson.AddComponent<PersonBehaviour>();
        source.transform.SetParent(otherPerson.transform);
        True(f.Adapter.Read().Sound > 0f);
        True(f.Adapter.LiveAudioSummary.Contains("external person Root (3)"));
        source.transform.SetParent(f.Root.transform);
        Equal(0f, f.Adapter.Read().Sound);
        Physics2D.Hits = [];
    }
    private static void AudioHistory()
    {
        var f = new Fixture();
        True(f.Adapter.LiveAudioSummary.Contains("No external audio detected yet"));
        var other = SoundObject(out var audio); Physics2D.Hits = [other];
        Time.realtimeSinceStartup = 10f;
        True(f.Adapter.Read().Sound > 0f);
        audio.isPlaying = false;
        Time.realtimeSinceStartup = 15f;
        Equal(0f, f.Adapter.Read().Sound);
        True(f.Adapter.LiveAudioSummary.Contains("LATEST SAMPLE): none"));
        True(f.Adapter.LiveAudioSummary.Contains("external object Radio"));
        True(f.Adapter.LiveAudioSummary.Contains("5.0 seconds ago"));
        Time.realtimeSinceStartup = 20f; // UI time advances even without a new physics sample.
        True(f.Adapter.LiveAudioSummary.Contains("10.0 seconds ago"));
        audio.isPlaying = true;
        True(f.Adapter.Read().Sound > 0f);
        True(f.Adapter.LiveAudioSummary.Contains("0.0 seconds ago"));
        Time.realtimeSinceStartup = 0f;
    }

    private static void Impacts()
    {
        var f = new Fixture(); var probe = f.Limb.GetComponent<PersonConnectomeLimbProbe>();
        var callback = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var own = f.Limb.gameObject.AddComponent<Collider2D>(); callback.Invoke(probe, [new Collision2D { collider = own, relativeVelocity = new Vector2(10, 0) }]); Equal(0, f.Adapter.Read().Impact);
        var floor = new GameObject("Floor").AddComponent<Collider2D>(); callback.Invoke(probe, [new Collision2D { collider = floor, relativeVelocity = new Vector2(10, 0) }]);
        var frame = f.Adapter.Read(); Equal(.5f, frame.Impact); Equal(0, frame.Sound); Equal("CONTACT IMPACT", f.Adapter.LiveSignal);
        for (var i = 0; i < 30; i++) frame = f.Adapter.Read(); True(frame.Impact < .001f);
        var soft = new Fixture(); soft.Adapter.RegisterCollision(2); frame = soft.Adapter.Read(); Equal(.1f, frame.Impact); Equal("CONTACT", soft.Adapter.LiveSignal); Equal("SENSING", soft.Adapter.LiveState);
    }
    private static void DetachedOwnCollisions()
    {
        var f = new Fixture();
        var other = Fixture.AddLimb(f.Root, "LowerArmBack");
        f.Person.Limbs = [f.Limb, other];
        f.Adapter.Read();
        other.transform.SetParent(null);
        var collider = other.gameObject.AddComponent<Collider2D>();
        other.gameObject.AddComponent<ProjectileBehaviour>();
        var probe = f.Limb.GetComponent<PersonConnectomeLimbProbe>();
        var projectileReports = 0;
        probe.ReportProjectile = _ => projectileReports++;
        var callback = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        callback.Invoke(probe, [new Collision2D { collider = collider, relativeVelocity = new Vector2(20, 0) }]);
        Equal(0, f.Adapter.Read().Impact);
        Equal(0, projectileReports);
        f.Adapter.Dispose();
        True(probe.IsOwned == null && probe.Report == null && probe.ReportProjectile == null);
    }

    private static void DetachedSourceCollisions()
    {
        var f = new Fixture();
        var detached = Fixture.AddLimb(f.Root, "DetachedArm");
        f.Person.Limbs = [f.Limb, detached];
        f.Adapter.Read();
        detached.transform.SetParent(null);
        detached.IsDismembered = true;

        var floor = new GameObject("Detached floor").AddComponent<Collider2D>();
        var projectileObject = new GameObject("Detached projectile");
        projectileObject.AddComponent<ProjectileBehaviour>();
        var projectile = projectileObject.AddComponent<Collider2D>();
        var callbackFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var enter = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", callbackFlags);
        var stay = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionStay2D", callbackFlags);
        var probe = detached.GetComponent<PersonConnectomeLimbProbe>();
        enter.Invoke(probe, [new Collision2D { collider = floor, relativeVelocity = new Vector2(20, 0) }]);
        stay.Invoke(probe, [new Collision2D { collider = floor, relativeVelocity = new Vector2(20, 0) }]);
        enter.Invoke(probe, [new Collision2D { collider = projectile, relativeVelocity = new Vector2(20, 0) }]);

        var frame = f.Adapter.Read();
        Equal(0f, frame.Impact);
        Equal(0f, frame.Vibration);
        Equal(0f, frame.Projectile);
    }

    private static void ConnectedSourceProjectiles()
    {
        var f = new Fixture(); f.Adapter.Read();
        var projectileObject = new GameObject("Connected projectile");
        projectileObject.AddComponent<ProjectileBehaviour>();
        var projectile = projectileObject.AddComponent<Collider2D>();
        var callback = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        callback.Invoke(f.Limb.GetComponent<PersonConnectomeLimbProbe>(), [new Collision2D { collider = projectile, relativeVelocity = new Vector2(20, 0) }]);
        Equal(1f, f.Adapter.Read().Projectile);
    }

    private static void DetachedLimbsDoNotControl()
    {
        var f = new Fixture();
        var detached = Fixture.AddLimb(f.Root, "DetachedArm");
        f.Person.Limbs = [f.Limb, detached];
        detached.transform.SetParent(null);
        detached.IsDismembered = true;
        detached.PhysicalBehaviour.Temperature = 100f;
        detached.CirculationBehaviour.LiquidDistribution[new Liquid("KNOCKOUT POISON")] = new() { Raw = .8f };
        var frame = f.Adapter.Read();
        Equal(0f, frame.LiquidSedation);
        Equal(0f, frame.Heat);
        Equal(.5f, frame.LimbLoss);
        True(f.Adapter.LiveLiquidSummary.Contains("KNOCKOUT POISON"));
        f.Adapter.Apply(Moving, false);
        Equal(1f, f.Person.DesiredWalkingDirection);
        True(detached.MotorSpeed == 0f);
    }

    private static void LocalDeadLimb()
    {
        var f = new Fixture();
        var healthy = Fixture.AddLimb(f.Root, "LowerArmBack");
        f.Person.Limbs = [f.Limb, healthy];
        f.Limb.Health = 0f;
        f.Adapter.Read();
        f.Adapter.Apply(Moving, true);
        Equal(0f, f.Limb.MotorSpeed);
        True(healthy.MotorSpeed != 0f);
        True(!f.Adapter.IsTerminal);
        f.Limb.Health = 100f;
        f.Limb.InitialHealth = 0f;
        var frame = f.Adapter.Read();
        Equal(0f, frame.Damage);
        f.Adapter.Apply(Moving, true);
        Equal(0f, f.Limb.MotorSpeed);
        healthy.InitialHealth = 0f;
        frame = f.Adapter.Read();
        True(!frame.DamageValid && !frame.VitalityValid);
    }

    private static void VisionRadiusUpdates()
    {
        var f = new Fixture();
        f.Adapter.Read();
        Equal(8f, Physics2D.LastOverlapRadius);
        f.Adapter.UpdateVisionRadius(3f);
        f.Adapter.Read();
        Equal(3f, Physics2D.LastOverlapRadius);
        f.Adapter.UpdateVisionRadius(float.NaN);
        f.Adapter.Read();
        Equal(8f, Physics2D.LastOverlapRadius);
    }

    private static void ControlClock()
    {
        var f = new Fixture();
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(controller, null);
        var brain = (LifBrain)type.GetField("brain", flags).GetValue(controller);
        var tick = type.GetMethod("FixedUpdate", flags);
        var previousDelta = Time.fixedDeltaTime;
        try
        {
            Time.fixedDeltaTime = .02f;
            for (var i = 0; i < 50; i++) tick.Invoke(controller, null);
            Equal(20, brain.StepCount);
            Time.fixedDeltaTime = .26f;
            tick.Invoke(controller, null);
            Equal(21, brain.StepCount);
            Equal(.26f, brain.LastElapsed);
            Time.fixedDeltaTime = .02f;
            tick.Invoke(controller, null);
            Equal(21, brain.StepCount);
        }
        finally { Time.fixedDeltaTime = previousDelta; }
    }

    private static void SuspensionClearsTransientEvents()
    {
        var f = new Fixture(); f.Adapter.Read(); f.Adapter.Apply(Moving, false);
        f.Adapter.RegisterCollision(20f); f.Adapter.RegisterProjectile(20f); f.Adapter.Suspend();
        var resumed = f.Adapter.Read();
        Equal(0f, resumed.Impact); Equal(0f, resumed.Vibration); Equal(0f, resumed.Projectile);

        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(controller, null);
        var adapter = (PeoplePlaygroundPersonAdapter)type.GetField("adapter", flags).GetValue(controller);
        type.GetMethod("OnEnable", flags).Invoke(controller, null);
        type.GetMethod("OnDisable", flags).Invoke(controller, null);
        var floor = new GameObject("Disabled floor").AddComponent<Collider2D>();
        var projectileObject = new GameObject("Disabled projectile");
        projectileObject.AddComponent<ProjectileBehaviour>();
        var projectile = projectileObject.AddComponent<Collider2D>();
        var probeType = typeof(PersonConnectomeLimbProbe);
        var enter = probeType.GetMethod("OnCollisionEnter2D", flags);
        enter.Invoke(f.Limb.GetComponent<PersonConnectomeLimbProbe>(), [new Collision2D { collider = floor, relativeVelocity = new Vector2(20, 0) }]);
        enter.Invoke(f.Limb.GetComponent<PersonConnectomeLimbProbe>(), [new Collision2D { collider = projectile, relativeVelocity = new Vector2(20, 0) }]);
        type.GetMethod("OnEnable", flags).Invoke(controller, null);
        resumed = adapter.Read();
        Equal(0f, resumed.Impact); Equal(0f, resumed.Vibration); Equal(0f, resumed.Projectile);
        enter.Invoke(f.Limb.GetComponent<PersonConnectomeLimbProbe>(), [new Collision2D { collider = projectile, relativeVelocity = new Vector2(20, 0) }]);
        Equal(1f, adapter.Read().Projectile);
    }

    private static void InitiallyDisabledControllersRejectEvents()
    {
        var f = new Fixture();
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(controller, null);
        var adapter = (PeoplePlaygroundPersonAdapter)type.GetField("adapter", flags).GetValue(controller);
        var projectileObject = new GameObject("Startup projectile");
        projectileObject.AddComponent<ProjectileBehaviour>();
        var projectile = projectileObject.AddComponent<Collider2D>();
        var enter = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", flags);
        enter.Invoke(f.Limb.GetComponent<PersonConnectomeLimbProbe>(), [new Collision2D { collider = projectile, relativeVelocity = new Vector2(20, 0) }]);
        type.GetMethod("OnEnable", flags).Invoke(controller, null);
        var frame = adapter.Read();
        Equal(0f, frame.Impact); Equal(0f, frame.Vibration); Equal(0f, frame.Projectile);
    }

    private static void DisabledControllersDoNotClaimTelemetry()
    {
        PersonConnectomeStatusDisplay.ResetForTest();
        var disabled = new Fixture();
        var disabledController = disabled.Root.AddComponent<PersonConnectomeController>();
        var enabled = new Fixture();
        var enabledController = enabled.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(disabledController, null);
        type.GetMethod("Awake", flags).Invoke(enabledController, null);
        Equal(0, PersonConnectomeStatusDisplay.ActiveCount);
        type.GetMethod("OnEnable", flags).Invoke(enabledController, null);
        Equal(1, PersonConnectomeStatusDisplay.ActiveCount);
        type.GetMethod("LateUpdate", flags).Invoke(enabledController, null);
        Equal(1, PersonConnectomeStatusDisplay.RenderedUpdates);
        type.GetMethod("OnDisable", flags).Invoke(enabledController, null);
        Equal(0, PersonConnectomeStatusDisplay.ActiveCount);
        type.GetMethod("OnDestroy", flags).Invoke(disabledController, null);
        type.GetMethod("OnDestroy", flags).Invoke(enabledController, null);
        Equal(0, PersonConnectomeStatusDisplay.ActiveCount);
        var replacement = new Fixture().Root.AddComponent<PersonConnectomeController>();
        type.GetMethod("Awake", flags).Invoke(replacement, null);
        type.GetMethod("OnEnable", flags).Invoke(replacement, null);
        Equal(1, PersonConnectomeStatusDisplay.ActiveCount);
        type.GetMethod("OnDisable", flags).Invoke(replacement, null);
        type.GetMethod("OnDestroy", flags).Invoke(replacement, null);
        Equal(0, PersonConnectomeStatusDisplay.ActiveCount);
        PersonConnectomeStatusDisplay.ResetForTest();
    }

    private static void Vision()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        var visible = new GameObject("Visible object");
        visible.AddComponent<PhysicalBehaviour>();
        var collider = visible.AddComponent<Collider2D>();
        collider.Surface = new Vector2(2, 0);
        Physics2D.Hits = [collider];
        Physics2D.LinecastResult = new RaycastHit2D { collider = collider };
        var frame = f.Adapter.Read();
        True(frame.Vision > .0f && frame.Vision <= 1f);
        Equal("VISION", f.Adapter.LiveSignal);
        var ownCollider = f.Root.AddComponent<Collider2D>();
        Physics2D.LinecastHits = [new RaycastHit2D { collider = ownCollider }, new RaycastHit2D { collider = collider }];
        frame = f.Adapter.Read();
        True(frame.Vision > 0f);
        var wall = new GameObject("Vision wall").AddComponent<Collider2D>();
        Physics2D.LinecastHits = [new RaycastHit2D { collider = ownCollider }, new RaycastHit2D { collider = wall }, new RaycastHit2D { collider = collider }];
        Equal(0f, f.Adapter.Read().Vision);
        Physics2D.LinecastHits = [];
        Physics2D.LinecastResult = default;
        Equal(0, f.Adapter.Read().Vision);
        RenderSettings.ambientLight = default;
    }
    private static void HeadRelativeVision()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        var target = new GameObject("Target"); target.AddComponent<PhysicalBehaviour>();
        var hit = target.AddComponent<Collider2D>(); hit.Surface = new Vector2(2, 2);
        Physics2D.Hits = [hit]; Physics2D.LinecastResult = new RaycastHit2D { collider = hit };
        var frame = f.Adapter.Read(); True(frame.GazeValid && frame.Vision > 0f); Equal(45f, frame.VisionHeadBearingDegrees);
        // This is observation of native pose, not integration of a requested motor value.
        f.Limb.transform.RotationDegrees = 90;
        frame = f.Adapter.Read(); Equal(90f, frame.GazeHeadingDegrees); Equal(-45f, frame.VisionHeadBearingDegrees);
        f.Limb.transform.RotationDegrees = 180;
        frame = f.Adapter.Read(); Equal(0f, frame.Vision); True(!frame.VisionHeadBearingValid); True(frame.Nearby > 0f);
        f.Limb.transform.RotationDegrees = 0; f.Limb.transform.lossyScale = new Vector3(-1, 1, 1);
        hit.Surface = new Vector2(-2, 0);
        frame = f.Adapter.Read(); True(frame.Vision > 0f); Equal(0f, frame.VisionHeadBearingDegrees);
        f.Limb.transform.lossyScale = new Vector3(0, 1, 1);
        frame = f.Adapter.Read(); True(!frame.GazeValid); Equal(0f, frame.Vision);
        f.Limb.transform.lossyScale = new Vector3(1, 1, 1); f.Limb.transform.RotationDegrees = float.NaN;
        frame = f.Adapter.Read(); True(!frame.GazeValid); Equal(0f, frame.Vision);
        f.Limb.transform.RotationDegrees = 0; f.Limb.HasBrain = false;
        frame = f.Adapter.Read(); True(!frame.GazeValid); Equal(0f, frame.Vision);
        f.Limb.HasBrain = true; f.Limb.CirculationBehaviour.IsDisconnected = true;
        frame = f.Adapter.Read(); True(!frame.GazeValid); Equal(0f, frame.Vision);
    }
    private static void VisibleTargetSelection()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        Collider2D Target(string name, float x, float y)
        {
            var go = new GameObject(name); go.AddComponent<PhysicalBehaviour>();
            var hit = go.AddComponent<Collider2D>(); hit.Surface = new Vector2(x, y); return hit;
        }
        var behind = Target("Behind", -.1f, 0); var blocked = Target("Blocked", 1, 0); var visible = Target("Visible", 3, 1);
        var wall = new GameObject("Wall").AddComponent<Collider2D>();
        Physics2D.Hits = [visible, behind, blocked];
        Physics2D.LinecastHandler = (_, end) => [new RaycastHit2D { collider = end.x == 1 ? wall : visible }];
        var frame = f.Adapter.Read(); True(frame.Vision > 0f && frame.VisionHeadBearingValid);
        Equal((1f - MathF.Sqrt(10f) / 8f), frame.Vision); Equal(2f, Physics2D.LinecastCalls);
        True(frame.Nearby > frame.Vision); // Proximity still reports the closest object, independently.
        RenderSettings.ambientLight = default; frame = f.Adapter.Read(); Equal(0f, frame.Vision); Equal(0f, frame.VisualApproach);
    }
    private static void VisualSearchLimits()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        var hits = new List<Collider2D>();
        for (var i = 0; i < 20; i++)
        {
            var go = new GameObject("Candidate"); go.AddComponent<PhysicalBehaviour>();
            var hit = go.AddComponent<Collider2D>(); hit.Surface = new Vector2(1 + i * .1f, 0); hits.Add(hit);
        }
        Physics2D.Hits = hits.AsEnumerable().Reverse().ToArray();
        var wall = new GameObject("Wall").AddComponent<Collider2D>();
        var visited = new List<float>();
        Physics2D.LinecastHandler = (_, end) => { visited.Add(end.x); return [new RaycastHit2D { collider = wall }]; };
        var frame = f.Adapter.Read(); True(frame.VisionLimited); Equal(16f, Physics2D.LinecastCalls); Equal(0f, frame.Vision);
        Equal(1f, visited[0]); Equal(2.5f, visited[^1]);
        Physics2D.Hits = [hits[0]];
        Physics2D.LinecastHandler = (_, _) => Enumerable.Repeat(new RaycastHit2D { collider = hits[0] }, 32).ToArray();
        frame = f.Adapter.Read(); True(frame.VisionLimited); Equal(0f, frame.Vision);
        Physics2D.Hits = []; frame = f.Adapter.Read(); True(!frame.VisionLimited && !frame.VisionHeadBearingValid);
    }
    private static void HeadRelativeSweep()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        var headBody = f.Limb.gameObject.AddComponent<Rigidbody2D>(); headBody.angularVelocity = 30f;
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        var go = new GameObject("Stationary target"); var physical = go.AddComponent<PhysicalBehaviour>();
        var targetBody = go.AddComponent<Rigidbody2D>(); physical.rigidbody = targetBody;
        var hit = go.AddComponent<Collider2D>(); hit.Surface = new Vector2(2, 0);
        hit.bounds = new Bounds { center = new Vector3(2, 0, 0), extents = new Vector3(.1f, .1f, 0) };
        Physics2D.Hits = [hit]; Physics2D.LinecastResult = new RaycastHit2D { collider = hit };
        var frame = f.Adapter.Read(); True(frame.VisualGeometryValid); Equal(30f, frame.VisualAngularSpeed);
        Equal(0f, frame.VisualExpansion); Equal(0f, frame.VisualApproach);
        targetBody.velocity = new Vector2(0, 2 * 30f / 57.29578f);
        frame = f.Adapter.Read(); Equal(0f, frame.VisualAngularSpeed);
        headBody.angularVelocity = float.NaN; frame = f.Adapter.Read(); True(!frame.VisualGeometryValid);
    }
    private static void StationaryHeadMotor()
    {
        var f = new Fixture();
        var head = Fixture.AddLimb(f.Root, "Head"); head.HasBrain = true;
        f.Person.Limbs = [f.Limb, head]; f.Adapter.Read();
        f.Limb.Broken = true;
        f.Adapter.Apply(new MotorCommand { Head = .5f }, false);
        // Native head influence is 0.18 of the 15 deg/s target.
        Equal(0f, f.Person.DesiredWalkingDirection); Equal(2.7f, head.MotorSpeed); Equal(0f, f.Limb.MotorSpeed);
        // The adapter requests native motor speed; it must not set head pose.
        Equal(0f, head.transform.RotationDegrees);
        head.Broken = true; f.Adapter.Apply(new MotorCommand { Head = .5f }, false); Equal(0f, head.MotorSpeed);
        head.Broken = false; f.Person.Consciousness = .1f;
        f.Adapter.Read(); f.Adapter.Apply(new MotorCommand { Head = .5f }, false); Equal(0f, head.MotorSpeed);
    }
    private static void SpatialSurroundings()
    {
        var f = new Fixture(); f.Limb.HasBrain = true; f.Limb.gameObject.AddComponent<Rigidbody2D>();
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        Collider2D Target(float y, float velocityY)
        {
            var go = new GameObject("External object"); var physical = go.AddComponent<PhysicalBehaviour>();
            var body = go.AddComponent<Rigidbody2D>(); body.velocity = new Vector2(0, velocityY); physical.rigidbody = body;
            var hit = go.AddComponent<Collider2D>(); hit.Surface = new Vector2(2, y);
            hit.bounds = new Bounds { center = new Vector3(2, y, 0), extents = new Vector3(.2f, .2f, 0) }; return hit;
        }
        var below = Target(-2, 3); var center = Target(0, 0); var above = Target(2, -3);
        Physics2D.Hits = [below, above, center];
        Physics2D.LinecastHandler = (_, end) => [new RaycastHit2D { collider = end.y < 0 ? below : end.y > 0 ? above : center }];
        var first = f.Adapter.Read(); True(first.VisualFieldValid);
        True(first.ViewClockwiseInner.Observed && first.ViewFront.Observed && first.ViewCounterclockwiseInner.Observed);
        Equal(0f, first.VisualApproach); // The nearest center object is stationary.
        True(first.ViewClockwiseInner.Expansion > 0f && first.ViewCounterclockwiseInner.Expansion > 0f);
        Equal(-45f, first.ViewClockwiseInner.BearingDegrees); Equal(45f, first.ViewCounterclockwiseInner.BearingDegrees);
        var wall = new GameObject("Occluder").AddComponent<Collider2D>();
        Physics2D.LinecastHandler = (_, end) => [new RaycastHit2D { collider = end.y > 0 ? wall : end.y < 0 ? below : center }];
        var next = f.Adapter.Read(); True(!next.ViewCounterclockwiseInner.Observed && next.ViewClockwiseInner.Observed);
        True(first.ViewCounterclockwiseInner.Observed); // Later reads must not mutate retained snapshots.
        f.Limb.transform.RotationDegrees = 180; next = f.Adapter.Read();
        for (var i = 0; i < 5; i++) True(!next.ViewAt(i).Observed);
        f.Limb.transform.RotationDegrees = 0; RenderSettings.ambientLight = default; next = f.Adapter.Read();
        for (var i = 0; i < 5; i++) True(!next.ViewAt(i).Observed && next.ViewAt(i).Strength == 0f);
        f.Limb.HasBrain = false; True(!f.Adapter.Read().VisualFieldValid);
    }
    private static void DirectionalSensors()
    {
        var f = new Fixture();
        f.Limb.HasBrain = true;
        var anchorBody = f.Limb.gameObject.AddComponent<Rigidbody2D>();
        anchorBody.velocity = new Vector2(4, -5);
        f.Person.AngleOffset = -90f;

        var loud = SoundObject(out var loudAudio);
        loud.Surface = new Vector2(6, 0); // Collider geometry is not the sound bearing.
        loud.transform.position = new Vector3(-2, 0, 0);
        loudAudio.volume = 1f;
        var quiet = SoundObject(out var quietAudio);
        quiet.Surface = new Vector2(-6, 0);
        quiet.transform.position = new Vector3(2, 0, 0);
        quietAudio.volume = .1f;
        Physics2D.Hits = [quiet, loud];
        var frame = f.Adapter.Read();
        True(frame.Sound > 0f && frame.SoundDirectionValid); Equal(-1f, frame.SoundDirection);
        True(frame.VelocityValid); Equal(.4f, frame.VelocityX); Equal(-.5f, frame.VelocityY);
        True(frame.TiltValid); Equal(-.5f, frame.SignedTilt);

        var visible = new GameObject("Closing target");
        var physical = visible.AddComponent<PhysicalBehaviour>();
        var targetBody = visible.AddComponent<Rigidbody2D>(); physical.rigidbody = targetBody;
        targetBody.velocity = new Vector2(-5, 0);
        var target = visible.AddComponent<Collider2D>(); target.Surface = new Vector2(2, 0);
        target.bounds = new Bounds { center = new Vector3(2, 0, 0), extents = new Vector3(1, 1, 0) };
        visible.transform.position = new Vector3(2, 0, 0);
        RenderSettings.ambientLight = new Color { grayscale = 1f };
        Physics2D.Hits = [target]; Physics2D.LinecastResult = new RaycastHit2D { collider = target };
        frame = f.Adapter.Read();
        True(frame.VisionDirectionValid); Equal(1f, frame.VisionDirection); True(frame.VisualApproach > 0f); True(frame.VisualGeometryValid); True(frame.VisualAngularSize > 0f && frame.VisualExpansion > 0f);
        target.Surface = new Vector2(-2, 0); // Vision direction follows the current LOS point.
        frame = f.Adapter.Read(); Equal(0f, frame.Vision); True(!frame.VisionDirectionValid);
        f.Limb.transform.lossyScale = new Vector3(-1, 1, 1);
        frame = f.Adapter.Read(); Equal(-1f, frame.VisionDirection); Equal(0f, frame.VisionHeadBearingDegrees);
        f.Limb.transform.lossyScale = new Vector3(1, 1, 1);
        target.Surface = new Vector2(2, 0); targetBody.velocity = new Vector2(5, 0); frame = f.Adapter.Read(); Equal(0f, frame.VisualApproach);
        Physics2D.LinecastResult = default; frame = f.Adapter.Read(); Equal(0f, frame.Vision); True(!frame.VisionDirectionValid); Equal(0f, frame.VisualApproach); True(!frame.VisualGeometryValid);

        var joint = f.Limb.gameObject.AddComponent<HingeJoint2D>();
        joint.connectedBody = new GameObject("Joint body").AddComponent<Rigidbody2D>(); joint.jointAngle = -90f; joint.jointSpeed = -45f; f.Limb.Joint = joint;
        frame = f.Adapter.Read(); True(frame.JointSensingValid); Equal(.5f, frame.JointPosition); Equal(.25f, frame.JointMotion);
        joint.jointAngle = float.NaN; frame = f.Adapter.Read(); True(!frame.JointSensingValid); Equal(0f, frame.JointPosition); Equal(0f, frame.JointMotion);
        f.Limb.Joint = null; frame = f.Adapter.Read(); True(!frame.JointSensingValid);

        var secondLimb = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, secondLimb];
        f.Limb.JointStress = 0f; joint.jointAngle = 0f; joint.jointSpeed = 0f; f.Limb.Joint = joint;
        secondLimb.JointStress = 100f; // Injury telemetry remains visible, but this limb has no readable local joint.
        frame = f.Adapter.Read(); True(frame.JointSensingValid); Equal(1f, frame.JointStress); Equal(0f, frame.NeuralJointLoad);
        f.Limb.JointStress = float.NaN; frame = f.Adapter.Read(); True(frame.JointSensingValid); Equal(0f, frame.NeuralJointLoad);
        f.Limb.Joint = null;
        var disconnectedJoint = secondLimb.gameObject.AddComponent<HingeJoint2D>();
        disconnectedJoint.connectedBody = new GameObject("Disconnected joint body").AddComponent<Rigidbody2D>(); disconnectedJoint.jointAngle = 90f; disconnectedJoint.jointSpeed = 90f; secondLimb.Joint = disconnectedJoint;
        secondLimb.IsDismembered = true; secondLimb.CirculationBehaviour.IsDisconnected = true;
        frame = f.Adapter.Read(); True(!frame.JointSensingValid); Equal(0f, frame.NeuralJointLoad); Equal(0f, frame.JointStress);

        RenderSettings.ambientLight = new Color { grayscale = float.NaN };
        frame = f.Adapter.Read(); True(!frame.LightValid); Equal(0f, frame.Light); Equal(0f, frame.VisualApproach);
        Physics2D.Hits = []; Physics2D.LinecastResult = default; RenderSettings.ambientLight = default;
        frame = f.Adapter.Read(); True(frame.LightValid); Equal(0f, frame.Sound); True(!frame.SoundDirectionValid);
        loud.transform.position = new Vector3(); Physics2D.Hits = [loud]; frame = f.Adapter.Read();
        True(frame.Sound > 0f && !frame.SoundDirectionValid); Equal(0f, frame.SoundDirection);
    }
    private static void ObservedBodySensors()
    {
        var f = new Fixture();
        f.Limb.IsOnFloor = true;
        var frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent); True(frame.RegionalTouchValid); Equal(1f, frame.TouchArms);
        f.Limb.Health = 50f; frame = f.Adapter.Read(); Equal(.5f, frame.DamageEvent);
        frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent);
        f.Limb.Health = 80f; frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent);
        f.Limb.InitialHealth = 200f; f.Limb.Health = 40f; frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent); // Baseline changes re-prime.
        f.Limb.Health = float.NaN; frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent);
        f.Limb.Health = 40f; frame = f.Adapter.Read(); Equal(0f, frame.DamageEvent); // Invalid samples re-prime.
        f.Limb.IsDismembered = true; f.Limb.Health = 10f; frame = f.Adapter.Read(); Equal(.2f, frame.DamageEvent); Equal(0f, f.Adapter.Read().DamageEvent);
        f.Limb.IsDismembered = false; f.Limb.IsOnFloor = false; f.Limb.PhysicalBehaviour.IsUnderWater = true; frame = f.Adapter.Read(); True(!frame.RegionalTouchValid);
        f.Limb.Health = 20f; f.Adapter.Read(); f.Limb.Health = 0f; Equal(.1f, f.Adapter.Read().DamageEvent); Equal(0f, f.Adapter.Read().DamageEvent);
        f.Limb.Health = 100f; f.Adapter.Read(); f.Limb.CirculationBehaviour.IsDisconnected = true; f.Limb.Health = 50f; Equal(0f, f.Adapter.Read().DamageEvent);
        f.Limb.CirculationBehaviour.IsDisconnected = false; f.Adapter.Read();
        f.Person.AverageHealth = float.NaN; f.Limb.Health = 40f; Equal(0f, f.Adapter.Read().DamageEvent);
        f.Person.AverageHealth = 1f; f.Limb.Health = 30f; Equal(0f, f.Adapter.Read().DamageEvent);
        f.Adapter.Suspend(); f.Limb.Health = 20f; Equal(0f, f.Adapter.Read().DamageEvent);
        f.Limb.gameObject.name = "MiddleBody"; f.Limb.PhysicalBehaviour.IsTouchingSomething = true;
        frame = f.Adapter.Read(); True(frame.RegionalTouchValid); Equal(1f, frame.TouchCore);
        f.Limb.gameObject.name = "MysteryPart"; frame = f.Adapter.Read(); True(!frame.RegionalTouchValid);

    }
    private static void SpectrumValidity()
    {
        var f = new Fixture(); var near = SoundObject(out var audio); var far = SoundObject(out var otherAudio);
        near.transform.position = new Vector3(1, 0, 0); far.transform.position = new Vector3(4, 0, 0);
        AudioSettings.outputSampleRate = 48000;
        audio.Spectrum = new float[512]; audio.Spectrum[0] = 100; audio.Spectrum[1] = 3; audio.Spectrum[3] = 1;
        Physics2D.Hits = [far, near, near];
        var frame = f.Adapter.Read(); True(frame.SoundSpectrumValid);
        True(Math.Abs(frame.SoundLow / frame.Sound - .9f) < .0001f); True(Math.Abs(frame.SoundHigh / frame.Sound - .1f) < .0001f);
        Equal(1, audio.SpectrumCalls); Equal(0, otherAudio.SpectrumCalls);
        var buffer = audio.LastSpectrumBuffer; f.Adapter.Read(); True(ReferenceEquals(buffer, audio.LastSpectrumBuffer));
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, -1f })
        { audio.Spectrum[1] = value; frame = f.Adapter.Read(); True(!frame.SoundSpectrumValid); Equal(0f, frame.SoundLow); Equal(0f, frame.SoundHigh); True(frame.Sound > 0f); }
        audio.Spectrum = []; frame = f.Adapter.Read(); True(!frame.SoundSpectrumValid);
        audio.ThrowSpectrum = true; frame = f.Adapter.Read(); True(!frame.SoundSpectrumValid); audio.ThrowSpectrum = false;
        AudioSettings.outputSampleRate = 0; var calls = audio.SpectrumCalls; f.Adapter.Read(); Equal(calls, audio.SpectrumCalls); AudioSettings.outputSampleRate = 48000;
        otherAudio.Spectrum = new float[512]; otherAudio.Spectrum[4] = 1f; audio.mute = true;
        frame = f.Adapter.Read(); True(frame.SoundSpectrumValid); Equal(0f, frame.SoundLow); Equal(frame.Sound, frame.SoundHigh); Equal(calls, audio.SpectrumCalls);
        Physics2D.Hits = []; frame = f.Adapter.Read(); True(!frame.SoundSpectrumValid); Equal(0f, frame.SoundHigh);
    }

    private static void EnvironmentTelemetry()
    {
        var f = new Fixture();
        f.Limb.PhysicalBehaviour.IsUnderWater = true;
        var frame = f.Adapter.Read();
        var text = f.Adapter.LiveEnvironmentSummary;
        foreach (var label in new[] { "fire=", "lava=", "acid=", "burn=", "heat=", "cold=", "ambient-heat=", "ambient-cold=", "light=", "nearby=", "direction-world-x=", "vision=", "sound=", "impact=", "vibration=", "projectile=", "touch=", "contact/held=", "wet=", "submerged-hypoxia=", "liquid=", "hazard-exposure=", "sedative-exposure=", "stimulant-exposure=", "restorative-exposure=", "charge=", "stabbed=", "weightless=", "sliding=" })
            True(text.Contains(label));
        True(text.Contains("underwater=" + frame.UnderWater.ToString("0.00")));
        True(text.Contains("visual bounds geometry=unknown"));
        True(text.Contains("source spectrum energy (below 100 / 100+ Hz)=unavailable"));
    }

    private static void GeometryAndRotation()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        var anchor = f.Limb.gameObject.AddComponent<Rigidbody2D>(); anchor.angularVelocity = -90f;
        var target = new GameObject("Target"); var physical = target.AddComponent<PhysicalBehaviour>();
        var body = target.AddComponent<Rigidbody2D>(); physical.rigidbody = body; body.velocity = new Vector2(-1, 2);
        var collider = target.AddComponent<Collider2D>(); collider.Surface = new Vector2(1, 0);
        collider.bounds = new Bounds { center = new Vector3(2, 0, 0), extents = new Vector3(1, 1, 0) };
        Physics2D.Hits = [collider]; Physics2D.LinecastResult = new RaycastHit2D { collider = collider }; RenderSettings.ambientLight = new Color { grayscale = 1f };
        var frame = f.Adapter.Read(); True(frame.VisualGeometryValid); True(Math.Abs(frame.VisualAngularSize - 53.1301f) < .001f);
        True(Math.Abs(frame.VisualExpansion - 22.9183f) < .001f); True(Math.Abs(frame.VisualAngularSpeed - 147.2958f) < .001f);
        True(frame.AngularVelocityValid); Equal(-90f, frame.AngularVelocity);
        body.velocity = new Vector2(1, 0); frame = f.Adapter.Read(); Equal(0f, frame.VisualExpansion); Equal(90f, frame.VisualAngularSpeed);
        foreach (var radius in new[] { 0f, float.NaN, float.PositiveInfinity })
        { collider.bounds = new Bounds { center = new Vector3(2, 0, 0), extents = new Vector3(radius, 0, 0) }; True(!f.Adapter.Read().VisualGeometryValid); }
        collider.bounds = new Bounds { center = new Vector3(2, 0, 0), extents = new Vector3(1, 1, 0) };
        anchor.angularVelocity = float.NaN; frame = f.Adapter.Read(); True(!frame.AngularVelocityValid); Equal(0f, frame.AngularVelocity);
        RenderSettings.ambientLight = default; True(!f.Adapter.Read().VisualGeometryValid);
        RenderSettings.ambientLight = new Color { grayscale = 1f }; body.velocity = new Vector2(float.NaN, 0); True(!f.Adapter.Read().VisualGeometryValid);
        Physics2D.LinecastResult = default; True(!f.Adapter.Read().VisualGeometryValid); Physics2D.Hits = []; RenderSettings.ambientLight = default;
    }

    private static void AdditionalSenses()
    {
        var f = new Fixture(); f.Limb.HasBrain = true;
        f.Person.AngleOffset = 90f;
        f.Person.BalanceOffset = 5f;
        f.Limb.JointStress = 100f;
        f.Adapter.RegisterCollision(10f);
        var frame = f.Adapter.Read();
        True(frame.Vibration > 0f);
        True(frame.Proprioception > 0f);
        Equal(0f, frame.Projectile);

        var penetrationOnly = new GameObject("Penetration-only object");
        var penetrationPhysical = penetrationOnly.AddComponent<PhysicalBehaviour>(); penetrationPhysical.BulletPenetration = true;
        var penetrationCollider = penetrationOnly.AddComponent<Collider2D>(); penetrationCollider.Surface = new Vector2(1, 0);
        Physics2D.Hits = [penetrationCollider]; Equal(0, f.Adapter.Read().Projectile);
        var realProjectile = new GameObject("Native projectile"); var realProjectilePhysical = realProjectile.AddComponent<PhysicalBehaviour>(); realProjectile.AddComponent<ProjectileBehaviour>();
        var realProjectileBody = realProjectile.AddComponent<Rigidbody2D>(); realProjectilePhysical.rigidbody = realProjectileBody;
        var projectileCollider = realProjectile.AddComponent<Collider2D>(); projectileCollider.Surface = new Vector2(1, 0);
        Physics2D.Hits = [projectileCollider]; Equal(0, f.Adapter.Read().Projectile);
        realProjectileBody.velocity = new Vector2(0, 8); RenderSettings.ambientLight = new Color { grayscale = 1f }; Physics2D.LinecastResult = new RaycastHit2D { collider = projectileCollider }; True(f.Adapter.Read().Projectile > 0f); Physics2D.Hits = []; Physics2D.LinecastResult = default; RenderSettings.ambientLight = default;

        f.Adapter.RegisterProjectile(0f);
        frame = f.Adapter.Read();
        Equal(0f, frame.Projectile);
        f.Adapter.RegisterProjectile(20f);
        frame = f.Adapter.Read();
        Equal(1f, frame.Projectile);
        True(f.Adapter.LiveEnvironmentSummary.Contains("projectile=" + frame.Projectile.ToString("0.00")));
    }
    private static void Falling()
    {
        var f = new Fixture();
        var body = f.Limb.gameObject.AddComponent<Rigidbody2D>(); f.Limb.PhysicalBehaviour.rigidbody = body;
        f.Person.IsTouchingFloor = false; f.Limb.IsOnFloor = false; body.velocity = new Vector2(0, -6);
        var frame = f.Adapter.Read(); Equal(.5f, frame.Fall); Equal("FALLING", f.Adapter.LiveSignal);
        body.velocity = new Vector2(4, 0); Equal(0, f.Adapter.Read().Fall);
        body.velocity = new Vector2(0, -12); f.Person.IsTouchingFloor = true; Equal(0, f.Adapter.Read().Fall);
    }
    private static void Chemistry()
    {
        var f = new Fixture(); f.Limb.RegenerationSpeed = .4f; f.Limb.CirculationBehaviour.BloodRegenerationPerSecond = .6f;
        f.Adapter.Read(); f.Adapter.Apply(new MotorCommand { Heal = .2f }, true); Equal(.4f, f.Limb.RegenerationSpeed); Equal(.6f, f.Limb.CirculationBehaviour.BloodRegenerationPerSecond);
        f.Adapter.Apply(Moving, true); Equal(.8f, f.Limb.RegenerationSpeed); f.Adapter.Stop(); Equal(.4f, f.Limb.RegenerationSpeed); Equal(.6f, f.Limb.CirculationBehaviour.BloodRegenerationPerSecond);
        f.Adapter.Apply(Moving, true); f.Limb.RegenerationSpeed = .9f; f.Limb.CirculationBehaviour.BloodRegenerationPerSecond = .95f; f.Adapter.Dispose(); Equal(.9f, f.Limb.RegenerationSpeed); Equal(.95f, f.Limb.CirculationBehaviour.BloodRegenerationPerSecond);
    }
    private static void SideRouting()
    {
        var root = new GameObject("Human"); var front = new GameObject("FrontArm"); front.transform.SetParent(root.transform);
        var back = new GameObject("BackArm"); back.transform.SetParent(root.transform);
        var a = Fixture.AddLimb(front, "LowerArm"); var b = Fixture.AddLimb(back, "LowerArm");
        a.transform.position = new Vector3(-4, 0, 0); b.transform.position = new Vector3(-4, 0, 0);
        new PersonConnectomeLimbController(a, root.transform).Apply(Moving); new PersonConnectomeLimbController(b, root.transform).Apply(Moving);
        True(a.MotorSpeed > 0 && b.MotorSpeed < 0);
    }
    private static void OptionalControls()
    {
        var f = new Fixture(); f.Limb.GripBehaviour = null; f.Limb.HasJoint = false;
        var foot = Fixture.AddLimb(f.Root, "FootFront"); f.Adapter.Read(); f.Adapter.Apply(Moving, false); True(foot.MotorSpeed > 0); Equal(1, f.Person.DesiredWalkingDirection); True(f.Adapter.LiveLimbSummary.Contains("LowerArmFront:no-joint"));
    }
    private static void NeutralGrip()
    {
        var f = new Fixture(); f.Adapter.Read(); f.Limb.GripBehaviour.isHolding = true;
        f.Adapter.Apply(default, false);
        True(f.Limb.GripBehaviour.isHolding);
        f.Adapter.Stop();
        True(!f.Limb.GripBehaviour.isHolding);
    }
    private static void LocalLimbDamage()
    {
        var f = new Fixture(); var healthy = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, healthy];
        f.Limb.Broken = true; f.Adapter.Read(); f.Adapter.Apply(Moving, false);
        Equal(0f, f.Limb.MotorSpeed); True(healthy.MotorSpeed != 0f);
    }
    private static void NativeTelemetryTrace()
    {
        var f = new Fixture();
        f.Person.AverageHealth = .75f; f.Person.PainLevel = .2f;
        f.Person.OxygenLevel = .6f; f.Person.Consciousness = .9f;
        f.Person.AverageSpeed = 0f; f.Person.AngleOffset = 18f;
        f.Person.AdrenalineLevel = 2.5f;
        var frame = f.Adapter.Read();
        Equal(.75f, frame.Health); Equal(.2f, frame.Pain); Equal(.6f, frame.Oxygen);
        Equal(.9f, frame.Consciousness); Equal(.1f, frame.Unconscious);
        Equal(0f, frame.Velocity); Equal(.1f, frame.Rotation);
        Equal(1f, frame.Adrenaline);
        True(f.Adapter.LiveBodySummary.Contains("adrenaline(raw)=" + 2.5f.ToString("0.00")));
        f.Adapter.Apply(new MotorCommand { Walk = .7f }, false);
        Equal(1f, f.Person.DesiredWalkingDirection);
        Equal(0f, f.Adapter.Read().Velocity);
        f.Person.AverageSpeed = 2f;
        Equal(.2f, f.Adapter.Read().Velocity);
    }

    private static void NativeMotorUnits()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Adapter.Apply(new MotorCommand { Walk = -.46f, RightArm = .5f }, false);
        Equal(-.92f, f.Person.DesiredWalkingDirection);
        Equal(3.3f, f.Limb.MotorSpeed); // 0.5 * 30 degrees/s, blended by native 0.22 influence.
        f.Limb.MotorSpeed = 0f;
        f.Adapter.Apply(new MotorCommand { Walk = 10f, RightArm = 10f }, false, 10000f, 10000f);
        Equal(1f, f.Person.DesiredWalkingDirection);
        Equal(26.4f, f.Limb.MotorSpeed); // Configured target is capped at 120 degrees/s.
        f.Limb.MotorSpeed = 0f;
        f.Adapter.Apply(new MotorCommand { Walk = -.46f, RightArm = .5f }, false, 1f, 1f);
        Equal(-.55f, f.Person.DesiredWalkingDirection); Equal(.11f, f.Limb.MotorSpeed);
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            f.Limb.MotorSpeed = 10f;
            f.Adapter.Apply(new MotorCommand { Walk = 1f, RightArm = 1f }, false, invalid, invalid);
            Equal(0f, f.Person.DesiredWalkingDirection); Equal(0f, f.Limb.MotorSpeed);
            True(f.Adapter.LiveLimbSummary.Contains("invalid (stopped)"));
        }
    }

    private static void FreezeCutoff()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Limb.MotorSpeed = 10f; f.Limb.GripBehaviour.isHolding = true;
        f.Adapter.Apply(new MotorCommand { Freeze = .49f }, false);
        Equal(7.8f, f.Limb.MotorSpeed); True(f.Limb.GripBehaviour.isHolding);

        f.Limb.MotorSpeed = 10f; f.Limb.GripBehaviour.isHolding = true;
        f.Adapter.Apply(new MotorCommand { Freeze = .5f }, false);
        Equal(0f, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);

        f.Limb.MotorSpeed = 10f; f.Limb.GripBehaviour.isHolding = true;
        f.Adapter.Apply(new MotorCommand { Freeze = .51f }, false);
        Equal(0f, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);
    }

    private static void WalkingRequestMaintenance()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Adapter.Apply(new MotorCommand { Walk = .26f }, false);
        Equal(.55f, f.Person.DesiredWalkingDirection);
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetField("adapter", flags).SetValue(controller, f.Adapter);
        var order = (DefaultExecutionOrderAttribute)type.GetCustomAttributes(typeof(DefaultExecutionOrderAttribute), false).Single();
        Equal(-1000f, order.Order);
        f.Person.DesiredWalkingDirection = .45f; // Simulate native decay before the next neural tick.
        type.GetMethod("LateUpdate", flags).Invoke(controller, null);
        Equal(.55f, f.Person.DesiredWalkingDirection);

        f.Adapter.Stop();
        f.Person.DesiredWalkingDirection = .45f;
        type.GetMethod("LateUpdate", flags).Invoke(controller, null);
        Equal(.45f, f.Person.DesiredWalkingDirection);
    }

    private static void WalkingMaintenanceChecksCurrentState()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Adapter.Apply(new MotorCommand { Walk = .3f, Avoid = 1f }, false);
        f.Person.Braindead = true;
        f.Person.DesiredWalkingDirection = 0f;
        f.Adapter.RefreshWalkingRequest();
        Equal(0f, f.Person.DesiredWalkingDirection);
    }

    private static void FiniteInputs()
    {
        var f = new Fixture(); f.Adapter.RegisterCollision(float.NaN); f.Adapter.RegisterCollision(float.PositiveInfinity);
        f.Person.PainLevel = float.NaN; f.Person.AverageSpeed = float.PositiveInfinity; f.Person.BrainDamagedTime = float.NegativeInfinity;
        f.Limb.BodyTemperature = float.PositiveInfinity; f.Limb.InternalTemperature = float.NegativeInfinity; f.Limb.PhysicalBehaviour.Temperature = float.NaN;
        var frame = f.Adapter.Read(); Equal(0, frame.Impact); Equal(0, frame.Pain); Equal(0, frame.Velocity); Equal(0, frame.Heat); Equal(0, frame.Cold); Equal(0, frame.BrainDamage);
        f.Adapter.Apply(new MotorCommand { Walk = float.NaN, RightArm = float.NaN, RightGrip = float.NaN }, true); Equal(0, f.Person.DesiredWalkingDirection); Equal(0, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);
    }
    private static void NeutralChemistry()
    {
        var f = new Fixture(); f.Person.AdrenalineLevel = 1.5f; f.Adapter.Read();
        f.Adapter.Apply(default, true); Equal(1.5f, f.Person.AdrenalineLevel);
        f.Adapter.Apply(new MotorCommand { Stimulate = .5f, Calm = .5f }, true); Equal(1.5f, f.Person.AdrenalineLevel);
    }
    private static void NativeAdrenalineRange()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Person.AdrenalineLevel = 2.5f;
        f.Adapter.Apply(new MotorCommand { Stimulate = 1f }, true); Equal(2.55f, f.Person.AdrenalineLevel);
        f.Adapter.Apply(new MotorCommand { Calm = 1f }, true); Equal(2.5f, f.Person.AdrenalineLevel);
        f.Person.AdrenalineLevel = 19.99f;
        f.Adapter.Apply(new MotorCommand { Stimulate = 1f }, true); Equal(20f, f.Person.AdrenalineLevel);
        f.Person.AdrenalineLevel = .01f;
        f.Adapter.Apply(new MotorCommand { Calm = 1f }, true); Equal(0f, f.Person.AdrenalineLevel);
        f.Person.AdrenalineLevel = 2.5f;
        f.Adapter.Apply(new MotorCommand { Calm = 1f }, false); Equal(2.5f, f.Person.AdrenalineLevel);
    }

    private static void HazardWalkingKeepsNativeGate()
    {
        var f = new Fixture(); f.Adapter.Read();
        f.Adapter.Apply(new MotorCommand { Walk = .3f, Avoid = 1f }, false);
        Equal(.55f, f.Person.DesiredWalkingDirection);
    }

    private static void ChemistryScalesWithElapsedTime()
    {
        var first = new Fixture(); first.Adapter.Read(); first.Person.AdrenalineLevel = 1f; first.Limb.PhysicalBehaviour.BurnIntensity = 1f;
        for (var i = 0; i < 5; i++) first.Adapter.Apply(new MotorCommand { Stimulate = 1f, Extinguish = 1f }, true, 30f, 2f, .1f);

        var second = new Fixture(); second.Adapter.Read(); second.Person.AdrenalineLevel = 1f; second.Limb.PhysicalBehaviour.BurnIntensity = 1f;
        for (var i = 0; i < 10; i++) second.Adapter.Apply(new MotorCommand { Stimulate = 1f, Extinguish = 1f }, true, 30f, 2f, .05f);

        Equal(first.Person.AdrenalineLevel, second.Person.AdrenalineLevel);
        Equal(first.Limb.PhysicalBehaviour.BurnIntensity, second.Limb.PhysicalBehaviour.BurnIntensity);
        Equal(.5f, first.Limb.PhysicalBehaviour.BurnIntensity);
        Equal(1.5f, first.Person.AdrenalineLevel);
    }
    private static void NeverActivated()
    {
        var f = new Fixture(); f.Person.DesiredWalkingDirection = .7f;
        f.Limb.MotorSpeed = 9; f.Limb.GripBehaviour.isHolding = true;
        f.Limb.RegenerationSpeed = .3f;
        f.Adapter.Stop(); f.Adapter.Dispose();
        Equal(.7f, f.Person.DesiredWalkingDirection); Equal(9, f.Limb.MotorSpeed);
        True(f.Limb.GripBehaviour.isHolding); Equal(.3f, f.Limb.RegenerationSpeed);
    }
    private static void Disable()
    {
        var f = new Fixture(); f.Adapter.Read(); f.Adapter.Apply(Moving, true);
        var controller = f.Root.AddComponent<PersonConnectomeController>(); var type = typeof(PersonConnectomeController);
        type.GetField("adapter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(controller, f.Adapter);
        type.GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(0, f.Person.DesiredWalkingDirection); Equal(0, f.Limb.MotorSpeed); Equal(0, f.Limb.RegenerationSpeed);
        True(!f.Adapter.HasSample);
        True(f.Adapter.LiveState == "INITIALIZING");
        type.GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        True(!f.Adapter.HasSample);
        f.Adapter.Read();
        True(f.Adapter.HasSample);
    }
    private static void NativeLightSources()
    {
        foreach (var kind in new[] { "tube", "bulb", "led", "toggle", "flood", "attachment", "native" })
        {
            var f = new Fixture(); f.Limb.HasBrain = true; RenderSettings.ambientLight = new Color { grayscale = .2f };
            var lamp = new GameObject(kind); lamp.AddComponent<PhysicalBehaviour>();
            var hit = lamp.AddComponent<Collider2D>(); hit.Surface = new Vector2(1f, 0f);
            var sprite = lamp.AddComponent<SpriteRenderer>();
            sprite.sprite = new Sprite { bounds = new Bounds { extents = new Vector3(2f, 2f, 0f) } };
            sprite.transform.position = new Vector3(1f, 0f, 0f);
            switch (kind)
            {
                case "tube": lamp.AddComponent<GlowtubeBehaviour>().LightSprite = sprite; break;
                case "bulb": lamp.AddComponent<BulbBehaviour>().LightSprite = sprite; break;
                case "led": lamp.AddComponent<LEDBulbBehaviour>().LightSprite = sprite; break;
                case "toggle": lamp.AddComponent<ActivationToggleBehaviour>().LightObject = lamp; break;
                case "flood": lamp.AddComponent<SingleFloodlightBehaviour>().ToToggle = lamp; break;
                case "attachment": lamp.AddComponent<FlashlightAttachmentBehaviour>().Lights = [sprite]; break;
                case "native": lamp.AddComponent<LightSprite>().SpriteRenderer = sprite; break;
            }
            Physics2D.Hits = [hit, hit]; Physics2D.LinecastResult = new RaycastHit2D { collider = hit };
            var frame = f.Adapter.Read(); True(frame.LightValid); Equal(.2f, frame.AmbientLight);
            Equal(.5f, frame.LocalLight); Equal(.7f, frame.Light); Equal(1, frame.LocalLightSources);
            True(frame.Vision > 0f);
            RenderSettings.ambientLight = new Color { grayscale = float.NaN };
            frame = f.Adapter.Read(); True(!frame.LightValid); Equal(0f, frame.Vision);
            RenderSettings.ambientLight = new Color { grayscale = .2f };
            lamp.AddComponent<LightSprite>().SpriteRenderer = sprite; // Same renderer through a second native owner.
            Equal(1, f.Adapter.Read().LocalLightSources);
            sprite.enabled = false; frame = f.Adapter.Read(); Equal(.2f, frame.Light); Equal(0, frame.LocalLightSources);
            sprite.enabled = true; lamp.activeSelf = false; Equal(.2f, f.Adapter.Read().Light);
            lamp.activeSelf = true; sprite.color = new Color(1f, 1f, 1f, 0f); Equal(.2f, f.Adapter.Read().Light);
        }
        Physics2D.Hits = []; Physics2D.LinecastResult = default; RenderSettings.ambientLight = default;
    }

    private static void LocalLightGeometry()
    {
        var f = new Fixture(); RenderSettings.ambientLight = default;
        f.Root.transform.position = new Vector3(2f, 0f, 0f);
        var lamp = new GameObject("beam"); lamp.AddComponent<PhysicalBehaviour>();
        var hit = lamp.AddComponent<Collider2D>(); hit.Surface = new Vector2(1f, 0f);
        var sprite = lamp.AddComponent<SpriteRenderer>();
        sprite.sprite = new Sprite { bounds = new Bounds { center = new Vector3(2f, 0f, 0f), extents = new Vector3(2f, 1f, 0f) } };
        Physics2D.Hits = [hit];
        Equal(0f, f.Adapter.Read().LocalLight); // An arbitrary bright sprite is not a light source.
        var light = lamp.AddComponent<LightSprite>(); light.SpriteRenderer = sprite;
        Equal(1f, f.Adapter.Read().LocalLight);
        sprite.transform.RotationDegrees = 180f; Equal(0f, f.Adapter.Read().LocalLight);
        sprite.transform.RotationDegrees = 0f; sprite.flipX = true; Equal(0f, f.Adapter.Read().LocalLight);
        sprite.flipX = false; sprite.transform.lossyScale = new Vector3(0f, 1f, 1f); Equal(0f, f.Adapter.Read().LocalLight);
        sprite.transform.lossyScale = new Vector3(1f, 1f, 1f); light.Brightness = .5f; Equal(.5f, f.Adapter.Read().LocalLight);
        light.Brightness = float.NaN; Equal(0f, f.Adapter.Read().LocalLight);
        light.Brightness = 1f; sprite.color = new Color(float.NaN, 1f, 1f, 1f); Equal(0f, f.Adapter.Read().LocalLight);
        sprite.color = new Color(1f, 1f, 1f, 1f); sprite.drawMode = SpriteDrawMode.Sliced; Equal(0f, f.Adapter.Read().LocalLight);
        sprite.size = new Vector2(4f, 2f); Equal(1f, f.Adapter.Read().LocalLight);
        sprite.drawMode = SpriteDrawMode.Tiled; Equal(1f, f.Adapter.Read().LocalLight);
        sprite.drawMode = SpriteDrawMode.Simple; sprite.sprite = null; Equal(0f, f.Adapter.Read().LocalLight);
        Physics2D.Hits = []; RenderSettings.ambientLight = default;
    }

    private static void LocalLightLimits()
    {
        var f = new Fixture(); RenderSettings.ambientLight = new Color { grayscale = .1f };
        var lamp = new GameObject("many lights"); lamp.AddComponent<PhysicalBehaviour>();
        var hit = lamp.AddComponent<Collider2D>(); hit.Surface = new Vector2(1f, 0f);
        lamp.AddComponent<ActivationToggleBehaviour>().LightObject = lamp;
        for (var index = 0; index < 65; index++)
        {
            var child = new GameObject("light"); child.transform.SetParent(lamp.transform);
            var sprite = child.AddComponent<SpriteRenderer>(); sprite.color = new Color(.5f, .5f, .5f, 1f);
            sprite.sprite = new Sprite { bounds = new Bounds { extents = new Vector3(2f, 2f, 0f) } };
        }
        Physics2D.Hits = [hit]; var frame = f.Adapter.Read();
        Equal(64, frame.LocalLightSources); True(frame.LocalLightLimited);
        Equal(.5f, frame.LocalLight); Equal(.6f, frame.Light); // Strongest footprint, not double-counted sum.
        Physics2D.Hits = []; frame = f.Adapter.Read(); Equal(0f, frame.LocalLight); True(!frame.LocalLightLimited);
        RenderSettings.ambientLight = default;
    }

    private sealed class Fixture
    {
        public readonly GameObject Root = new("Human");
        public readonly PersonBehaviour Person;
        public readonly LimbBehaviour Limb;
        public readonly PeoplePlaygroundPersonAdapter Adapter;
        public Fixture()
        {
            Person = Root.AddComponent<PersonBehaviour>(); Limb = AddLimb(Root, "LowerArmFront"); Limb.Person = Person; Person.Limbs = [Limb];
            Adapter = new PeoplePlaygroundPersonAdapter(Root, 8, value => Adapter.RegisterCollision(value), value => Adapter.RegisterProjectile(value));
        }
        public static LimbBehaviour AddLimb(GameObject parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            var limb = go.AddComponent<LimbBehaviour>(); limb.Person = parent.transform.GetComponentInParent<PersonBehaviour>(); limb.CirculationBehaviour = go.AddComponent<CirculationBehaviour>(); limb.CirculationBehaviour.OriginalBloodAmount = 1f;
            limb.PhysicalBehaviour = go.AddComponent<PhysicalBehaviour>(); limb.GripBehaviour = new(); return limb;
        }
    }
}
