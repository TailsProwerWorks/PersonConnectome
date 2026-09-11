using PersonConnectome;

var tests = new (string Name, Action Run)[]
{
    ("determinism", () => { var g = DemoCircuit.Create(); var a = new LifSimulator(g); var b = new LifSimulator(g); for (var i = 0; i < 12; i++) { var x = a.Step(new Dictionary<int,float>{{10, 1.3f}}); var y = b.Step(new Dictionary<int,float>{{10, 1.3f}}); Equal(string.Join(',',x), string.Join(',',y)); } }),
    ("refractory", () => { var s = new LifSimulator(new SparseGraph(new[]{new Neuron(1,1,2)}, Array.Empty<Synapse>())); True(s.Step(new Dictionary<int,float>{{1,2}}).Count == 1); True(s.Step(new Dictionary<int,float>{{1,2}}).Count == 0); True(s.Step(new Dictionary<int,float>{{1,2}}).Count == 0); }),
    ("compact graph", () => { var g = new SparseGraph(new[]{new Neuron(999),new Neuron(-7)}, Array.Empty<Synapse>()); True(g.TryCompactId(-7,out var x) && x == 0); True(g.TryCompactId(999,out var y) && y == 1); }),
    ("state validation", () => { var s = new LifSimulator(DemoCircuit.Create()); True(LocalStateStore.TrySave(new PersistedState(2,s.ExportState(),true),out var json)); True(LocalStateStore.TryLoad(json,5,out _)); True(!LocalStateStore.TryLoad("{",5,out _)); True(!s.TryImportState(new SimulatorState(2,0,new float[1],new int[1],Array.Empty<PendingEvent>()))); }),
    ("pending state persistence", () => { var graph = new SparseGraph(new[]{new Neuron(1),new Neuron(2)},new[]{new Synapse(1,2,2,2)}); var original = new LifSimulator(graph); original.Step(new Dictionary<int,float>{{1,2}}); var restored = new LifSimulator(graph); True(restored.TryImportState(original.ExportState())); Equal(string.Join(',', original.Step()), string.Join(',', restored.Step())); Equal(string.Join(',', original.Step()), string.Join(',', restored.Step())); }),
    ("effect aliases and unknown telemetry", () => { var r = new EffectAliasRegistry(); foreach(var x in new[]{"water","blood","acid","poison","anesthetic","syringe","zombie","regeneration","adrenaline","immortality"}) True(r.Resolve(x) is not null); True(r.Resolve("future-effect") is null && r.UnknownEffects.Contains("future-effect")); }),
    ("motor safety", () => { var gate = new SafeMotorGate { ObserveOnly = true }; var x = gate.Filter(new MotorCommand{Locomotion=99}); Equal(0f,x.Locomotion); gate.ObserveOnly=false; x=gate.Filter(new MotorCommand{Locomotion=99},1); Equal(1f,x.Locomotion); gate.EmergencyDisable(); Equal(0f,gate.Filter(new MotorCommand{Locomotion=-1}).Locomotion); }),
    ("controller applies active default", () => { var game = new CountingGame(); new PersonConnectomeController().Update(game, 1); True(game.ApplyCount > 0); }),
    ("controller emergency disable is latched", () => { var game = new CountingGame(); var controller = new PersonConnectomeController(); controller.Safety.EmergencyDisable(); controller.Update(game, 1); Equal(0, game.ApplyCount); controller.Safety.ResetEmergency(); controller.Update(game, 1); True(game.ApplyCount > 0); }),
    ("scheduler bound", () => { var q=new FixedRateScheduler(10); Equal(0,q.Advance(.09)); Equal(1,q.Advance(.02)); Equal(4,q.Advance(99)); }),
    ("adapter null-safe categories", () => { var adapter=new ReflectionPersonCapabilities(new Stub()); var frame=adapter.Read(); True(frame.Alive && frame.Damage>0 && frame.Fire>0 && frame.Wetness>0 && frame.Contact>0 && frame.Sedation>0 && frame.Infection>0); True(adapter.UnknownEffectNames.Contains("future-effect")); }),
    ("game bridge contract", () => { var script = File.ReadAllText(FindModScript()); foreach (var member in new[]{"DesiredWalkingDirection", "InfluenceMotorSpeed", "GripBehaviour", "BloodRegenerationPerSecond", "BurnIntensity", "EmergencyDisableKey", "Physics2D.OverlapCircleAll"}) True(script.Contains(member, StringComparison.Ordinal)); True(script.Contains("ActiveControl = true", StringComparison.Ordinal)); })
};
var failures=0; foreach(var test in tests) try { test.Run(); Console.WriteLine("PASS "+test.Name); } catch(Exception e) { failures++; Console.Error.WriteLine("FAIL "+test.Name+": "+e.Message); } return failures;
static void True(bool value) { if(!value) throw new Exception("assertion failed"); }
static void Equal<T>(T a,T b) where T:notnull { if(!EqualityComparer<T>.Default.Equals(a,b)) throw new Exception($"expected {a}, got {b}"); }
static string FindModScript()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "Mod", "script.cs");
        if (File.Exists(candidate)) return candidate;
    }
    throw new FileNotFoundException("Mod/script.cs was not found from the test working directory.");
}
sealed class Stub { public bool Alive=true; public float Damage=.4f; public float OnFire=1; public float Wetness=.5f; public float Contact=.3f; public bool Sedated=true; public bool Zombie=true; public string[] Effects={"water","future-effect"}; }
sealed class CountingGame : IGameCapabilities
{
    public int ApplyCount;
    public bool CanApplySupported => true;
    public SensoryFrame Read() => new() { Exists = true, Alive = true };
    public void ApplySupported(MotorCommand command) => ApplyCount++;
}
