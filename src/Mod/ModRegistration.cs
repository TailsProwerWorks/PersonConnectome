using UnityEngine;
using Mod;

// The loader entry point intentionally uses the author's unique namespace.
// changing that project-wide would alter every game-facing type name.
#pragma warning disable IDE0130
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

            var flyBase = FindFlyBase();
            if (flyBase == null)
            {
                Debug.Log("Person Connectome: fly base spawnable was unavailable; fly variation was not registered.");
                return;
            }

            ModAPI.Register(new Modification
            {
                OriginalItem = flyBase,
                NameOverride = "Person Connectome Fly [ShadowNineX]",
                DescriptionOverride = "An articulated six-legged fly body driven by the MaleCNS fly channels.",
                CategoryOverride = ModAPI.FindCategory("Entities"),
                AfterSpawn = ConfigureFly
            });
        }

        private static SpawnableAsset? FindFlyBase()
        {
            foreach (var name in new[] { "Ball", "Small Ball", "Apple", "Brick" })
            {
                var candidate = ModAPI.FindSpawnable(name);
                if (candidate != null) return candidate;
            }

            return null;
        }

        private static void ConfigureFly(GameObject instance)
        {
            if (instance == null) return;
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.enabled = false;
            }
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child != instance.transform)
                {
                    Object.Destroy(child.gameObject);
                }
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true))
            {
                Object.Destroy(collider);
            }

            foreach (var childBody in instance.GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (childBody.gameObject != instance)
                {
                    Object.Destroy(childBody);
                }
            }

            foreach (var joint in instance.GetComponentsInChildren<HingeJoint2D>(true))
            {
                Object.Destroy(joint);
            }

            var bodyCollider = instance.AddComponent<CircleCollider2D>();
            bodyCollider.radius = .28f;
            var body = instance.GetComponent<Rigidbody2D>() ?? instance.AddComponent<Rigidbody2D>();
            body.mass = .05f;
            body.gravityScale = .2f;
            body.drag = .8f;
            body.angularDrag = .8f;
            instance.AddComponent<FlyBodyRig>().Initialize();
            instance.AddComponent<FlyConnectomeController>();
        }
    }

}
#pragma warning restore IDE0130
