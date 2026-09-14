

namespace Mod
{
#pragma warning disable CS0649
    internal struct SensoryFrame
    {
        public bool Alive, BrainDead, HealthValid, OxygenValid, ConsciousnessValid, CirculationValid, DamageValid, VitalityValid, BloodValid;
        public float Pain, Damage, Bleeding, Health, Fire, Heat, Cold, Shock, Oxygen, SubmergedHypoxia;
        // Infection is the native IsZombie flag aggregated across tracked limbs, not a general infection assay.
        public float Wetness, UnderWater, Blood, Infection, Charge, AcidExposure;
        public float NativeBloodMinimum, NativeBloodMaximum;
        public int BloodSampleCount, BloodExpectedSamples;
        // Liquid channels are exposure fractions. LiquidWater is reserved: no plain water is registered in stock 1.27.17.
        public float LiquidExposure, LiquidHazard, LiquidSedation, LiquidStimulation, LiquidHealing, LiquidWater;
        public float Nearby, NearbyDirection, Vision, Light, Touch, Impact, Vibration, Sound, AmbientHeat, AmbientCold, LimbLoss, Breakage, JointStress, NeuralJointLoad;
        // Light combines native ambient brightness with a bounded local sprite-footprint proxy.
        public float AmbientLight, LocalLight;
        public int LocalLightSources;
        public bool LocalLightLimited;
        // Explicit catalog-based gameplay cues, not measured odor or taste.
        public bool FoodCuesValid;
        public float FoodNearbyCue, FoodContactCue;
        public float Consciousness, Adrenaline, Velocity, Rotation, Proprioception, Fall, Projectile, Unconscious;
        // Legacy Vitality/VitalityValid store derived minimum connected-limb health, not native Vitality.
        public float Balance, Heartbeat, BrainDamage, Seizure, Frozen, Paralysis, Numbness, Vitality, LungDamage;
        // These retained direction diagnostics use world X; visual neural input
        // uses the separate head bearing below. Neither is anatomical localization.
        public float SoundDirection, VisionDirection, VisualApproach, SignedTilt, JointPosition, JointMotion, VelocityX, VelocityY;
        public bool SoundDirectionValid, VisionDirectionValid, TiltValid, JointSensingValid, VelocityValid, LightValid;
        // Local +X of the connected brain limb defines facing for stock Humans.
        // Positive bearing is counterclockwise in the 2D world, an engineered
        // projection onto fly R populations, not a reconstructed fly retina.
        public float GazeHeadingDegrees, VisionHeadBearingDegrees;
        public bool GazeValid, VisionHeadBearingValid, VisionLimited;
        // Value snapshots of five 36-degree bands; no mutable frame arrays.
        public bool VisualFieldValid;
        public VisualObservation ViewClockwiseOuter, ViewClockwiseInner, ViewFront, ViewCounterclockwiseInner, ViewCounterclockwiseOuter;
        public VisualObservation ViewAt(int index)
        {
            switch (index)
            {
                case 0: return ViewClockwiseOuter;
                case 1: return ViewClockwiseInner;
                case 2: return ViewFront;
                case 3: return ViewCounterclockwiseInner;
                case 4: return ViewCounterclockwiseOuter;
                default: return default;
            }
        }
        public void SetView(int index, VisualObservation observation)
        {
            switch (index)
            {
                case 0: ViewClockwiseOuter = observation; break;
                case 1: ViewClockwiseInner = observation; break;
                case 2: ViewFront = observation; break;
                case 3: ViewCounterclockwiseInner = observation; break;
                case 4: ViewCounterclockwiseOuter = observation; break;
            }
        }
        // Reference-game adaptations: sampled injury event, regional contact and
        // geometric visual features. These are proxies, not biological assays.
        public float DamageEvent, TouchHead, TouchArms, TouchLegs, TouchCore;
        public float VisualAngularSize, VisualExpansion, VisualAngularSpeed, AngularVelocity;
        public bool RegionalTouchValid, VisualGeometryValid, AngularVelocityValid;
        public float SoundLow, SoundHigh;
        public bool SoundSpectrumValid;
        public bool SoundLimited;
        public float InternalBleeding, Circulation, Disconnected, Wounds;
        public float Lava, BurnProgress, Stabbed, PhysicalContact, Weightless, Sliding;
    }

    internal struct VisualObservation
    {
        public bool Observed, GeometryValid;
        public float Strength, BearingDegrees, Approach, AngularSize, Expansion, AngularSpeed;
    }

    internal struct MotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float EscapeLocomotionSeconds;
        // Keep the source signals visible instead of hiding them inside Avoid:
        // body hazard is native state, visual threat is the looming encoder,
        // DNp01Activity reports actual firing; NeuralEscape is the context-qualified request.
        public float ReachGrab, LeftGrip, RightGrip, Avoid, BodyThreat, InjuryEvent, VisualThreat, DNp01Activity, NeuralEscape, Freeze, Heal, Stimulate, Calm, Extinguish;
    }
#pragma warning restore CS0649
}
