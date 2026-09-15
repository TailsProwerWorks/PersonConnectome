using Mod.Core;

namespace Mod.Adapters
{
    /// <summary>
    /// Reserved adapter slot for a native People Playground fly body.
    ///
    /// This intentionally remains disabled until the installed game's fly API,
    /// physiology, and calibration are verified. Keeping the no-op implementation
    /// here lets composition and tests exercise adapter selection without creating
    /// a second connectome engine or inventing unsupported mappings.
    /// </summary>
    internal sealed class PeoplePlaygroundFlyAdapter : IBodyAdapter
    {
        public bool IsUsable => false;
        public bool HasSample => false;
        public bool IsTerminal => false;
        public bool IsBrainDead => false;

        public SensoryFrame Read() => default;

        public void Apply(FlyMotorCommand command, bool chemistry, float jointSpeedDegreesPerSecond, float walkingRequestGain, float elapsedSeconds)
        {
            // No fly API is registered yet; unsupported actuation is deliberately ignored.
        }

        public void RefreshWalkingRequest() { }
        public void Suspend() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
