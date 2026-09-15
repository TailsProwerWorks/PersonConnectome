using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>Coarse anatomical slot used by the person-only posture adapter.</summary>
    internal enum PersonLimbRole
    {
        Other,
        Head,
        Core,
        Arm,
        Hand,
        Leg,
        Foot
    }

    /// <summary>Side in People Playground's side-on person hierarchy.</summary>
    internal enum PersonLimbSide
    {
        Center,
        Left,
        Right
    }

    /// <summary>Physics state sampled from the torso once per adapter update.</summary>
    internal struct PersonTorsoObservation
    {
        public bool Valid { get; set; }
        public float TiltDegrees { get; set; }
        public float AngularVelocityDegreesPerSecond { get; set; }
        public Vector2 Velocity { get; set; }
        public float Height { get; set; }
    }

    /// <summary>One reusable, adapter-owned limb sample for posture control.</summary>
    internal struct PersonLimbObservation
    {
        public LimbBehaviour? Limb { get; set; }
        public PersonLimbRole Role { get; set; }
        public PersonLimbSide Side { get; set; }
        public float JointAngleDegrees { get; set; }
        public float JointSpeedDegreesPerSecond { get; set; }
        public bool Usable { get; set; }
        public bool SupportsBody { get; set; }
        public bool HasContact { get; set; }
    }

    /// <summary>
    /// Mutable collection of one native physics sample. The adapter retains an
    /// instance per person and calls <see cref="Clear"/> before filling it, so
    /// normal sampling does not allocate a new limb array each tick.
    /// </summary>
    internal sealed class PersonObservation
    {
        private readonly List<PersonLimbObservation> limbs;

        public PersonObservation(int capacity = 16)
        {
            limbs = new List<PersonLimbObservation>(Math.Max(1, capacity));
        }

        public PersonTorsoObservation Torso;
        public int LimbCount => limbs.Count;
        public PersonLimbObservation this[int index] => limbs[index];

        public void Clear()
        {
            Torso = default;
            limbs.Clear();
        }

        public void AddLimb(PersonLimbObservation observation)
        {
            limbs.Add(observation);
        }
    }

    /// <summary>One final, degree-per-second joint target for native actuation.</summary>
    internal struct PersonJointTarget
    {
        public LimbBehaviour Limb;
        public float MotorSpeedDegreesPerSecond;
        public float Influence;
    }

    /// <summary>
    /// Reusable output buffer for <see cref="PersonStandingController"/>. It
    /// grows only when a person has more controllable joints than before.
    /// </summary>
    internal sealed class PersonJointTargetBuffer
    {
        private PersonJointTarget[] targets;

        public PersonJointTargetBuffer(int capacity = 16)
        {
            targets = new PersonJointTarget[Math.Max(1, capacity)];
        }

        public int Count { get; private set; }
        public PersonJointTarget this[int index] => targets[index];

        public void Clear()
        {
            Count = 0;
        }

        public void Add(LimbBehaviour limb, float motorSpeedDegreesPerSecond, float influence)
        {
            if (limb == null)
            {
                return;
            }

            if (Count == targets.Length)
            {
                Array.Resize(ref targets, targets.Length * 2);
            }

            targets[Count++] = new PersonJointTarget
            {
                Limb = limb,
                MotorSpeedDegreesPerSecond = motorSpeedDegreesPerSecond,
                Influence = influence
            };
        }
    }
}
