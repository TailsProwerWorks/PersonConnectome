using System;
using ShadowNineX.PersonConnectome.Adapters;
using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>Restores runtime-only bindings on every clone of the registered physical prefab.</summary>
    [DefaultExecutionOrder(-1100)]
    internal sealed class FlyBodyBootstrap : MonoBehaviour
    {
        [SerializeField] private LimbBehaviour head = null!;
        [SerializeField] private LimbBehaviour thorax = null!;
        [SerializeField] private LimbBehaviour abdomen = null!;
        [SerializeField] private LimbBehaviour[] uppers = Array.Empty<LimbBehaviour>();
        [SerializeField] private LimbBehaviour[] lowers = Array.Empty<LimbBehaviour>();
        [SerializeField] private LimbBehaviour[] wings = Array.Empty<LimbBehaviour>();

        public void Configure(LimbBehaviour headPart, LimbBehaviour thoraxPart, LimbBehaviour abdomenPart,
            LimbBehaviour[] upperParts, LimbBehaviour[] lowerParts, LimbBehaviour[] wingParts)
        {
            head = headPart;
            thorax = thoraxPart;
            abdomen = abdomenPart;
            uppers = upperParts;
            lowers = lowerParts;
            wings = wingParts;
        }

        private void Awake()
        {
            // The six legs and two wings overlap in this side-on body. Establish
            // the same self-collision exclusions as Person.Start before any
            // native limb/controller can activate the new assembly.
            var colliders = new Collider2D[3 + uppers.Length + lowers.Length + wings.Length];
            colliders[0] = ColliderFor(head);
            colliders[1] = ColliderFor(thorax);
            colliders[2] = ColliderFor(abdomen);
            var index = 3;
            foreach (var part in uppers) colliders[index++] = ColliderFor(part);
            foreach (var part in lowers) colliders[index++] = ColliderFor(part);
            foreach (var part in wings) colliders[index++] = ColliderFor(part);
            for (var i = 0; i < colliders.Length; i++)
                for (var j = i + 1; j < colliders.Length; j++)
                    if (colliders[i] != null && colliders[j] != null)
                        Physics2D.IgnoreCollision(colliders[i], colliders[j], true);

            var health = GetComponent<FlyHealth>();
            health.Initialize();
            health.BindRegion(FlyHealthRegion.Head, head);
            health.BindRegion(FlyHealthRegion.Thorax, thorax);
            health.BindRegion(FlyHealthRegion.Abdomen, abdomen);
            health.BindRegion(FlyHealthRegion.LeftWing, wings[0]);
            health.BindRegion(FlyHealthRegion.RightWing, wings[1]);
            for (var i = 0; i < 6; i++) health.BindRegion(FlyHealthRegion.FrontLeftLeg + i, uppers[i], lowers[i]);
            GetComponent<FlyBodyRig>().Initialize(thorax, uppers, lowers, wings);
        }

        private static Collider2D ColliderFor(LimbBehaviour part) => part == null ? null! : part.GetComponent<Collider2D>();
    }
}
