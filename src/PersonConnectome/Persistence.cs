using System.Text;
using System.Text.Json;

namespace PersonConnectome;

public sealed record PersistedState(int Version, SimulatorState Simulator);
public static class LocalStateStore
{
    public const int CurrentVersion = 2;
    public const int MaximumBytes = 65_536;
    public static bool TrySave(PersistedState state, out string json)
    {
        json = string.Empty;
        if (state is null)
        {
            return false;
        }

        try
        {
            json = JsonSerializer.Serialize(state);
            return Encoding.UTF8.GetByteCount(json) <= MaximumBytes;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryLoad(string? json, int expectedNeurons, out PersistedState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumBytes)
        {
            return false;
        }

        try
        {
            var candidate = JsonSerializer.Deserialize<PersistedState>(json);
            if (!IsValid(candidate, expectedNeurons))
            {
                return false;
            }

            state = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValid(PersistedState? state, int expectedNeurons)
    {
        if (state is null || state.Version != CurrentVersion || state.Simulator is null)
        {
            return false;
        }

        var simulator = state.Simulator;
        return simulator.Version == CurrentVersion
            && simulator.Potential is { Length: var potentialLength } && potentialLength == expectedNeurons
            && simulator.Refractory is { Length: var refractoryLength } && refractoryLength == expectedNeurons
            && simulator.Pending is { Length: <= 4096 };
    }
}
