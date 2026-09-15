using System;

namespace Mod.Core
{
    /// <summary>Actuation boundary from the runtime into a controlled body.</summary>
    internal interface IBodyActuator
    {
        void Apply(FlyMotorCommand command, bool chemistry, float jointSpeedDegreesPerSecond, float walkingRequestGain, float elapsedSeconds);
        void RefreshWalkingRequest();
        void Suspend();
        void Stop();
    }

    /// <summary>Combined sensor/actuator contract used by composition code.</summary>
    internal interface IBodyAdapter : IBodySensor, IBodyActuator, IDisposable
    {
    }
}
