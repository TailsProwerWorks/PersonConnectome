using System;
using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>
    /// Reads the small set of external cues that a spawned fly can actually obtain
    /// from People Playground. It deliberately does not manufacture organism state.
    /// </summary>
    internal sealed class FlyEnvironmentSensor
    {
        private const float SenseRadius = 12f;
        private const int VisualBandCount = 5;
        private readonly GameObject? root;
        private readonly Transform? assemblyRoot;
        private readonly Rigidbody2D? body;
        private readonly Collider2D[] nearbyColliders = new Collider2D[32];
        private readonly RaycastHit2D[] linecastHits = new RaycastHit2D[16];
        private readonly Collider2D[] visualColliders = new Collider2D[VisualBandCount];
        private readonly Vector2[] visualPoints = new Vector2[VisualBandCount];
        private readonly float[] visualDistances = new float[VisualBandCount];
        private readonly float[] visualThreats = new float[VisualBandCount];
        private Collider2D? headCollider;
        private PhysicalBehaviour? physical;
        private readonly FlyExperienceMemory? experienceMemory;
        private SpawnableAsset? pumpkin;

        public FlyEnvironmentSensor(GameObject root, Rigidbody2D body)
        {
            this.root = root;
            this.body = body;
            assemblyRoot = root == null ? null : root.transform.GetComponentInParent<PersonBehaviour>()?.transform ?? root.transform;
            if (root != null)
            {
                headCollider = root.GetComponent<FlyHealth>()?.Head?.GetComponent<Collider2D>() ?? root.GetComponent<Collider2D>();
                physical = root.GetComponent<PhysicalBehaviour>();
                experienceMemory = root.GetComponent<FlyExperienceMemory>();
            }
            pumpkin = ModAPI.FindSpawnable("Pumpkin");
        }

        public void Read(ref SensoryFrame frame)
        {
            if (root == null || body == null)
            {
                return;
            }

            frame.VisionLimited = false;
            if (headCollider == null) headCollider = root.GetComponent<FlyHealth>()?.Head?.GetComponent<Collider2D>() ?? root.GetComponent<Collider2D>();
            if (physical == null) physical = root.GetComponent<PhysicalBehaviour>();
            if (pumpkin == null) pumpkin = ModAPI.FindSpawnable("Pumpkin");

            ReadPhysical(ref frame, physical);
            frame.Touch = Mathf.Max(frame.Touch, frame.PhysicalContact);
            var headTransform = headCollider == null ? root.transform : headCollider.transform;
            var origin = (Vector2)headTransform.position;
            var facing = headTransform.TransformVector(new Vector3(-1f, 0f, 0f));
            var facingLength = (float)Math.Sqrt(facing.x * facing.x + facing.y * facing.y);
            frame.GazeValid = IsFinite(facing.x) && IsFinite(facing.y) && IsFinite(facingLength) && facingLength > .0001f;
            if (frame.GazeValid)
            {
                facing.x /= facingLength;
                facing.y /= facingLength;
                frame.GazeHeadingDegrees = Degrees(facing.y, facing.x);
            }
            frame.VisualFieldValid = frame.GazeValid;
            frame.FoodCuesValid = true;

            ReadAmbientTemperature(ref frame, origin);
            Array.Clear(visualColliders, 0, visualColliders.Length);
            Array.Clear(visualThreats, 0, visualThreats.Length);
            var hitCount = Physics2D.OverlapCircleNonAlloc(origin, SenseRadius, nearbyColliders);
            if (hitCount == nearbyColliders.Length)
            {
                frame.VisionLimited = true;
                frame.SoundLimited = true;
            }

            for (var i = 0; i < hitCount; i++)
            {
                ReadCollider(ref frame, nearbyColliders[i], origin, facing);
            }
            ReadVisuals(ref frame, origin, facing);
        }

        private void ReadCollider(ref SensoryFrame frame, Collider2D hit, Vector2 origin, Vector3 facing)
        {
            if (hit == null || IsOwnTransform(hit.transform) || !hit.enabled || hit.isTrigger || !hit.gameObject.activeInHierarchy)
            {
                return;
            }

            var point = hit.ClosestPoint(origin);
            var delta = point - origin;
            var distance = delta.magnitude;
            if (!IsFinite(delta.x) || !IsFinite(delta.y) || !IsFinite(distance))
            {
                return;
            }

            var proximity = Unit(1f - distance / SenseRadius);
            if (proximity > frame.Nearby)
            {
                frame.Nearby = proximity;
                frame.NearbyDirection = Math.Abs(delta.x) < .001f ? 0f : Mathf.Sign(delta.x);
            }

            var external = hit.GetComponentInParent<PhysicalBehaviour>();
            if (external != null)
            {
                ReadExternalTemperature(ref frame, external, proximity);
                ReadAudio(ref frame, external, delta, proximity);
                ReadFood(ref frame, hit, external, proximity);
            }

            if (headCollider != null && headCollider.enabled && !headCollider.isTrigger && headCollider.IsTouching(hit))
            {
                frame.Touch = 1f;
                frame.PhysicalContact = 1f;
            }

            if (!frame.GazeValid || distance <= .001f || facing.x * delta.x + facing.y * delta.y <= 0f)
            {
                return;
            }

            var bearing = Degrees(facing.x * delta.y - facing.y * delta.x, facing.x * delta.x + facing.y * delta.y);
            var band = VisualBand(bearing);
            if (visualColliders[band] == null || distance < visualDistances[band])
            {
                visualColliders[band] = hit;
                visualPoints[band] = point;
                visualDistances[band] = distance;
                visualThreats[band] = experienceMemory?.ThreatFor(hit.gameObject) ?? 0f;
            }
        }

        private void ReadVisuals(ref SensoryFrame frame, Vector2 origin, Vector3 facing)
        {
            for (var band = 0; band < VisualBandCount; band++)
            {
                var collider = visualColliders[band];
                if (collider != null) ReadVisual(ref frame, origin, facing, band, collider);
            }
        }

        private void ReadVisual(ref SensoryFrame frame, Vector2 origin, Vector3 facing, int band, Collider2D collider)
        {
            if (!HasLineOfSight(origin, visualPoints[band], collider, ref frame))
            {
                // Only the nearest object in each sector is retained to keep
                // work bounded, so an occluded sector is an incomplete view.
                frame.VisionLimited = true;
                return;
            }

            var delta = visualPoints[band] - origin;
            var distance = visualDistances[band];
            var bearing = Degrees(facing.x * delta.y - facing.y * delta.x, facing.x * delta.x + facing.y * delta.y);
            var strength = frame.LightValid ? Unit((1f - distance / SenseRadius) * frame.Light) : 0f;
            var observation = new VisualObservation
            {
                Observed = strength > 0f,
                Strength = strength,
                BearingDegrees = bearing
            };
            ReadVisualGeometry(ref observation, collider, origin, strength);
            if (collider.GetComponentInParent<ProjectileBehaviour>() != null)
                frame.Projectile = Mathf.Max(frame.Projectile, observation.Approach);
            ApplyLearnedThreat(ref frame, ref observation, band, bearing, strength);
            frame.SetView(band, observation);
            UpdateStrongestVisual(ref frame, observation, delta, bearing, strength);
        }

        private void ApplyLearnedThreat(ref SensoryFrame frame, ref VisualObservation observation, int band, float bearing, float strength)
        {
            // Object recognition is a visual cue: no light means no learned
            // visual threat, even though the physics overlap still exists.
            var learnedThreat = Unit(visualThreats[band] * strength);
            if (learnedThreat <= 0f) return;

            observation.Approach = Mathf.Max(observation.Approach, learnedThreat);
            if (frame.LearnedThreatValid && learnedThreat <= frame.LearnedThreat) return;

            frame.LearnedThreatValid = true;
            frame.LearnedThreat = learnedThreat;
            frame.LearnedThreatDirection = ClampSigned(bearing / 90f);
        }

        private static void UpdateStrongestVisual(ref SensoryFrame frame, VisualObservation observation, Vector2 delta, float bearing, float strength)
        {
            if (strength <= frame.Vision) return;

            frame.Vision = strength;
            frame.VisionDirection = Math.Abs(delta.x) < .001f ? 0f : Mathf.Sign(delta.x);
            frame.VisionDirectionValid = true;
            frame.VisionHeadBearingDegrees = bearing;
            frame.VisionHeadBearingValid = true;
            frame.VisualApproach = observation.Approach;
            frame.VisualGeometryValid = observation.GeometryValid;
            frame.VisualAngularSize = observation.AngularSize;
            frame.VisualExpansion = observation.Expansion;
            frame.VisualAngularSpeed = observation.AngularSpeed;
        }

        private void ReadVisualGeometry(ref VisualObservation observation, Collider2D collider, Vector2 origin, float strength)
        {
            var delta = (Vector2)collider.bounds.center - origin;
            var distance = delta.magnitude;
            if (body == null || !IsFinite(body.velocity.x) || !IsFinite(body.velocity.y) || !IsFinite(body.angularVelocity) || distance <= .001f)
            {
                return;
            }

            var target = collider.GetComponentInParent<PhysicalBehaviour>();
            var targetBody = target == null ? null : target.rigidbody;
            if (targetBody != null && (!IsFinite(targetBody.velocity.x) || !IsFinite(targetBody.velocity.y))) return;
            var targetVelocityX = targetBody != null ? targetBody.velocity.x : 0f;
            var targetVelocityY = targetBody != null ? targetBody.velocity.y : 0f;
            var relativeX = targetVelocityX - body.velocity.x;
            var relativeY = targetVelocityY - body.velocity.y;
            var closing = -(relativeX * delta.x + relativeY * delta.y) / distance;
            observation.Approach = Unit(closing / 10f) * strength;
            var extents = collider.bounds.extents;
            var radius = Mathf.Max(Mathf.Abs(extents.x), Mathf.Abs(extents.y));
            if (!IsFinite(radius) || radius <= 0f)
            {
                return;
            }

            observation.AngularSize = 2f * (float)Math.Atan(radius / distance) * 57.29578f;
            observation.Expansion = Mathf.Max(0f, 2f * radius * closing / (distance * distance + radius * radius) * 57.29578f);
            observation.AngularSpeed = Mathf.Abs((delta.x * relativeY - delta.y * relativeX) / (distance * distance) * 57.29578f - body.angularVelocity);
            observation.GeometryValid = IsFinite(observation.AngularSize) && IsFinite(observation.Expansion) && IsFinite(observation.AngularSpeed);
            if (!observation.GeometryValid) observation.AngularSize = observation.Expansion = observation.AngularSpeed = 0f;
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 targetPoint, Collider2D target, ref SensoryFrame frame)
        {
            var hitCount = Physics2D.LinecastNonAlloc(origin, targetPoint, linecastHits);
            if (hitCount == linecastHits.Length)
            {
                frame.VisionLimited = true;
                return false;
            }

            for (var i = 0; i < hitCount; i++)
            {
                var hit = linecastHits[i].collider;
                if (hit == null || IsOwnTransform(hit.transform) || !hit.enabled || hit.isTrigger || !hit.gameObject.activeInHierarchy) continue;
                return hit == target;
            }
            return false;
        }

        private void ReadFood(ref SensoryFrame frame, Collider2D hit, PhysicalBehaviour external, float proximity)
        {
            if (root == null || !frame.FoodCuesValid || external.isDisintegrated || hit.isTrigger)
            {
                return;
            }

            var dedicatedFood = hit.GetComponentInParent<FlyFoodMarker>() != null;
            var identity = external.GetComponentInParent<SerialiseInstructions>();
            var stockPumpkin = pumpkin != null && identity != null && identity.OriginalSpawnableAsset == pumpkin;
            if (!dedicatedFood && !stockPumpkin)
            {
                return;
            }

            frame.FoodNearbyCue = Mathf.Max(frame.FoodNearbyCue, proximity);
            if (headCollider != null && headCollider.enabled && !headCollider.isTrigger && headCollider.IsTouching(hit))
            {
                var articulatedHead = root.GetComponent<FlyHealth>()?.Head;
                var mouthFrame = articulatedHead == null ? root.transform : articulatedHead.transform;
                var mouthOffset = mouthFrame.TransformVector(articulatedHead == null ? new Vector3(-.4f, -.02f, 0f) : new Vector3(-.12f, -.10f, 0f));
                var mouth = (Vector2)mouthFrame.position + (Vector2)mouthOffset;
                var mouthDistance = (hit.ClosestPoint(mouth) - mouth).magnitude;
                var scale = root.transform.TransformVector(new Vector3(.18f, 0f, 0f));
                var reach = (float)Math.Sqrt(scale.x * scale.x + scale.y * scale.y);
                if (IsFinite(mouthDistance) && IsFinite(reach) && mouthDistance <= reach) frame.FoodContactCue = 1f;
            }
        }

        private static void ReadPhysical(ref SensoryFrame frame, PhysicalBehaviour? source)
        {
            if (source == null) return;
            if (source.isDisintegrated)
            {
                frame.Alive = false;
                frame.HealthValid = true;
                frame.Health = 0f;
            }
            frame.Fire = Mathf.Max(frame.Fire, source.OnFire ? Unit(source.BurnIntensity) : 0f);
            frame.Heat = Mathf.Max(frame.Heat, Unit((source.Temperature - 30f) / 70f));
            frame.Cold = Mathf.Max(frame.Cold, Unit((10f - source.Temperature) / 10f));
            frame.BurnProgress = Mathf.Max(frame.BurnProgress, Unit(source.BurnProgress));
            frame.Wetness = Mathf.Max(frame.Wetness, source.IsUnderWater ? 1f : Unit(source.Wetness));
            frame.UnderWater = Mathf.Max(frame.UnderWater, source.IsUnderWater ? 1f : 0f);
            frame.Lava = Mathf.Max(frame.Lava, source.IsInLava ? 1f : 0f);
            frame.Stabbed = Mathf.Max(frame.Stabbed, source.IsBeingStabbed ? 1f : 0f);
            frame.PhysicalContact = Mathf.Max(frame.PhysicalContact, source.IsTouchingSomething || source.beingHeldByGripper ? 1f : 0f);
            frame.Weightless = Mathf.Max(frame.Weightless, source.IsWeightless ? 1f : 0f);
            frame.Sliding = Mathf.Max(frame.Sliding, source.isSliding ? 1f : 0f);
            frame.Charge = Mathf.Max(frame.Charge, Unit(Mathf.Abs(source.Charge)));
        }

        private static void ReadAmbientTemperature(ref SensoryFrame frame, Vector2 origin)
        {
            var grid = AmbientTemperatureGridBehaviour.Instance;
            if (grid == null) return;
            var temperature = grid.GetTemperatureAtPoint(origin);
            frame.AmbientHeat = Mathf.Max(frame.AmbientHeat, Unit((temperature - 30f) / 70f));
            frame.AmbientCold = Mathf.Max(frame.AmbientCold, Unit((10f - temperature) / 10f));
        }

        private static void ReadExternalTemperature(ref SensoryFrame frame, PhysicalBehaviour source, float proximity)
        {
            if (!IsFinite(source.Temperature)) return;
            frame.AmbientHeat = Mathf.Max(frame.AmbientHeat, Unit((source.Temperature - 30f) / 70f) * proximity);
            frame.AmbientCold = Mathf.Max(frame.AmbientCold, Unit((10f - source.Temperature) / 10f) * proximity);
            if (source.OnFire) frame.AmbientHeat = Mathf.Max(frame.AmbientHeat, proximity);
        }

        private static void ReadAudio(ref SensoryFrame frame, PhysicalBehaviour source, Vector2 delta, float proximity)
        {
            var audio = source.MainAudioSource;
            if (audio == null || !audio.isActiveAndEnabled || !audio.isPlaying || audio.mute || !IsFinite(audio.volume)) return;
            var strength = Unit(audio.volume) * proximity;
            if (strength <= frame.Sound) return;
            frame.Sound = strength;
            frame.SoundDirection = Math.Abs(delta.x) < .001f ? 0f : Mathf.Sign(delta.x);
            frame.SoundDirectionValid = true;
        }

        private bool IsOwnTransform(Transform candidate)
        {
            return assemblyRoot != null && (candidate == assemblyRoot || candidate.IsChildOf(assemblyRoot));
        }

        private static int VisualBand(float bearing)
        {
            if (bearing <= -54f) return 0;
            if (bearing <= -18f) return 1;
            if (bearing < 18f) return 2;
            return bearing < 54f ? 3 : 4;
        }

        private static float Degrees(float y, float x) => (float)Math.Atan2(y, x) * 57.29578f;
        private static float Unit(float value) => IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        private static float ClampSigned(float value) => IsFinite(value) ? Mathf.Clamp(value, -1f, 1f) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
