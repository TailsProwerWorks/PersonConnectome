namespace Mod.Core
{
    /// <summary>Read-only boundary from a controlled body into the Unity-free runtime.</summary>
    internal interface IBodySensor
    {
        bool IsUsable { get; }
        bool HasSample { get; }
        bool IsTerminal { get; }
        bool IsBrainDead { get; }
        SensoryFrame Read();
    }
}
