using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Mod.Adapters
{
    /// <summary>
    /// Small, bounded preference-learning session for the standing controller.
    /// Feedback changes validated controller parameters; it never injects neural
    /// stimulation or writes native body state directly.
    /// </summary>
    internal sealed class PersonTrainingSession
    {
        private const string ProfileVersion = "person-connectome-training-v2";
        private readonly PersonStandingController controller;
        private readonly List<int> feedbackHistory = [];
        private readonly PersonStandingControllerParameters defaultParameters = new();
        private PersonStandingControllerParameters bestParameters;
        private float trialScore;
        private float bestScore;

        public PersonTrainingSession(PersonStandingController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            bestParameters = controller.Parameters.Clone();
            bestScore = float.NegativeInfinity;
        }

        public bool IsActive { get; private set; }
        public bool LearningPaused { get; private set; }
        public int FeedbackCount => feedbackHistory.Count;
        public float TrialScore => trialScore;
        public float BestScore => bestScore;
        public float StandingScore { get; private set; }
        public PersonStandingControllerParameters Parameters => controller.Parameters;

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
            controller.Reset();
            StandingScore = 0f;
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
            controller.Reset();
            StandingScore = 0f;
        }

        /// <summary>
        /// Updates the objective shown to the player from the current native
        /// posture. This is a reward signal, not a button counter.
        /// </summary>
        public void Observe(PersonObservation? observation)
        {
            if (!IsActive || observation == null || !observation.Torso.Valid)
            {
                StandingScore = 0f;
                return;
            }

            var tilt = 1f - Mathf.Clamp01(Mathf.Abs(observation.Torso.TiltDegrees) / 45f);
            var targetHeight = Mathf.Max(.01f, controller.Parameters.DesiredHeight);
            var height = Mathf.Clamp01(observation.Torso.Height / targetHeight);
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
            if (StandingScore > bestScore)
            {
                bestScore = StandingScore;
                bestParameters = controller.Parameters.Clone();
            }
        }

        public void GiveFeedback(bool positive)
        {
            if (!IsActive || LearningPaused) return;
            var feedback = positive ? 1 : -1;
            feedbackHistory.Add(feedback);
            trialScore += feedback;
            AdjustParameters(feedback);
        }

        public void UndoLastFeedback()
        {
            if (!IsActive || feedbackHistory.Count == 0) return;
            var index = feedbackHistory.Count - 1;
            var feedback = feedbackHistory[index];
            feedbackHistory.RemoveAt(index);
            trialScore -= feedback;
            AdjustParameters(-feedback);
        }

        public void RestoreBest()
        {
            if (float.IsNegativeInfinity(bestScore)) return;
            controller.RestoreParameters(bestParameters);
        }

        public void ResetSkill()
        {
            controller.RestoreParameters(defaultParameters);
            bestParameters = defaultParameters.Clone();
            bestScore = float.NegativeInfinity;
            trialScore = 0f;
            feedbackHistory.Clear();
            StandingScore = 0f;
        }

        public string Serialize()
        {
            var values = new List<string>(33) { ProfileVersion };
            AddParameters(values, controller.Parameters);
            AddParameters(values, bestParameters);
            values.Add(float.IsNegativeInfinity(bestScore) ? "none" : bestScore.ToString("R", CultureInfo.InvariantCulture));
            return String.Join("|", values);
        }

        public bool TryLoad(string serialized)
        {
            if (String.IsNullOrWhiteSpace(serialized)) return false;
            var values = serialized.Split('|');
            if (values.Length != 32 || !String.Equals(values[0], ProfileVersion, StringComparison.Ordinal)) return false;
            var current = new PersonStandingControllerParameters();
            var best = new PersonStandingControllerParameters();
            if (!TryReadParameters(values, 1, current) || !TryReadParameters(values, 16, best)) return false;
            var loadedBestScore = float.NegativeInfinity;
            if (!String.Equals(values[31], "none", StringComparison.Ordinal) && !TryReadFinite(values[31], out loadedBestScore)) return false;
            current.Validate();
            best.Validate();
            controller.RestoreParameters(current);
            bestParameters = best;
            bestScore = loadedBestScore;
            trialScore = 0f;
            feedbackHistory.Clear();
            return true;
        }

        private void AdjustParameters(int feedback)
        {
            var parameters = controller.Parameters;
            var direction = feedback * .01f;
            parameters.TiltProportional += direction;
            parameters.TiltDerivative += direction * .25f;
            parameters.JointAngleProportional += direction * .25f;
            parameters.MotorInfluence += direction * .25f;
            parameters.WalkContribution += direction * 2f;
            parameters.Validate();
        }

        private static void AddParameters(List<string> values, PersonStandingControllerParameters parameters)
        {
            values.Add(parameters.TiltProportional.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.TiltDerivative.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.JointAngleProportional.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.HorizontalVelocityDerivative.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.DesiredHeight.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.HeightProportional.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.MaximumMotorSpeed.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.MaximumMotorAcceleration.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.MotorInfluence.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.WalkContribution.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.SideBiasContribution.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.CorePostureScale.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.HeadPostureScale.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.ArmPostureScale.ToString("R", CultureInfo.InvariantCulture));
            values.Add(parameters.UnsupportedLegScale.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool TryReadParameters(string[] values, int offset, PersonStandingControllerParameters parameters)
        {
            var target = new float[15];
            for (var index = 0; index < target.Length; index++)
            {
                if (!TryReadFinite(values[offset + index], out target[index])) return false;
            }

            parameters.TiltProportional = target[0];
            parameters.TiltDerivative = target[1];
            parameters.JointAngleProportional = target[2];
            parameters.HorizontalVelocityDerivative = target[3];
            parameters.DesiredHeight = target[4];
            parameters.HeightProportional = target[5];
            parameters.MaximumMotorSpeed = target[6];
            parameters.MaximumMotorAcceleration = target[7];
            parameters.MotorInfluence = target[8];
            parameters.WalkContribution = target[9];
            parameters.SideBiasContribution = target[10];
            parameters.CorePostureScale = target[11];
            parameters.HeadPostureScale = target[12];
            parameters.ArmPostureScale = target[13];
            parameters.UnsupportedLegScale = target[14];
            return true;
        }

        private static bool TryReadFinite(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                !float.IsNaN(result) && !float.IsInfinity(result);
        }
    }
}
