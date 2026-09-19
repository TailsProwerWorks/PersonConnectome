using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>
    /// Passive posture scoring and explicit-feedback bookkeeping for Teach mode.
    /// This type does not own an actuator, action policy, or standing controller.
    /// </summary>
    internal sealed class PersonTrainingSession
    {
        private const string ProfileVersion = "person-connectome-training-v4";
        private const int MaximumFeedbackEntries = 256;
        private const float StandingReferenceHeight = 1.1f;
        private readonly List<int> feedbackHistory = new();
        private float trialScore;
        private float bestScore = float.NegativeInfinity;
        private bool bestScoreImproved;

        public bool IsActive { get; private set; }
        public bool LearningPaused { get; private set; }
        public int FeedbackCount => feedbackHistory.Count;
        public float TrialScore => trialScore;
        public float BestScore => bestScore;
        public float StandingScore { get; private set; }

        public string StatusText
        {
            get
            {
                if (!IsActive) return "TEACH: inactive";
                return "TEACH: " + (LearningPaused ? "paused" : "learning") +
                    " | feedback " + FeedbackCount + " | score " + trialScore.ToString("0.0", CultureInfo.InvariantCulture) +
                    " | best-stand " + (float.IsNegativeInfinity(bestScore) ? "-" : bestScore.ToString("0.00", CultureInfo.InvariantCulture)) +
                    " | stand " + StandingScore.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }

        public void StartTrial()
        {
            IsActive = true;
            LearningPaused = false;
            trialScore = 0f;
            feedbackHistory.Clear();
            StandingScore = 0f;
            bestScoreImproved = false;
        }

        public void ToggleLearningPause()
        {
            if (!IsActive) return;
            LearningPaused = !LearningPaused;
        }

        public void Stop()
        {
            IsActive = false;
            LearningPaused = false;
            feedbackHistory.Clear();
            trialScore = 0f;
            StandingScore = 0f;
            bestScoreImproved = false;
        }

        /// <summary>
        /// Updates the objective shown to the player from the current native
        /// posture. This reads state only; it never emits a motor request.
        /// </summary>
        public void Observe(PersonObservation? observation)
        {
            if (!IsActive || observation == null || !observation.Torso.Valid)
            {
                StandingScore = 0f;
                return;
            }

            var tilt = 1f - Mathf.Clamp01(Mathf.Abs(observation.Torso.TiltDegrees) / 45f);
            var height = Mathf.Clamp01(observation.Torso.Height / StandingReferenceHeight);
            var motion = 1f - Mathf.Clamp01(Mathf.Abs(observation.Torso.AngularVelocityDegreesPerSecond) / 90f +
                Mathf.Abs(observation.Torso.Velocity.x) / 4f);
            var supportCount = 0;
            var legCount = 0;
            for (var index = 0; index < observation.LimbCount; index++)
            {
                var limb = observation[index];
                if (limb.Role != PersonLimbRole.Leg && limb.Role != PersonLimbRole.Foot) continue;
                legCount++;
                if (limb.SupportsBody && limb.HasContact) supportCount++;
            }

            var support = legCount == 0 ? 0f : Mathf.Clamp01((float)supportCount / legCount);
            StandingScore = Mathf.Clamp01(tilt * .4f + height * .25f + motion * .2f + support * .15f);
            if (StandingScore <= bestScore) return;
            bestScore = StandingScore;
            bestScoreImproved = true;
        }

        public bool ConsumeBestScoreImproved()
        {
            var improved = bestScoreImproved;
            bestScoreImproved = false;
            return improved;
        }

        public void GiveFeedback(bool positive)
        {
            if (!IsActive || LearningPaused) return;
            var feedback = positive ? 1 : -1;
            if (feedbackHistory.Count >= MaximumFeedbackEntries) feedbackHistory.RemoveAt(0);
            feedbackHistory.Add(feedback);
            trialScore += feedback;
        }

        public void UndoLastFeedback()
        {
            if (!IsActive || feedbackHistory.Count == 0) return;
            var index = feedbackHistory.Count - 1;
            var feedback = feedbackHistory[index];
            feedbackHistory.RemoveAt(index);
            trialScore -= feedback;
        }

        public void ResetSkill()
        {
            bestScore = float.NegativeInfinity;
            trialScore = 0f;
            feedbackHistory.Clear();
            StandingScore = 0f;
            bestScoreImproved = false;
        }

        public string Serialize()
        {
            return ProfileVersion + "|" +
                (float.IsNegativeInfinity(bestScore) ? "none" : bestScore.ToString("R", CultureInfo.InvariantCulture));
        }

        public bool TryLoad(string serialized)
        {
            if (String.IsNullOrWhiteSpace(serialized)) return false;
            var values = serialized.Split('|');
            if (values.Length != 2 || !String.Equals(values[0], ProfileVersion, StringComparison.Ordinal)) return false;
            var loadedBestScore = float.NegativeInfinity;
            if (!String.Equals(values[1], "none", StringComparison.Ordinal) &&
                (!TryReadFinite(values[1], out loadedBestScore) || loadedBestScore < 0f || loadedBestScore > 1f)) return false;
            bestScore = loadedBestScore;
            trialScore = 0f;
            feedbackHistory.Clear();
            StandingScore = 0f;
            bestScoreImproved = false;
            return true;
        }

        private static bool TryReadFinite(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                !float.IsNaN(result) && !float.IsInfinity(result);
        }
    }
}
