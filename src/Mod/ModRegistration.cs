using UnityEngine;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;

// The loader entry point intentionally uses the author's unique namespace.
// changing that project-wide would alter every game-facing type name.
#pragma warning disable IDE0130
namespace ShadowNineX.PersonConnectome
{
    public static class ModEntry
    {
        public static void Main()
        {
            // Prefab Awake and component Start do not have a path-dependent
            // ModAPI context. Cache the shared graph while Main does.
            if (!LifBrain.PrepareAsset(out var assetStatus))
                Debug.Log("Person Connectome: asset preparation failed: " + assetStatus);
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

            RegisterFlyFood();

            // Register the complete physical hierarchy as the asset's prefab so
            // catalog spawn, saved contraptions and Undo all clone the same body.
            var flyBase = Object.Instantiate(human);
            flyBase.Prefab = FlyBodyBuilder.CreatePrefab(human.Prefab);

            ModAPI.Register(new Modification
            {
                OriginalItem = flyBase,
                NameOverride = "Person Connectome Fly [ShadowNineX]",
                DescriptionOverride = "A six-legged connectome fly. Try approaching its head, touching it, placing a Pumpkin nearby, or changing the light. Inspect its senses and stimulate inputs in the shared brain panel.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                ThumbnailOverride = ModAPI.LoadSprite("fly-thumb.png"),
                AfterSpawn = instance => { if (instance != null) instance.name = "Person Connectome Fly"; }
            });
        }

        private static void RegisterFlyFood()
        {
            var foodBase = ModAPI.FindSpawnable("Pumpkin") ?? ModAPI.FindSpawnable("Apple");
            if (foodBase == null)
            {
                Debug.Log("Person Connectome: no Pumpkin or Apple was available for the fly-food variation.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = foodBase,
                NameOverride = "Fly Treat [ShadowNineX]",
                DescriptionOverride = "A dedicated positive-reinforcement food cue for the Person Connectome Fly. Mouth contact produces a bounded dopamine-style learning signal.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                AfterSpawn = instance =>
                {
                    if (instance != null && instance.GetComponent<FlyFoodMarker>() == null)
                        instance.AddComponent<FlyFoodMarker>();
                }
            });
        }

    }
}
#pragma warning restore IDE0130
