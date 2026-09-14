namespace Mod.Core
{
    /// <summary>Unity-free snapshot of body and environment observations consumed by the LIF runtime.</summary>
    internal struct SensoryFrame
    {
        public bool Alive, BrainDead, HealthValid, OxygenValid, ConsciousnessValid, CirculationValid, DamageValid, VitalityValid, BloodValid;
        public float Pain, Damage, Bleeding, Health, Fire, Heat, Cold, Shock, Oxygen, SubmergedHypoxia;
        public float Wetness, UnderWater, Blood, Infection, Charge, AcidExposure;
        public float NativeBloodMinimum, NativeBloodMaximum;
        public int BloodSampleCount, BloodExpectedSamples;
        public float LiquidExposure, LiquidHazard, LiquidSedation, LiquidStimulation, LiquidHealing, LiquidWater;
        public float Nearby, NearbyDirection, Vision, Light, Touch, Impact, Vibration, Sound, AmbientHeat, AmbientCold, LimbLoss, Breakage, JointStress, NeuralJointLoad;
        public float AmbientLight, LocalLight;
        public int LocalLightSources;
        public bool LocalLightLimited;
        public bool FoodCuesValid;
        public float FoodNearbyCue, FoodContactCue;
        public float Consciousness, Adrenaline, Velocity, Rotation, Proprioception, Fall, Projectile, Unconscious;
        public float Balance, Heartbeat, BrainDamage, Seizure, Frozen, Paralysis, Numbness, Vitality, LungDamage;
        public float SoundDirection, VisionDirection, VisualApproach, SignedTilt, JointPosition, JointMotion, VelocityX, VelocityY;
        public bool SoundDirectionValid, VisionDirectionValid, TiltValid, JointSensingValid, VelocityValid, LightValid;
        public float GazeHeadingDegrees, VisionHeadBearingDegrees;
        public bool GazeValid, VisionHeadBearingValid, VisionLimited;
        public bool VisualFieldValid;
        public VisualObservation ViewClockwiseOuter, ViewClockwiseInner, ViewFront, ViewCounterclockwiseInner, ViewCounterclockwiseOuter;

        public readonly VisualObservation ViewAt(int index)
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
}
