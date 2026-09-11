

namespace Mod
{
    internal struct SensoryFrame
    {
        public bool Alive, BrainDead, HealthValid;
        public float Pain, Damage, Bleeding, Health, Fire, Heat, Cold, Shock, Oxygen, SubmergedHypoxia;
        public float Wetness, UnderWater, Blood, Infection, Charge, AcidExposure;
        public float LiquidExposure, LiquidHazard, LiquidSedation, LiquidStimulation, LiquidHealing, LiquidWater;
        public float Nearby, NearbyDirection, Light, Touch, Impact, Sound, LimbLoss, Breakage, JointStress;
        public float Consciousness, Adrenaline, Velocity, Rotation, Unconscious;
        public float Balance, Heartbeat, BrainDamage, Seizure, Frozen, Paralysis, Numbness, Vitality, LungDamage;
        public float InternalBleeding, Circulation, Disconnected, Wounds;
        public float Lava, BurnProgress, Stabbed, PhysicalContact, Weightless, Sliding;
    }

    internal struct MotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float ReachGrab, LeftGrip, RightGrip, Avoid, Freeze, Heal, Stimulate, Calm, Extinguish;
    }
}
