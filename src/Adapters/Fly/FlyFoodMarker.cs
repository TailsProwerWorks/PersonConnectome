using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>Marks the dedicated fly-food variation without guessing from display names.</summary>
    internal sealed class FlyFoodMarker : MonoBehaviour
    {
        public static bool IsDedicatedFlyFood => true;
    }
}
