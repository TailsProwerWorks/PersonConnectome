using Mod;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        (string, Action)[] tests =
        [
            ("terminal motors and grips clear immediately", TerminalStop),
            ("unconscious and incapable limbs clear old commands", IncapableStop),
            ("brain injury remains alive with matching signal value", BrainInjury),
            ("invalid health stops control without inventing death", InvalidHealth),
            ("destroyed and newly added inactive limbs", LimbLifecycle),
            ("normal blood, gorse blood and knockout identities", Liquids),
            ("hypoxia and submersion remain distinct", Oxygen),
            ("external audio excludes own limbs, mute and invalid distances", Audio),
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
        True(f.Adapter.Read().Alive); f.Adapter.Apply(Moving, false); True(extra.MotorSpeed < 0); True(f.Adapter.LiveLimbSummary.Contains("LIMBS: 1"));
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
    }
    private static void Oxygen()
    {
        var f = new Fixture(); f.Person.OxygenLevel = .2f;
        Equal(0, f.Adapter.Read().SubmergedHypoxia); Equal("LOW OXYGEN", f.Adapter.LiveSignal);
        f.Limb.PhysicalBehaviour.IsUnderWater = true; Equal(.8f, f.Adapter.Read().SubmergedHypoxia); Equal("SUBMERGED HYPOXIA", f.Adapter.LiveSignal);
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
        True(f.Adapter.LiveAudioSummary.Contains("external Radio"));
        audio.mute = true; Equal(0, f.Adapter.Read().Sound); audio.mute = false; audio.isActiveAndEnabled = false; Equal(0, f.Adapter.Read().Sound);
        audio.isActiveAndEnabled = true; other.Surface = new Vector2(float.NaN, 0); frame = f.Adapter.Read(); Equal(0, frame.Sound); Equal(0, frame.Nearby);
        var own = f.Limb.gameObject.AddComponent<Collider2D>(); f.Limb.PhysicalBehaviour.MainAudioSource = audio;
        Physics2D.Hits = [own]; Equal(0, f.Adapter.Read().Sound); Equal(0, f.Adapter.Read().Nearby);
        f.Limb.transform.SetParent(null); Equal(0, f.Adapter.Read().Sound); Equal(0, f.Adapter.Read().Nearby);
        Physics2D.Hits = []; Equal(0, f.Adapter.Read().Nearby);
    }
    private static void Impacts()
    {
        var f = new Fixture(); var probe = f.Limb.GetComponent<PersonConnectomeLimbProbe>();
        var callback = typeof(PersonConnectomeLimbProbe).GetMethod("OnCollisionEnter2D", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var own = f.Limb.gameObject.AddComponent<Collider2D>(); callback.Invoke(probe, [new Collision2D { collider = own, relativeVelocity = new Vector2(10, 0) }]); Equal(0, f.Adapter.Read().Impact);
        var floor = new GameObject("Floor").AddComponent<Collider2D>(); callback.Invoke(probe, [new Collision2D { collider = floor, relativeVelocity = new Vector2(10, 0) }]);
        var frame = f.Adapter.Read(); Equal(.5f, frame.Impact); Equal(0, frame.Sound); Equal("CONTACT IMPACT", f.Adapter.LiveSignal);
        for (var i = 0; i < 30; i++) frame = f.Adapter.Read(); True(frame.Impact < .001f);
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
            Person = Root.AddComponent<PersonBehaviour>(); Limb = AddLimb(Root, "LowerArmFront"); Person.Limbs = [Limb];
            Adapter = new PeoplePlaygroundPersonAdapter(Root, 8, value => Adapter.RegisterCollision(value));
        }
        public static LimbBehaviour AddLimb(GameObject parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform);
            var limb = go.AddComponent<LimbBehaviour>(); limb.CirculationBehaviour = go.AddComponent<CirculationBehaviour>();
            limb.PhysicalBehaviour = go.AddComponent<PhysicalBehaviour>(); limb.GripBehaviour = new(); return limb;
        }
    }
}
