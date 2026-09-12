using Mod;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        (string, Action)[] tests =
        [
            ("terminal motors and grips clear immediately", TerminalStop),
            ("native pose context actions are suppressed", ContextMenuPoseActions),
            ("unconscious and locally damaged limbs clear old commands", IncapableStop),
            ("brain injury remains alive with matching signal value", BrainInjury),
            ("invalid health stops control without inventing death", InvalidHealth),
            ("destroyed and newly added inactive limbs", LimbLifecycle),
            ("all 41 stock liquid IDs have explicit exposure routes", Liquids),
            ("unknown and invalid liquids cannot fabricate effects", UnknownAndInvalidLiquids),
            ("liquid snapshots and native zombie state stay separate", LiquidSnapshots),
            ("acid pools produce an explicit acid signal", AcidPools),
            ("paralysis, breakage and limb loss stay distinct", LimbDamageCategories),
            ("blood baseline and vitality fallback", BloodAndVitality),
            ("invalid limb health and blood remain unknown", InvalidLimbSamples),
            ("hypoxia and submersion remain distinct", Oxygen),
            ("invalid oxygen remains unknown without hypoxia", InvalidOxygen),
            ("invalid consciousness suspends output without an unconscious claim", InvalidConsciousness),
            ("circulation reports unknown when native flow is unavailable", InvalidCirculation),
        ("nearby temperatures provide bounded ambient heat and cold", AmbientTemperature),
        ("nearby lava provides heat and hazard", NearbyLava),
        ("external audio excludes own limbs, mute and invalid distances", Audio),
        ("last-heard audio persists without stimulating stale sound", AudioHistory),
            ("visible external objects produce a vision proxy", Vision),
            ("vibration, proprioception and projectile channels stay distinct", AdditionalSenses),
            ("falling is distinct from walking and floor contact", Falling),
            ("contact impacts do not fabricate hearing", Impacts),
            ("detached owned limbs remain excluded from collision signals", DetachedOwnCollisions),
            ("control clock preserves rate and caps catch-up", ControlClock),
            ("regeneration ownership and cleanup", Chemistry),
            ("front-back hierarchy routes separate channels", SideRouting),
            ("missing grip and joint do not interrupt other limbs", OptionalControls),
            ("broken limbs do not suppress healthy limb control", LocalLimbDamage),
            ("dead limb health stops only its own actuator", LocalDeadLimb),
            ("finite boundary sanitization", FiniteInputs),
            ("telemetry follows native readings independently of requests", NativeTelemetryTrace),
            ("native motor requests use bounded walking and angular units", NativeMotorUnits),
            ("component disable clears commands and chemistry", Disable),
            ("never-activated cleanup preserves game state", NeverActivated),
            ("neutral chemistry preserves adrenaline", NeutralChemistry),
            ("active chemistry respects native adrenaline range", NativeAdrenalineRange)
        ];
        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { Physics2D.Hits = []; test(); Console.WriteLine("PASS " + name); }
            catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
        }
        return failures;
    }
    private static void True(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static void Equal(float expected, float actual) { if (float.IsNaN(actual) || MathF.Abs(expected - actual) > .00001f) throw new Exception($"expected {expected}, actual {actual}"); }
    private static void Equal(string expected, string actual) { if (expected != actual) throw new Exception($"expected {expected}, actual {actual}"); }
    private static MotorCommand Moving => new() { Walk = 1, RightArm = 1, LeftArm = -1, RightLeg = 1, LeftLeg = -1, Head = .5f, Core = .5f, RightGrip = 1, LeftGrip = 1, ReachGrab = 1, Heal = .8f, Avoid = 0, Freeze = 0, Stimulate = 0, Calm = 0, Extinguish = 0 };
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
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(1, options.Buttons.Count); True(options.Buttons.Contains(delete));
        type.GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(controller, null);
        Equal(3, options.Buttons.Count); True(options.Buttons.Contains(walking)); True(options.Buttons.Contains(sitting));
    }
    private static void BloodAndVitality()
    {
        var f = new Fixture();
        Equal(0, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.BloodAmount = .5f;
        Equal(.5f, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.BloodAmount = 0;
        Equal(1, f.Adapter.Read().Blood);
        f.Limb.CirculationBehaviour.BloodAmount = 1;
        f.Limb.Vitality = 0;
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
        f.Limb.CirculationBehaviour.BloodAmount = float.NaN;
        var frame = f.Adapter.Read(); True(!frame.BloodValid); Equal(0f, frame.Blood); True(f.Adapter.LiveInjurySummary.Contains("blood-loss=unknown"));

        f = new Fixture(); f.Limb.Health = float.NaN;
        frame = f.Adapter.Read(); True(!frame.DamageValid); Equal(0f, frame.Damage); True(!frame.VitalityValid); Equal(0f, frame.Vitality);
        True(f.Adapter.LiveBodySummary.Contains("damage=unknown")); True(f.Adapter.LiveInjurySummary.Contains("vitality=unknown"));
        f.Adapter.Apply(Moving, false); Equal(0f, f.Limb.MotorSpeed); True(f.Adapter.LiveLimbSummary.Contains("LowerArmFront:invalid-health"));

        var healthy = Fixture.AddLimb(f.Root, "LowerArmBack"); f.Person.Limbs = [f.Limb, healthy]; healthy.IsDismembered = true;
        frame = f.Adapter.Read(); Equal(.5f, frame.LimbLoss); True(frame.DamageValid); True(frame.VitalityValid);
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
        var collider = go.AddComponent<Collider2D>(); collider.Surface = new Vector2(1, 0); return collider;
    }
    private static void Audio()
    {
        var f = new Fixture(); var other = SoundObject(out var audio); Physics2D.Hits = [other];
        var frame = f.Adapter.Read(); True(frame.Sound > 0 && frame.Nearby >= 0 && frame.Nearby <= 1); Equal("OBJECT AUDIO", f.Adapter.LiveSignal);
        True(f.Adapter.LiveAudioSummary.Contains("external object Radio"));
        audio.mute = true; Equal(0, f.Adapter.Read().Sound); Equal("SENSING", f.Adapter.LiveState); audio.mute = false; audio.isActiveAndEnabled = false; Equal(0, f.Adapter.Read().Sound);
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

    private static void ControlClock()
    {
        var f = new Fixture();
        var controller = f.Root.AddComponent<PersonConnectomeController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(PersonConnectomeController);
        type.GetMethod("Awake", flags).Invoke(controller, null);
        var brain = (ConnectomeBrain)type.GetField("brain", flags).GetValue(controller);
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

    private static void Vision()
    {
        var f = new Fixture();
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
        Physics2D.LinecastResult = default;
        Equal(0, f.Adapter.Read().Vision);
        RenderSettings.ambientLight = default;
    }
    private static void AdditionalSenses()
    {
        var f = new Fixture();
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
        Equal(-.46f, f.Person.DesiredWalkingDirection); Equal(.11f, f.Limb.MotorSpeed);
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            f.Limb.MotorSpeed = 10f;
            f.Adapter.Apply(new MotorCommand { Walk = 1f, RightArm = 1f }, false, invalid, invalid);
            Equal(0f, f.Person.DesiredWalkingDirection); Equal(0f, f.Limb.MotorSpeed);
            True(f.Adapter.LiveLimbSummary.Contains("invalid (stopped)"));
        }
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
    private sealed class Fixture
    {
        public readonly GameObject Root = new("Human");
        public readonly PersonBehaviour Person;
        public readonly LimbBehaviour Limb;
        public readonly PeoplePlaygroundPersonAdapter Adapter;
        public Fixture()
        {
            Person = Root.AddComponent<PersonBehaviour>(); Limb = AddLimb(Root, "LowerArmFront"); Limb.Person = Person; Person.Limbs = [Limb];
            Adapter = new PeoplePlaygroundPersonAdapter(Root, 8, value => Adapter.RegisterCollision(value));
        }
        public static LimbBehaviour AddLimb(GameObject parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            var limb = go.AddComponent<LimbBehaviour>(); limb.Person = parent.transform.GetComponentInParent<PersonBehaviour>(); limb.CirculationBehaviour = go.AddComponent<CirculationBehaviour>();
            limb.PhysicalBehaviour = go.AddComponent<PhysicalBehaviour>(); limb.GripBehaviour = new(); return limb;
        }
    }
}
