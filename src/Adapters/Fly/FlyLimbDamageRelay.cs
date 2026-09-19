using ShadowNineX.PersonConnectome.Core;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>Reports native impacts and size-caps native hinge fragility.</summary>
    [DefaultExecutionOrder(900)]
    internal sealed class FlyLimbDamageRelay : MonoBehaviour
    {
        private FlyHealth? health;
        private LimbBehaviour? limb;
        internal void Bind(FlyHealth owner, LimbBehaviour source) { health = owner; limb = source; }
        private void FixedUpdate()
        {
            if (limb == null || limb.Joint == null || limb.PhysicalBehaviour == null ||
                limb.PhysicalBehaviour.rigidbody == null) return;

            var initialHealth = limb.InitialHealth;
            var healthFraction = initialHealth > 0f ? limb.Health / initialHealth : 0f;
            var forceCap = FlyGait.BreakForceCap(limb.PhysicalBehaviour.rigidbody.mass, healthFraction);
            // LimbBehaviour refreshes native rot/temperature fragility before this
            // component. Only lower its actual hinge limits; never alter the shared
            // BreakingThreshold used by PPG's bone-stress state machine.
            limb.Joint.breakForce = Mathf.Min(limb.Joint.breakForce, forceCap);
            limb.Joint.breakTorque = Mathf.Min(limb.Joint.breakTorque, forceCap * .5f);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (health != null && limb != null) health.RecordNativeImpact(limb, collision);
        }
    }
}
