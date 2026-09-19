# Fly body design

The fly is a native articulated PPG creature with **17 separate rigid bodies**:
head, thorax, abdomen, two wings, and six legs with a femur and tibia each.
Every part retains a stock Human limb's `LimbBehaviour`, `PhysicalBehaviour`,
`CirculationBehaviour`, `SkinMaterialHandler`, and connectivity node. It has its
own collider and can be damaged, burned, broken, detached, or disintegrated.
There is no enclosing whole-fly collider and no line-renderer anatomy.

Wing rigidbodies use zero gravity scale while idle so a released hinge does not
fall into an arbitrary vertical pose between strokes. Their native hinge,
collider, health and break behavior remain active; the flight actuator supplies
the visible movement.

`FlyBodyBuilder` registers a complete prefab beneath an inactive persistent holder before
native `Awake`/`Start`. Catalog spawning and save/undo reconstruction clone this same complete hierarchy. `FlyBodyBootstrap` restores runtime bindings from serialized part references. Native Person initialization then discovers the final
limbs and brain, creates pose lookups and collision exclusions, and retains its
physiology. The thorax is the circulation pump and connectivity root; each
appendage has a native, breakable hinge. Human pose muscle/standing assistance
is suppressed for every pose state and `BaseStrength` is zeroed because native
limbs outside a pose ignore its force multiplier. Self-collision is disabled
in bootstrap `Awake`, before native `Start`; the legs still
collide with the world and carry the animal's weight.

Startup hinges retain infinite break limits until the native limb update sets
its physiological break limits. A later fly relay caps the actual hinge break
force from part mass and remaining health, so miniature joints do not require
Human-scale impact forces to sever. It deliberately leaves `BreakingThreshold`
unchanged because PPG also uses that field for accumulated bone stress; scaling
it to fly mass caused healthy legs to break and lose motor authority at spawn.
Native temperature, rot, joint-break, bleeding and loose-tissue behavior still
own the live break response.
All path-dependent brain and appearance assets are loaded during mod `Main`;
prefab `Awake` and limb `Start` consume cached references. PPG rejects asset
loads from those Unity callbacks.

## IK and walking

`Core/FlyGait.cs` solves two-link inverse kinematics for each foot target. The
near front/hind plus far middle legs alternate with the opposite tripod. Stance
feet sweep backward, swing feet lift and return forward, reverse commands
reverse the sweep, and flight tucks the legs. Front-leg grooming moves the feet
toward the head. Both body sides plant at the same ground height.

`FlyBodyRig` uses load-bearing, health-scaled angular feedback to drive the native
hip and knee hinges toward those angles after the native limb update. Hip targets
are thorax-relative; the solver's absolute lower-segment heading is converted to
a femur-relative knee target before motor control. It does not reposition limbs or set
walking velocity on the thorax. Foot friction and joint forces generate ground
movement; a bounded grounded balance torque helps hold the thorax level. Broken,
severed, frozen, paralyzed, dead, or unconscious limbs lose actuator authority.
Stopping control releases motors and retains passive ragdoll physics.

Joint targets stay in `Rigidbody2D.rotation` space. Catalog facing uses a
negative X scale, which rigidbody angles intentionally exclude; reflecting only
the target previously made correctly posed legs drive through the thorax and
point upward on a mirrored spawn.

The completed silhouette is centered horizontally around the catalog prefab
pivot. PPG can therefore apply its normal Q/E left/right-facing root flip without
moving the asymmetric wings and body to the opposite side of the cursor.

Flight still uses an engineered bounded force/velocity projection. It requires
two usable native wings; wing loss cancels held flight. During flight the two
wing hinges receive a phase-locked, rate-limited stroke with health-scaled
torque; at rest their motors are released instead of holding a noisy position.
The adapter's bounded
thorax velocity change is also applied to every still-attached dynamic body, so
the other gravity-affected parts do not anchor the thorax through their hinges.
Detached parts are excluded. This is not an aerodynamic or reconstructed muscle
simulation. The gait uses a periodic foot path relative to the thorax, not
terrain raycasts or world-locked footholds.

Strong resolved looming danger can qualify the existing armed, refractory
escape reflex even when DNp01 does not fire on the exact same scheduled tick.
Weaker cues still require descending-neuron activity. Approaching native
projectiles are reported as explicit danger after head-field and line-of-sight
checks.

## Appearance and wounds

Each physical part has its own original surface and matching flesh/bone layers,
generated by `scripts/build/Build-FlySprites.ps1`. They are the skin on individual
moving limbs, not a picture of the whole animal. The head has a red compound
eye and antennae, the thorax has bristles, the abdomen tapers with dark bands,
and the wings have pale membranes and veins. Native skin materials remain in
control of wounds, burns and blood. Hit detection and bleeding occur on the
actual part, not a guessed region of a single collider.

Wing hinges retain native break and wound events, but their inherited Human
`GoreStringBehaviour` is disabled. A broken wing therefore separates cleanly
instead of creating a random secondary spring/tissue tether; other body parts
retain the user's normal loose-tissue preference.

`FlyPartAssets` loads all three layers at **200 pixels per world unit** using
`ModAPI.LoadSprite(scale: 200f / 35f)`. The installed PPG API multiplies its
`scale` argument by 35; passing 200 directly previously produced 7000 PPU,
shrinking every surface and collider 35-fold while leaving the joints at their
original spacing. A runtime PPU check rejects stale incorrectly scaled cache
entries. Femur/tibia artwork now spans .24/.29 units, matching the .23/.28-unit
IK links; the wings span .76 units. Restart PPG after installing this correction
because its sprite cache is keyed by path, not scale.

`FlyHealth` reads bound native limbs for regional telemetry and forwards impact
learning without applying a second damage model. The sensory scan excludes the
whole sibling limb assembly and reads head contact/vision from the head.

## Verification boundary

Offline tests cover IK reach, tripod phasing, reverse gait, flight folding,
invalid values, native health/dismemberment telemetry, disabling flight after
wing failure, and absence of body sliding for walking commands. Builds and the
installed compiler/scanner checks validate the exact API surface.

Native spawn initialization, visual quality, stable walking on actual surfaces,
visible bleeding, dismemberment, mirroring, save/load, and delete/undo require the
[manual fly checklist](manual-game-test.md#fly-variation). Passing doubles does
not establish those results.
