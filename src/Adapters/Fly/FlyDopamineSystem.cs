using System;
using ShadowNineX.PersonConnectome.Core;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>
    /// Converts observable outcomes into sparse signed reinforcement. Positive
    /// food contact and negative pain are applied to recent neural eligibility.
    /// </summary>
    internal sealed class FlyDopamineSystem
    {
        private const float FoodReward = .75f;
        private const float FoodRewardCooldownSeconds = 2f;
        private bool foodContactWasActive;
        private float foodRewardCooldown;

        public float Level { get; private set; }
        public string LastEvent { get; private set; } = "none";

        public float Observe(SensoryFrame frame, float elapsedSeconds)
        {
            var elapsed = IsFinite(elapsedSeconds) ? Math.Max(0f, Math.Min(.25f, elapsedSeconds)) : 0f;
            foodRewardCooldown = Math.Max(0f, foodRewardCooldown - elapsed);
            Level = MoveTowards(Level, 0f, elapsed * .7f);

            var foodContact = frame.FoodCuesValid && Unit(frame.FoodContactCue) >= .5f;
            var reward = 0f;
            if (foodContact && (!foodContactWasActive || foodRewardCooldown <= 0f))
            {
                reward += FoodReward;
                foodRewardCooldown = FoodRewardCooldownSeconds;
                LastEvent = "food contact";
            }
            foodContactWasActive = foodContact;

            // FlyHealth exposes persistent total damage through Pain for UI, but
            // reinforcement must be an outcome impulse rather than 20 Hz punishment.
            var pain = Unit(frame.DamageEvent);
            if (pain > 0f)
            {
                reward -= Math.Max(.25f, pain);
                LastEvent = "pain";
            }

            reward = ClampSigned(reward);
            if (Math.Abs(reward) > .0001f) Level = reward;
            return reward;
        }

        public void Reset()
        {
            foodContactWasActive = false;
            foodRewardCooldown = 0f;
            Level = 0f;
            LastEvent = "none";
        }

        private static float MoveTowards(float value, float target, float step) => value + Math.Max(-step, Math.Min(step, target - value));
        private static float Unit(float value) => IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
        private static float ClampSigned(float value) => IsFinite(value) ? Math.Max(-1f, Math.Min(1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
