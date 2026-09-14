namespace Mod.Core
{
    /// <summary>Unity-free motor request emitted by the LIF runtime for a body adapter.</summary>
    internal struct MotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float EscapeLocomotionSeconds;
        public float ReachGrab, LeftGrip, RightGrip, Avoid, BodyThreat, InjuryEvent, VisualThreat, DNp01Activity, NeuralEscape, Freeze, Heal, Stimulate, Calm, Extinguish;
    }
}
