// Standalone People Playground script. This intentionally uses only the documented
// Modification registration surface and Unity's component attachment API.
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
                ModAPI.Log("Person Connectome: Human spawnable was not found; observer was not registered.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                NameOverride = "Person Connectome Observer",
                DescriptionOverride = "Observe-only status probe. It never moves, damages, spawns, or deletes objects.",
                CategoryOverride = "Entities",
                AfterSpawn = instance =>
                {
                    if (instance != null && instance.GetComponent<PersonConnectomeObserver>() == null)
                        instance.AddComponent<PersonConnectomeObserver>();
                }
            });
        }
    }

    // A deliberately inert marker. It does not invoke methods, mutate the person,
    // create objects, or poll undocumented members. The offline adapter is where
    // target-build member names are mapped and validated before any future bridge.
    public sealed class PersonConnectomeObserver : MonoBehaviour
    {
    }
}
