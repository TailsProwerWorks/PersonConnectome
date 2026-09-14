using UnityEngine;
using Mod;

namespace ShadowNineX.PersonConnectome
{
    public static class ModEntry
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

}
