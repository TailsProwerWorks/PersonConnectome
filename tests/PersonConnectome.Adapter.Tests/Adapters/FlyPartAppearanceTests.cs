using System.Reflection;
using ShadowNineX.PersonConnectome;
using ShadowNineX.PersonConnectome.Adapters;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests
{

    public sealed class FlyPartAppearanceTests
    {
        [Fact]
        public void StartPreservesNativeMaterialAndInitializesPhysicsSkinAndFragmentsWithoutAssetPaths()
        {
            var previousGate = ModAPI.ThrowOnAssetLoad;
            try
            {
                ModAPI.ThrowOnAssetLoad = true;
                var part = new GameObject("Fly leg");
                var body = part.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                var limb = part.AddComponent<LimbBehaviour>();
                limb.PhysicalBehaviour = part.AddComponent<PhysicalBehaviour>();
                limb.PhysicalBehaviour.rigidbody = body;
                limb.SkinMaterialHandler = part.AddComponent<SkinMaterialHandler>();
                var renderer = part.AddComponent<SpriteRenderer>();
                var nativeMaterial = renderer.material;
                var skin = new Sprite();
                renderer.sprite = skin;
                var fragments = part.AddComponent<ShatteredObjectSpriteInitialiser>();
                var flesh = new Sprite { texture = new Texture() };
                var bone = new Sprite { texture = new Texture() };
                var appearance = part.AddComponent<FlyPartAppearance>();
                appearance.Configure(flesh, bone, .35f);

                InvokePrivate(appearance, "Start");

                Assert.Same(nativeMaterial, renderer.material);
                Assert.Same(flesh.texture, nativeMaterial.GetTexture("_FleshTex"));
                Assert.Same(bone.texture, nativeMaterial.GetTexture("_BoneTex"));
                Assert.Equal(.35f, body.mass);
                Assert.Equal(1f, body.gravityScale);
                Assert.Equal(1, limb.SkinMaterialHandler.SyncCalls);
                Assert.Equal(1, fragments.UpdateCalls);
                Assert.Same(skin, fragments.LastLayers.Skin);
                Assert.Same(flesh, fragments.LastLayers.Flesh);
                Assert.Same(bone, fragments.LastLayers.Bone);
            }
            finally
            {
                ModAPI.ThrowOnAssetLoad = previousGate;
            }
        }

        [Fact]
        public void WingAppearanceKeepsTheIdleMembraneOutOfGravity()
        {
            var part = new GameObject("FlyLeftWing");
            var body = part.AddComponent<Rigidbody2D>();
            var limb = part.AddComponent<LimbBehaviour>();
            limb.PhysicalBehaviour = part.AddComponent<PhysicalBehaviour>();
            limb.PhysicalBehaviour.rigidbody = body;
            limb.SkinMaterialHandler = part.AddComponent<SkinMaterialHandler>();
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = new Sprite();
            var appearance = part.AddComponent<FlyPartAppearance>();
            appearance.Configure(new Sprite { texture = new Texture() }, new Sprite { texture = new Texture() }, .002f);

            InvokePrivate(appearance, "Start");

            Assert.Equal(0f, body.gravityScale);
        }

        [Fact]
        public void BootstrapExcludesOnlyTheSeventeenConfiguredPartColliders()
        {
            Physics2D.IgnoredCollisions.Clear();
            var head = AddPart("Head");
            var thorax = AddPart("Thorax");
            thorax.gameObject.AddComponent<FlyHealth>().Initialize();
            thorax.gameObject.AddComponent<FlyBodyRig>();
            var abdomen = AddPart("Abdomen");
            var uppers = Enumerable.Range(0, 6).Select(i => AddPart("Upper " + i)).ToArray();
            var lowers = Enumerable.Range(0, 6).Select(i => AddPart("Lower " + i)).ToArray();
            var wings = new[] { AddPart("Left wing"), AddPart("Right wing") };
            var bootstrap = thorax.gameObject.AddComponent<FlyBodyBootstrap>();
            bootstrap.Configure(head, thorax, abdomen, uppers, lowers, wings);
            var world = new GameObject("World").AddComponent<Collider2D>();

            InvokePrivate(bootstrap, "Awake");

            Assert.Equal(136, Physics2D.IgnoredCollisions.Count);
            Assert.True(Physics2D.GetIgnoreCollision(head.GetComponent<Collider2D>(), wings[1].GetComponent<Collider2D>()));
            Assert.False(Physics2D.GetIgnoreCollision(head.GetComponent<Collider2D>(), world));
        }

        private static LimbBehaviour AddPart(string name)
        {
            var part = new GameObject(name);
            part.AddComponent<Collider2D>();
            var limb = part.AddComponent<LimbBehaviour>();
            limb.PhysicalBehaviour = part.AddComponent<PhysicalBehaviour>();
            limb.PhysicalBehaviour.rigidbody = part.AddComponent<Rigidbody2D>();
            return limb;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, null);
        }
    }
}
