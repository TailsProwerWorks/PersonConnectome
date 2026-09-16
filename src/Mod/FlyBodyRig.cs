using System;
using System.Collections.Generic;
using Mod.Core;
using UnityEngine;

namespace Mod
{
    /// <summary>
    /// Lightweight articulated fly body. The root carries flight physics and
    /// each of the six legs has its own rigidbody and hinge attachment.
    /// </summary>
    internal sealed class FlyBodyRig : MonoBehaviour, IFlyBodyRig
    {
        private readonly List<Rigidbody2D> legs = [];
        private readonly List<Rigidbody2D> wings = [];
        private readonly List<Rigidbody2D> groomingParts = [];
        private Rigidbody2D? rootBody;
        private Material? lineMaterial;

        public bool IsUsable => rootBody != null && legs.Count == 6;

        public void Initialize()
        {
            rootBody = gameObject.GetComponent<Rigidbody2D>();
            if (rootBody == null)
            {
                rootBody = gameObject.AddComponent<Rigidbody2D>();
            }

            rootBody.mass = .05f;
            rootBody.gravityScale = .2f;
            rootBody.drag = .8f;
            rootBody.angularDrag = .8f;
            lineMaterial = CreateLineMaterial();
            CreateBodyOutline();
            CreateLegs();
        }

        public void Apply(FlyMotorCommand command, float elapsedSeconds)
        {
            if (!IsUsable || rootBody == null) return;

            var legDrive = ClampSigned(command.FlyLegMotor);
            var asymmetry = ClampSigned(command.FlyLegMotorAsym);
            var wingDrive = Unit(command.FlyWingMotor + command.FlySongPulse + command.FlySong);
            var wingLift = wingDrive * .35f;
            for (var i = 0; i < legs.Count; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var sideDrive = ClampSigned(legDrive + asymmetry * side);
                var grooming = i % 3 == 0 ? Unit(command.FlyGroomLeg) : Unit(command.FlyGroomAbdomen);
                var legVelocity = legs[i].velocity;
                legVelocity.x = rootBody.velocity.x + sideDrive * 1.8f - side * grooming * .8f;
                legVelocity.y = rootBody.velocity.y + wingLift - grooming * .5f;
                legs[i].velocity = legVelocity;
            }

            for (var i = 0; i < wings.Count; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var wingVelocity = wings[i].velocity;
                wingVelocity.x = rootBody.velocity.x + side * wingDrive * 1.2f;
                wingVelocity.y = rootBody.velocity.y + wingLift;
                wings[i].velocity = wingVelocity;
            }

            var antennaGroom = Unit(command.FlyGroomAntenna + command.FlyGroomHead);
            var abdomenGroom = Unit(command.FlyGroomAbdomen);
            for (var i = 0; i < groomingParts.Count; i++)
            {
                var groomingVelocity = groomingParts[i].velocity;
                var drive = i < 2 ? antennaGroom : abdomenGroom;
                groomingVelocity.x = rootBody.velocity.x + (i % 2 == 0 ? -drive : drive) * .75f;
                groomingVelocity.y = rootBody.velocity.y + drive * .3f;
                groomingParts[i].velocity = groomingVelocity;
            }
        }

