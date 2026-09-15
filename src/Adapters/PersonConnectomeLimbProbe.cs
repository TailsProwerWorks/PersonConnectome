using System;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>Forwards limb collision callbacks across the adapter boundary.</summary>
    public sealed class PersonConnectomeLimbProbe : MonoBehaviour
    {
        public Transform? OwnerRoot;
        public Action<float>? Report;
        public Action<float>? ReportProjectile;
        public Func<Transform, bool>? IsOwned;
        public Func<bool>? IsConnected;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsSourceConnected() && IsExternal(collision))
            {
                Report?.Invoke(collision.relativeVelocity.magnitude);
                ReportProjectileIfApplicable(collision);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsSourceConnected() && IsExternal(collision) && Report != null)
            {
                Report(collision.relativeVelocity.magnitude * .25f);
            }
        }

        private bool IsSourceConnected() => IsConnected == null || IsConnected();

        private bool IsExternal(Collision2D collision)
        {
            if (collision == null || collision.collider == null) return false;
            var other = collision.collider.transform;
            if (IsOwned != null) return !IsOwned(other);
            return OwnerRoot == null || (other != OwnerRoot && !other.IsChildOf(OwnerRoot));
        }

        private void ReportProjectileIfApplicable(Collision2D collision)
        {
            if (ReportProjectile == null || collision == null || collision.collider == null) return;
            if (PersonConnectomeProjectileDetection.IsProjectile(collision.collider))
                ReportProjectile(collision.relativeVelocity.magnitude);
        }
    }

    // The installed game still exposes this legacy component on compatible projectiles.
#pragma warning disable CS0612 // People Playground marks this compatibility type obsolete but still ships it.
    internal static class PersonConnectomeProjectileDetection
    {
        public static bool IsProjectile(Collider2D collider)
        {
            return collider != null &&
                (collider.GetComponentInParent<ProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<GorseProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<GenericScifiProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<MachineGunProjectileBehaviour>() != null ||
                 collider.GetComponentInParent<LaunchedRocketBehaviour>() != null);
        }

        public static bool IsMovingProjectile(Collider2D collider, PhysicalBehaviour? physical)
        {
            if (!IsProjectile(collider) || physical == null || physical.rigidbody == null) return false;
            var velocity = physical.rigidbody.velocity;
            return IsFinite(velocity.x) && IsFinite(velocity.y) && velocity.magnitude > 1f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
#pragma warning restore CS0612
}
