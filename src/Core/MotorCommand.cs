namespace Mod.Core
{
    /// <summary>Unity-free motor request emitted by the LIF runtime for a body adapter.</summary>
    internal struct MotorCommand
    {
        public float Walk, LeftArm, RightArm, LeftLeg, RightLeg, Core, Head;
        public float EscapeLocomotionSeconds;
        public float ReachGrab, LeftGrip, RightGrip, Avoid, BodyThreat, InjuryEvent, VisualThreat, DNp01Activity, NeuralEscape, Freeze, Heal, Stimulate, Calm, Extinguish;

        // Fly-native decoder channels. These are kept separate from the
        // human adaptation fields above so diagnostics can report what the
        // connectome requests without pretending a fly has human joints.
        public float FlyForward, FlyYaw, FlyBackward, FlyHalt, FlyBrake;
        public float FlyJump, FlyTakeoff, FlyLanding, FlyFlightPower, FlyFlightYaw, FlyWingMotor;
        public float FlyGroomAntenna, FlyGroomHead, FlyGroomLeg, FlyGroomAbdomen;
        public float FlyFeed, FlyCourtship, FlySong, FlySongPulse, FlyLegMotor, FlyLegMotorAsym;
    }
}
