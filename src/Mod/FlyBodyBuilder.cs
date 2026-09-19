using System;
using System.Collections.Generic;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>Rebuilds a Human prefab as seventeen native flesh-and-blood rigid bodies.</summary>
    internal static class FlyBodyBuilder
    {
        public static GameObject CreatePrefab(GameObject prefab)
        {
            // Instantiate's parent overload keeps the entire clone inactive in
            // the hierarchy, deferring native Awake until the graph is complete.
            // https://docs.unity3d.com/2020.2/Documentation/ScriptReference/Object.Instantiate.html
            var staging = new GameObject("Person Connectome fly prefab");
            staging.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(staging);
            GameObject? instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, staging.transform);
                Build(instance);
                // Active-self under an inactive template holder. Native catalog
                // AND ObjectState (save/undo) instantiate the finished hierarchy.
                instance.SetActive(true);
                return instance;
            }
            catch
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                UnityEngine.Object.Destroy(staging);
                throw;
            }
        }

        private static void Build(GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            if (person == null) throw new InvalidOperationException("The fly requires the native Human prefab.");
            instance.SetActive(false);
            var originals = instance.GetComponentsInChildren<LimbBehaviour>(true);
            var headTemplate = Find(originals, "Head");
            var bodyTemplate = Find(originals, "UpperBody");
            var abdomenTemplate = Find(originals, "LowerBody");
            var upperTemplate = Find(originals, "UpperLegFront");
            var lowerTemplate = Find(originals, "LowerLegFront");
            var parts = new List<LimbBehaviour>();
            var origin = new Vector2(0f, .55f);
            var thorax = Part(bodyTemplate, person, parts, "Thorax", "thorax", origin, 0f, .045f, 10);
            var head = Part(headTemplate, person, parts, "Head", "head", origin + new Vector2(-.255f, .015f), 0f, .018f, 12);
            var abdomen = Part(abdomenTemplate, person, parts, "Abdomen", "abdomen", origin + new Vector2(.32f, -.02f), -5f, .035f, 9);
            Connect(head, thorax, origin + new Vector2(-.15f, .015f), 18f);
            Connect(abdomen, thorax, origin + new Vector2(.13f, -.025f), 22f);
            RemoveJoint(thorax);
            thorax.NodeBehaviour.IsRoot = true;
            thorax.HasBrain = false;
            head.HasBrain = true;
            thorax.CirculationBehaviour.IsPump = true;
            thorax.CirculationBehaviour.WasInitiallyPumping = true;

            var uppers = new LimbBehaviour[6];
            var lowers = new LimbBehaviour[6];
            for (var i = 0; i < 6; i++)
            {
                var far = i % 2 == 0;
                var pair = i / 2;
                var hip = origin + new Vector2(-.12f + pair * .105f - (far ? .025f : 0f), far ? -.055f : -.065f);
                FlyGait.Pose(i, 0f, 0f, 0f, 0f, out var upperAngle, out var lowerAngle);
                var knee = hip + Direction(upperAngle) * FlyGait.UpperLength;
                var foot = knee + Direction(lowerAngle) * FlyGait.LowerLength;
                var label = (pair == 0 ? "Front" : pair == 1 ? "Middle" : "Hind") + (far ? "Left" : "Right");
                uppers[i] = Part(upperTemplate, person, parts, label + "Femur", "femur", (hip + knee) * .5f, upperAngle, .006f, far ? 4 : 14);
                lowers[i] = Part(lowerTemplate, person, parts, label + "Tibia", "tibia", (knee + foot) * .5f, lowerAngle, .004f, far ? 5 : 15);
                Connect(uppers[i], thorax, hip, 105f);
                Connect(lowers[i], uppers[i], knee, 120f);
            }

            var wings = new LimbBehaviour[2];
            for (var i = 0; i < wings.Length; i++)
            {
                var angle = 16f + i * 7f;
                var hinge = origin + new Vector2(.015f, .095f);
                var center = hinge + (Vector2)(Quaternion.Euler(0f, 0f, angle) * new Vector3(.34f, 0f, 0f));
                wings[i] = Part(lowerTemplate, person, parts, i == 0 ? "LeftWing" : "RightWing", "wing", center, angle, .002f, i == 0 ? 6 : 13);
                Connect(wings[i], thorax, hinge, 55f, FlyGait.RetainLooseTissue(isWing: true));
            }

            // Catalog Q/E facing is implemented by flipping the prefab root.
            // Center the asymmetric wing/body silhouette around that root so a
            // facing change does not move the fly to the other side of the cursor.
            CenterOnSpawnPivot(parts);

            // None of the original Human geometry remains in the fly assembly.
            foreach (var original in originals)
            {
                original.gameObject.SetActive(false);
                original.transform.SetParent(instance.transform.parent, false);
                UnityEngine.Object.Destroy(original.gameObject);
            }
            person.Limbs = parts.ToArray();
            WireTopology(parts, thorax);
            // Keep every native pose state for PersonBehaviour's physiology,
            // but give the human walking/standing controller no muscle authority.
            person.InitialiseAllPoses();
            foreach (var pose in person.Poses)
            {
                pose.ShouldStandUpright = false;
                pose.ShouldStumble = false;
                pose.UprightForceMultiplier = 0f;
                pose.ForceMultiplier = 0f;
                pose.Rigidity = 0f;
                pose.Angles.Clear();
                pose.ConstructDictionary();
            }
            person.DesiredWalkingDirection = 0f;
            thorax.gameObject.AddComponent<FlyExperienceMemory>();
            thorax.gameObject.AddComponent<FlyHealth>();
            thorax.gameObject.AddComponent<FlyBodyRig>();
            thorax.gameObject.AddComponent<FlyBodyBootstrap>().Configure(head, thorax, abdomen, uppers, lowers, wings);
            thorax.gameObject.AddComponent<FlyConnectomeController>();
            foreach (var part in parts) part.gameObject.SetActive(true);
        }

        private static LimbBehaviour Find(LimbBehaviour[] limbs, string name)
        {
            foreach (var limb in limbs) if (limb.name == name) return limb;
            throw new InvalidOperationException("Human prefab is missing the fly limb template: " + name);
        }

        private static LimbBehaviour Part(LimbBehaviour template, PersonBehaviour person, List<LimbBehaviour> parts,
            string name, string skin, Vector2 position, float angle, float mass, int order)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, person.transform);
            clone.SetActive(false);
            clone.name = "Fly" + name;
            clone.transform.localScale = Vector3.one;
            clone.transform.localPosition = position;
            clone.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var limb = clone.GetComponent<LimbBehaviour>();
            limb.Person = person;
            limb.PhysicalBehaviour = clone.GetComponent<PhysicalBehaviour>();
            limb.CirculationBehaviour = clone.GetComponent<CirculationBehaviour>();
            limb.NodeBehaviour = clone.GetComponent<ConnectedNodeBehaviour>();
            limb.SkinMaterialHandler = clone.GetComponent<SkinMaterialHandler>();
            limb.HasBrain = false;
            limb.HasLungs = skin == "thorax";
            // Inactive pose limbs ignore ForceMultiplier in native MotorStrength.
            // Disable Human muscle torque explicitly; FlyBodyRig supplies only
            // its bounded insect joint torques after the native fixed update.
            limb.BaseStrength = 0f;
            limb.FakeUprightForce = 0f;
            limb.BalanceMuscleMovement = 0f;
            limb.DoBalanceJerk = false;
            limb.DoStumble = false;
            limb.ConnectedLimbs = new List<LimbBehaviour>();
            limb.NodeBehaviour.IsRoot = false;
            limb.NodeBehaviour.Connections = Array.Empty<ConnectedNodeBehaviour>();
            limb.CirculationBehaviour.Limb = limb;
            limb.CirculationBehaviour.Source = null;
            limb.CirculationBehaviour.PushesTo = Array.Empty<CirculationBehaviour>();
            limb.CirculationBehaviour.IsPump = false;
            limb.CirculationBehaviour.WasInitiallyPumping = false;
            var renderer = clone.GetComponent<SpriteRenderer>();
            renderer.sprite = FlyPartAssets.Load(skin);
            renderer.color = order < 6 ? new Color(.68f, .72f, .69f, 1f) : Color.white;
            renderer.sortingOrder = order;
            // Replacement takes place on inactive clones before native Awake caches
            // collider arrays/bounds, so no stale Human collision geometry survives.
            foreach (var old in clone.GetComponents<Collider2D>()) UnityEngine.Object.DestroyImmediate(old);
            var collider = clone.AddComponent<CapsuleCollider2D>();
            var size = renderer.sprite.bounds.size;
            collider.direction = skin == "femur" || skin == "tibia" ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;
            collider.size = new Vector2(size.x * (skin == "wing" ? .9f : .76f), size.y * .78f);
            collider.sharedMaterial = new PhysicsMaterial2D("Fly foot friction") { friction = .9f, bounciness = 0f };
            limb.Collider = collider;
            var body = clone.GetComponent<Rigidbody2D>();
            body.mass = mass;
            // Keep idle wing membranes pose-stable; flight still uses the
            // explicit hinge motor and thorax actuator.
            body.gravityScale = skin == "wing" ? 0f : 1f;
            body.drag = .15f;
            body.angularDrag = .5f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            limb.PhysicalBehaviour.rigidbody = body;
            clone.AddComponent<FlyPartAppearance>().Configure(
                FlyPartAssets.Load(skin + "-flesh"),
                FlyPartAssets.Load(skin + "-bone"), mass);
            parts.Add(limb);
            return limb;
        }

        private static void RemoveJoint(LimbBehaviour limb)
        {
            foreach (var joint in limb.GetComponents<Joint2D>()) UnityEngine.Object.DestroyImmediate(joint);
            limb.Joint = null;
            limb.HasJoint = false;
        }

        private static void Connect(LimbBehaviour child, LimbBehaviour parent, Vector2 anchor, float range,
            bool retainLooseTissue = true)
        {
            RemoveJoint(child);
            var joint = child.gameObject.AddComponent<HingeJoint2D>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = parent.PhysicalBehaviour.rigidbody;
            var world = child.Person.transform.TransformPoint(anchor);
            joint.anchor = child.transform.InverseTransformPoint(world);
            joint.connectedAnchor = parent.transform.InverseTransformPoint(world);
            joint.enableCollision = false;
            joint.useLimits = true;
            joint.limits = new JointAngleLimits2D { min = -range, max = range };
            // Native LimbBehaviour.SetJointFragility supplies the physiological
            // limits on its first ManagedUpdate. Do not break the assembly while
            // the native limb has not yet initialized that strength.
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            child.Joint = joint;
            child.HasJoint = true;
            var tissue = child.GetComponent<GoreStringBehaviour>();
            if (tissue != null)
            {
                // LimbBehaviour checks this flag before creating its optional
                // post-dismemberment SpringJoint2D. Human loose tissue looks and
                // behaves like an elastic cord on a broad insect wing, so wings
                // keep their native break/wound event but not that second joint.
                tissue.enabled = retainLooseTissue;
                tissue.Other = joint.connectedBody;
                tissue.ColourSource = child.CirculationBehaviour;
                tissue.OriginLine = new LineSegment(joint.anchor + new Vector2(-.008f, 0f), joint.anchor + new Vector2(.008f, 0f));
                tissue.OtherLine = new LineSegment(joint.connectedAnchor + new Vector2(-.008f, 0f), joint.connectedAnchor + new Vector2(.008f, 0f));
                tissue.LineWidthMultiplier = .2f;
                tissue.CreateJointOnStart = false;
            }
            child.ConnectedLimbs.Add(parent);
            parent.ConnectedLimbs.Add(child);
            child.CirculationBehaviour.Source = parent.CirculationBehaviour;
        }

        private static void CenterOnSpawnPivot(List<LimbBehaviour> parts)
        {
            var minimumX = float.PositiveInfinity;
            var maximumX = float.NegativeInfinity;
            foreach (var part in parts)
            {
                var renderer = part.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null) continue;
                var bounds = renderer.sprite.bounds;
                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        var corner = new Vector3(bounds.center.x + bounds.extents.x * x,
                            bounds.center.y + bounds.extents.y * y, 0f);
                        var rotated = part.transform.localRotation * corner;
                        var pointX = part.transform.localPosition.x + rotated.x;
                        minimumX = Math.Min(minimumX, pointX);
                        maximumX = Math.Max(maximumX, pointX);
                    }
                }
            }

            var offset = FlyGait.SpawnCenterOffset(minimumX, maximumX);
            if (Math.Abs(offset) <= .0001f) return;
            foreach (var part in parts)
            {
                var position = part.transform.localPosition;
                position.x += offset;
                part.transform.localPosition = position;
            }
        }

        private static void WireTopology(List<LimbBehaviour> parts, LimbBehaviour thorax)
        {
            foreach (var part in parts)
            {
                var nodes = new List<ConnectedNodeBehaviour>();
                var adjacentSkin = new List<SkinMaterialHandler>();
                var downstream = new List<CirculationBehaviour>();
                foreach (var neighbour in part.ConnectedLimbs)
                {
                    nodes.Add(neighbour.NodeBehaviour);
                    adjacentSkin.Add(neighbour.SkinMaterialHandler);
                    if (neighbour.CirculationBehaviour.Source == part.CirculationBehaviour)
                        downstream.Add(neighbour.CirculationBehaviour);
                }
                part.NodeBehaviour.Connections = nodes.ToArray();
                part.SkinMaterialHandler.adjacentLimbs = adjacentSkin.ToArray();
                part.CirculationBehaviour.PushesTo = downstream.ToArray();
                part.CirculationBehaviour.IsDisconnected = false;
            }
            thorax.CirculationBehaviour.Source = null;
        }

        private static Vector2 Direction(float degrees) => Quaternion.Euler(0f, 0f, degrees) * Vector3.down;
    }
}
