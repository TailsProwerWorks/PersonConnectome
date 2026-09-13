using UnityEngine;

namespace Mod
{
    public static class Mod
    {
        public static void Main()
        {
            var human = ModAPI.FindSpawnable("Human");
            if (human == null)
            {
                Debug.Log("Person Connectome: Human spawnable was not found; active variation was not registered.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                NameOverride = "Person Connectome (Active)",
                DescriptionOverride = "Bounded connectome control with screen telemetry and a full located-soma brain activity map.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                AfterSpawn = instance =>
                {
                    if (instance != null && instance.GetComponent<PersonConnectomeController>() == null)
                    {
                        instance.AddComponent<PersonConnectomeController>();
                    }
                }
            });
        }
    }

    internal sealed partial class ConnectomeBrain
    {
        private static readonly object AssetLock = new object();
        private static RuntimeAsset sharedAsset;

        public static ConnectomeBrain TryCreate(out string status)
        {
            lock (AssetLock)
            {
                if (sharedAsset == null && !RuntimeAsset.TryLoad(out sharedAsset, out status))
                {
                    return null;
                }

                status = "MaleCNS v1.0 loaded";
                return new ConnectomeBrain(sharedAsset);
            }
        }
    }
}
