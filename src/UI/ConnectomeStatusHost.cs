using System;
using UnityEngine;
using ShadowNineX.PersonConnectome.UI;

namespace ShadowNineX.PersonConnectome
{
    internal sealed class ConnectomeStatusHostBehaviour : MonoBehaviour
    {
        // This marker component identifies the persistent Unity host object.
        // Keeping the name here prevents registration code from duplicating it.
        internal const string HostObjectName = "Person Connectome Status Host";
    }

    /// <summary>
    /// Global owner for the one connectome telemetry window. Registrations are
    /// lightweight per-body state handles rendered through that shared window.
    /// </summary>
    internal static class ConnectomeStatusHost
    {
        private static GameObject? hostObject;
        private static int nextIdentity;

        public static Transform Root => EnsureRoot();

        public static int AllocateIdentity() => ++nextIdentity;

        public static PersonConnectomeStatusDisplay RegisterPerson(Transform anchor, ManualInputState input, StatusDisplayBindings bindings)
        {
            return new PersonConnectomeStatusDisplay(anchor, input, bindings, EnsureRoot(), AllocateIdentity(), StatusDisplayBodyKind.Person);
        }

        public static PersonConnectomeStatusDisplay RegisterFly(Transform anchor, ManualInputState input, StatusDisplayBindings bindings)
        {
            return new PersonConnectomeStatusDisplay(anchor, input, bindings, EnsureRoot(), AllocateIdentity(), StatusDisplayBodyKind.Fly);
        }

        public static void Unregister(IDisposable? display)
        {
            display?.Dispose();
        }

        private static Transform EnsureRoot()
        {
            if (hostObject == null)
            {
                hostObject = new GameObject(ConnectomeStatusHostBehaviour.HostObjectName);
                hostObject.AddComponent<ConnectomeStatusHostBehaviour>();
            }

            return hostObject.transform;
        }
    }
}
