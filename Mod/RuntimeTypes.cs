

namespace Mod
{
#pragma warning disable CS0649
    internal struct SensoryFrame
    {
        public bool Alive, BrainDead, HealthValid, OxygenValid, ConsciousnessValid, CirculationValid, DamageValid, VitalityValid, BloodValid;
        public float Pain, Damage, Bleeding, Health, Fire, Heat, Cold, Shock, Oxygen, SubmergedHypoxia;
        // Infection is the native IsZombie flag aggregated across tracked limbs, not a general infection assay.
        public float Wetness, UnderWater, Blood, Infection, Charge, AcidExposure;
        // Liquid channels are exposure fractions. LiquidWater is reserved: no plain water is registered in stock 1.27.17.
        public float LiquidExposure, LiquidHazard, LiquidSedation, LiquidStimulation, LiquidHealing, LiquidWater;
        public float Nearby, NearbyDirection, Vision, Light, Touch, Impact, Vibration, Sound, AmbientHeat, AmbientCold, LimbLoss, Breakage, JointStress, NeuralJointLoad;
        public float Consciousness, Adrenaline, Velocity, Rotation, Proprioception, Fall, Projectile, Unconscious;
        public float Balance, Heartbeat, BrainDamage, Seizure, Frozen, Paralysis, Numbness, Vitality, LungDamage;
        // Geometry is a 2D world-axis projection, not biological ear/eye localization.
        public float SoundDirection, VisionDirection, VisualApproach, SignedTilt, JointPosition, JointMotion, VelocityX, VelocityY;
        public bool SoundDirectionValid, VisionDirectionValid, TiltValid, JointSensingValid, VelocityValid, LightValid;
        // Reference-game adaptations: sampled injury event, regional contact and
        // geometric visual features. These are proxies, not biological assays.
        public float DamageEvent, TouchHead, TouchArms, TouchLegs, TouchCore;
        public float VisualAngularSize, VisualExpansion, VisualAngularSpeed, AngularVelocity;
        public bool RegionalTouchValid, VisualGeometryValid, AngularVelocityValid;
        public float SoundLow, SoundHigh;
        public bool SoundSpectrumValid;
        public float InternalBleeding, Circulation, Disconnected, Wounds;
        public float Lava, BurnProgress, Stabbed, PhysicalContact, Weightless, Sliding;
    }

    internal struct MotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float ReachGrab, LeftGrip, RightGrip, Avoid, Freeze, Heal, Stimulate, Calm, Extinguish;
    }
#pragma warning restore CS0649
}
