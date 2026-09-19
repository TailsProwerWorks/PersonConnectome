using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>Sets each native limb's own skin layers after native material initialization.</summary>
    [DefaultExecutionOrder(900)]
    internal sealed class FlyPartAppearance : MonoBehaviour
    {
        [SerializeField] private Sprite flesh = null!;
        [SerializeField] private Sprite bone = null!;
        [SerializeField] private float mass;
        private Material? material;

        public void Configure(Sprite fleshSprite, Sprite boneSprite, float partMass)
        {
            flesh = fleshSprite;
            bone = boneSprite;
            mass = partMass;
        }

        private void Start()
        {
            var limb = GetComponent<LimbBehaviour>();
            // Physics setup must not depend on success of visual material setup.
            limb.PhysicalBehaviour.rigidbody.mass = mass;
            limb.PhysicalBehaviour.rigidbody.gravityScale = IsWing(limb) ? 0f : 1f;
            var renderer = GetComponent<SpriteRenderer>();
            // SkinMaterialHandler caches this same per-renderer material in Awake.
            // Replacing it here would disconnect the native wound shader updates.
            material = renderer.material;
            material.SetTexture("_FleshTex", flesh.texture);
            material.SetTexture("_BoneTex", bone.texture);
            limb.SkinMaterialHandler.Sync();
            var fragments = GetComponent<ShatteredObjectSpriteInitialiser>();
            if (fragments != null)
            {
                var layers = new LimbSpriteCache.LimbSprites(renderer.sprite, flesh, bone);
                fragments.UpdateSprites(in layers);
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private static bool IsWing(LimbBehaviour limb) => limb.name == "FlyLeftWing" || limb.name == "FlyRightWing";
    }
}