        private void CreateLegs()
        {
            if (legs.Count == 6) return;
            legs.Clear();
            var positions = new[]
            {
                new Vector3(-.16f, .08f, 0f), new Vector3(.16f, .08f, 0f),
                new Vector3(-.2f, 0f, 0f), new Vector3(.2f, 0f, 0f),
                new Vector3(-.16f, -.08f, 0f), new Vector3(.16f, -.08f, 0f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var legObject = new GameObject("Fly leg " + (i + 1));
                legObject.transform.SetParent(transform);
                legObject.transform.localPosition = positions[i];
                legObject.transform.localScale = new Vector3(.18f, .42f, 1f);
                var legSide = i % 2 == 0 ? -1f : 1f;
                CreateLine(legObject, new[] { Vector3.zero, new Vector3(legSide * .42f, -.28f, 0f) }, .035f, new Color(.3f, .75f, .8f, .95f));
                var legBody = legObject.AddComponent<Rigidbody2D>();
                legBody.mass = .005f;
                legBody.gravityScale = .2f;
                legBody.drag = 1.5f;
                var joint = legObject.AddComponent<HingeJoint2D>();
                joint.connectedBody = rootBody;
                legObject.AddComponent<CircleCollider2D>();
                legs.Add(legBody);
            }

            CreateWing("Fly left wing", new Vector3(-.2f, .12f, 0f), -1f);
            CreateWing("Fly right wing", new Vector3(.2f, .12f, 0f), 1f);
            CreateGroomingPart("Fly left antenna", new Vector3(-.34f, .08f, 0f), new Vector3(.06f, .2f, 1f));
            CreateGroomingPart("Fly right antenna", new Vector3(-.34f, -.08f, 0f), new Vector3(.06f, .2f, 1f));
            CreateGroomingPart("Fly abdomen", new Vector3(0f, -.13f, 0f), new Vector3(.2f, .28f, 1f));
        }

        private void CreateWing(string name, Vector3 position, float side)
        {
            var wingObject = new GameObject(name);
            wingObject.transform.SetParent(transform);
            wingObject.transform.localPosition = position;
            wingObject.transform.localScale = new Vector3(.42f, .12f, 1f);
            CreateLine(wingObject, new[] { Vector3.zero, new Vector3(side * .42f, .13f, 0f) }, .055f, new Color(.45f, .85f, .95f, .65f));
            var wingBody = wingObject.AddComponent<Rigidbody2D>();
            wingBody.mass = .002f;
            wingBody.gravityScale = 0f;
            wingBody.drag = 2f;
            var joint = wingObject.AddComponent<HingeJoint2D>();
            joint.connectedBody = rootBody;
            wingObject.AddComponent<CircleCollider2D>();
            wings.Add(wingBody);
        }

        private void CreateGroomingPart(string name, Vector3 position, Vector3 scale)
        {
            var partObject = new GameObject(name);
            partObject.transform.SetParent(transform);
            partObject.transform.localPosition = position;
            partObject.transform.localScale = scale;
            var end = position.x < -.3f
                ? new Vector3(-.14f, position.y >= 0f ? .18f : -.18f, 0f)
                : new Vector3(0f, -.18f, 0f);
            CreateLine(partObject, new[] { Vector3.zero, end }, .03f, new Color(.2f, .6f, .7f, .85f));
            var partBody = partObject.AddComponent<Rigidbody2D>();
            partBody.mass = .003f;
            partBody.gravityScale = .1f;
            partBody.drag = 1.8f;
            var joint = partObject.AddComponent<HingeJoint2D>();
            joint.connectedBody = rootBody;
            partObject.AddComponent<CircleCollider2D>();
            groomingParts.Add(partBody);
        }

        private void CreateBodyOutline()
        {
            CreateEllipse(new Vector3(-.27f, 0f, 0f), .13f, .13f, .1f, new Color(.16f, .32f, .5f, 1f));
            CreateEllipse(new Vector3(-.02f, 0f, 0f), .24f, .18f, .12f, new Color(.12f, .24f, .4f, 1f));
            CreateEllipse(new Vector3(.24f, 0f, 0f), .22f, .14f, .1f, new Color(.2f, .34f, .52f, 1f));
            CreateEllipse(new Vector3(-.31f, .06f, 0f), .025f, .025f, .03f, new Color(.45f, .95f, 1f, 1f));
            CreateEllipse(new Vector3(-.31f, -.06f, 0f), .025f, .025f, .03f, new Color(.45f, .95f, 1f, 1f));
        }

        private void CreateEllipse(Vector3 center, float radiusX, float radiusY, float width, Color color)
        {
            const int segments = 16;
            var points = new Vector3[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Math.PI * 2d / segments;
                points[i] = center + new Vector3((float)Math.Cos(angle) * radiusX, (float)Math.Sin(angle) * radiusY, 0f);
            }

            CreateLine(gameObject, points, width, color);
        }

        private LineRenderer CreateLine(GameObject owner, Vector3[] points, float width, Color color)
        {
            var lineObject = new GameObject(owner.name + " visual");
            lineObject.transform.SetParent(owner.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = points.Length;
            line.useWorldSpace = false;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.material = lineMaterial;
            line.sortingOrder = 100;
            line.SetPositions(points);
            return line;
        }

        private static Material? CreateLineMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            return shader == null ? null : new Material(shader);
        }

        private static float Unit(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(0f, value)) : 0f;
        private static float ClampSigned(float value) => IsFinite(value) ? Math.Min(1f, Math.Max(-1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
