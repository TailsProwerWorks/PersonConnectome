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
            ("unconscious and incapable limbs clear old commands", IncapableStop),
            ("brain injury remains alive with matching signal value", BrainInjury),
            ("invalid health stops control without inventing death", InvalidHealth),
            ("destroyed and newly added inactive limbs", LimbLifecycle),
            ("normal blood, gorse blood and knockout identities", Liquids),
            ("acid pools produce an explicit acid signal", AcidPools),
            ("paralysis, breakage and limb loss stay distinct", LimbDamageCategories),
            ("blood baseline and vitality fallback", BloodAndVitality),
            ("hypoxia and submersion remain distinct", Oxygen),
            ("nearby temperatures provide bounded ambient heat and cold", AmbientTemperature),
            ("external audio excludes own limbs, mute and invalid distances", Audio),
            ("visible external objects produce a vision proxy", Vision),
            ("vibration, proprioception and projectile channels stay distinct", AdditionalSenses),
            ("falling is distinct from walking and floor contact", Falling),
            ("contact impacts do not fabricate hearing", Impacts),
            ("regeneration ownership and cleanup", Chemistry),
            ("front-back hierarchy routes separate channels", SideRouting),
            ("missing grip and joint do not interrupt other limbs", OptionalControls),
            ("finite boundary sanitization", FiniteInputs),
            ("component disable clears commands and chemistry", Disable),
            ("never-activated cleanup preserves game state", NeverActivated),
            ("neutral chemistry preserves adrenaline", NeutralChemistry)
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
        f.Person.Consciousness = 1; f.Limb.IsCapable = false; f.Limb.MotorSpeed = 3; f.Adapter.Read(); f.Adapter.Apply(Moving, true); Equal(0, f.Limb.MotorSpeed); Equal(0, f.Limb.RegenerationSpeed);
        True(f.Adapter.LiveLimbSummary.Contains("LowerArmFront:incapable"));
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
        var f = new Fixture(); var c = f.Limb.CirculationBehaviour;
        c.LiquidDistribution[new Liquid("BLOOD")] = new() { Raw = 1 };
        var frame = f.Adapter.Read(); Equal(0, frame.LiquidExposure); Equal(0, frame.LiquidHazard);
        c.LiquidDistribution.Clear(); c.LiquidDistribution[new Liquid("GORSE BLOOD")] = new() { Raw = .4f };
        frame = f.Adapter.Read(); Equal(.4f, frame.LiquidExposure); Equal(.4f, frame.LiquidHazard);
        c.LiquidDistribution.Clear(); c.LiquidDistribution[new Liquid("KNOCKOUT POISON")] = new() { Raw = .7f };
        frame = f.Adapter.Read(); Equal(.7f, frame.LiquidSedation); Equal(0, frame.LiquidHazard); Equal("SEDATION", f.Adapter.LiveSignal);
        c.LiquidDistribution[new Liquid("ACID")] = new() { Raw = .3f }; Equal(.3f, f.Adapter.Read().LiquidHazard);
        c.LiquidDistribution.Clear(); frame = f.Adapter.Read(); Equal(0, frame.LiquidSedation); Equal(0, frame.LiquidHazard);
        foreach (var identity in new[] { "REANIMATION AGENT", "TISSUE DECONSTRUCTION AGENT", "NITRO", "GASOLINE", "COOLANT", "TRITIUM" })
        {
            c.LiquidDistribution[new Liquid(identity)] = new() { Raw = .4f };
            Equal(.4f, f.Adapter.Read().LiquidHazard);
            c.LiquidDistribution.Clear();
        }
        foreach (var identity in new[] { "LIFE SERUM", "MENDING SERUM", "COAGULATION SERUM", "ENHANCING SERUM", "ULTRA STRENGTH SERUM" })
        {
            c.LiquidDistribution[new Liquid(identity)] = new() { Raw = .4f };
            frame = f.Adapter.Read();
            True(frame.LiquidHealing > .0f || frame.LiquidStimulation > .0f);
            c.LiquidDistribution.Clear();
        }
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
    private static void AcidPools()
    {
        var f = new Fixture(); var acidObject = new GameObject("Acid Spider Pool"); var acid = acidObject.AddComponent<AcidPoolBehaviour>(); acid.AcidProgress = .8f; acid.PainIntensity = .2f;
        Physics2D.Hits = [acidObject.AddComponent<Collider2D>()];
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
    private static void AmbientTemperature()
    {
        var f = new Fixture();
        var hot = new GameObject("Hot object"); var hotPhysical = hot.AddComponent<PhysicalBehaviour>(); hotPhysical.Temperature = 100; var hotCollider = hot.AddComponent<Collider2D>(); hotCollider.Surface = new Vector2(2, 0);
        Physics2D.Hits = [hotCollider]; var frame = f.Adapter.Read(); True(frame.AmbientHeat > 0f); Equal(0f, frame.AmbientCold); Equal("AMBIENT HEAT", f.Adapter.LiveSignal);
        hotPhysical.Temperature = 0; frame = f.Adapter.Read(); Equal(0f, frame.AmbientHeat); True(frame.AmbientCold > 0f); Equal("AMBIENT COLD", f.Adapter.LiveSignal);
        hotCollider.Surface = new Vector2(8, 0); frame = f.Adapter.Read(); Equal(0f, frame.AmbientCold); Physics2D.Hits = [];
        var detachedOwn = new GameObject("Detached own limb"); var detachedPhysical = detachedOwn.AddComponent<PhysicalBehaviour>(); detachedPhysical.Temperature = 100; var detachedCollider = detachedOwn.AddComponent<Collider2D>(); detachedCollider.Surface = new Vector2(2, 0);
        f.Limb.PhysicalBehaviour = detachedPhysical; Physics2D.Hits = [detachedCollider]; Equal(0f, f.Adapter.Read().AmbientHeat); Physics2D.Hits = [];
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
        generatedAudio.isPlaying = true; generatedPhysical.MainAudioSource = generatedAudio; var generatedCollider = generatedRoot.AddComponent<Collider2D>(); generatedCollider.Surface = new Vector2(.37f, 0);
        Physics2D.Hits = [generatedCollider]; Equal(0, f.Adapter.Read().Sound); Equal("SENSING", f.Adapter.LiveState);
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
    private static void FiniteInputs()
    {
        var f = new Fixture(); f.Adapter.RegisterCollision(float.NaN); f.Adapter.RegisterCollision(float.PositiveInfinity);
        f.Person.PainLevel = float.NaN; f.Person.AverageSpeed = float.PositiveInfinity;
        var frame = f.Adapter.Read(); Equal(0, frame.Impact); Equal(0, frame.Pain); Equal(0, frame.Velocity);
        f.Adapter.Apply(new MotorCommand { Walk = float.NaN, RightArm = float.NaN, RightGrip = float.NaN }, true); Equal(0, f.Person.DesiredWalkingDirection); Equal(0, f.Limb.MotorSpeed); True(!f.Limb.GripBehaviour.isHolding);
    }
    private static void NeutralChemistry()
    {
        var f = new Fixture(); f.Person.AdrenalineLevel = 1.5f; f.Adapter.Read();
        f.Adapter.Apply(default, true); Equal(1.5f, f.Person.AdrenalineLevel);
        f.Adapter.Apply(new MotorCommand { Stimulate = .5f, Calm = .5f }, true); Equal(1.5f, f.Person.AdrenalineLevel);
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
