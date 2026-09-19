namespace ShadowNineX.PersonConnectome.Core
{
    /// <summary>
    /// Unity-free motor decoder output from the fly connectome.
    ///
    /// Every field is a fly-native request. Body-specific adapters may project
    /// these requests onto their own controls, but that projection is not part
    /// of the neural runtime contract.
    /// </summary>
    internal struct FlyMotorCommand
    {
        public float FlyForward, FlyYaw, FlyBackward, FlyHalt, FlyBrake, FlyEscape;
        public float FlyJump, FlyTakeoff, FlyLanding, FlyFlightPower, FlyFlightYaw, FlyWingMotor;
        public float FlyGroomAntenna, FlyGroomHead, FlyGroomLeg, FlyGroomAbdomen;
        public float FlyFeed, FlyCourtship, FlySong, FlySongPulse, FlyLegMotor, FlyLegMotorAsym;
    }

    /// <summary>Optional Unity-side body rig that can receive fly-native commands.</summary>
    internal interface IFlyBodyRig
    {
        bool IsUsable { get; }
        void Apply(FlyMotorCommand command, float elapsedSeconds);
    }

    /// <summary>A joint-driven body: walking belongs to its legs, flight to the adapter.</summary>
    internal interface IFlyPhysicalBodyRig : IFlyBodyRig
    {
        bool IsGrounded { get; }
        float FlightCapacity { get; }
        bool TryTurnAround();
        void ApplyFlightVelocityChange(float deltaX, float deltaY);
        void Release();
    }
}
