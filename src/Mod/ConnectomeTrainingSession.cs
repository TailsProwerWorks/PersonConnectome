using System;
using System.Collections.Generic;
using ShadowNineX.PersonConnectome.Adapters;
using ShadowNineX.PersonConnectome.Core;
using ShadowNineX.PersonConnectome.UI;
using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>
    /// Shared feedback-learning lifecycle for every connectome-controlled body.
    /// Feedback only reinforces recent neural eligibility; it never issues an
    /// actuator command or replaces the ordinary brain-to-body path.
    /// </summary>
    internal sealed class ConnectomeTrainingSession
    {
        private const int MaximumUndoEntries = 64;
        private const int MaximumFeedbackEntries = 256;
        private const string EmptyProfile = "connectome-feedback-v1";
        private const string PlasticitySuffix = ".plasticity";
        private const string BestPlasticitySuffix = ".best-plasticity";
        private static ConnectomeTrainingSession? activeSession;

        private readonly LifBrain brain;
        private readonly string profileKey;
        private readonly PersonTrainingSession? postureObjective;
        private readonly ConnectomeLearningMode idleMode;
        private readonly List<string> undoMemory = new();
        private readonly List<int> feedbackHistory = new();
        private string? bestMemory;
        private string lastResult = "no feedback yet";

        public ConnectomeTrainingSession(LifBrain brain, string profileKey, PersonTrainingSession? postureObjective = null,
            ConnectomeLearningMode idleMode = ConnectomeLearningMode.FrozenBaseline)
        {
            this.brain = brain ?? throw new ArgumentNullException(nameof(brain));
            this.profileKey = String.IsNullOrWhiteSpace(profileKey) ? throw new ArgumentException("A profile key is required.", nameof(profileKey)) : profileKey;
            this.postureObjective = postureObjective;
            this.idleMode = idleMode;
            SetMode(ResolveIdleMode());
        }

        public bool IsActive { get; private set; }
        public bool IsPaused { get; private set; }
        public int FeedbackCount => feedbackHistory.Count;
        public float FeedbackScore { get; private set; }
        public int AutonomousFeedbackCount { get; private set; }
        public float DopamineLevel { get; private set; }
        public ConnectomeLearningMode CurrentMode { get; private set; } = ConnectomeLearningMode.FrozenBaseline;

        public string StatusText
        {
            get
            {
                var state = GetStatusState();
                return "TEACH: " + state + " | feedback " + FeedbackCount + " | score " +
                    FeedbackScore.ToString("+0;-0;0") + " | dopamine " + DopamineLevel.ToString("+0.00;-0.00;0.00") +
                    " | " + lastResult;
            }
        }

        public TrainingControlBindings CreateBindings(Action? additionalReset = null)
        {
            return new TrainingControlBindings
            {
                Status = () => StatusText,
                Start = Start,
                End = End,
                TogglePause = TogglePause,
                Good = () => GiveFeedback(true),
                Bad = () => GiveFeedback(false),
                Undo = Undo,
                RestoreBest = RestoreBest,
                Reset = () =>
                {
                    Reset();
                    additionalReset?.Invoke();
                },
                Save = Save,
                Load = Load
            };
        }

        public void Start()
        {
            if (!TryAcquireTrainingLease(this))
            {
                lastResult = "another body is training";
                return;
            }

            IsActive = true;
            IsPaused = false;
            FeedbackScore = 0f;
            DopamineLevel = 0f;
            feedbackHistory.Clear();
            undoMemory.Clear();
            postureObjective?.StartTrial();
            SetMode(ConnectomeLearningMode.PlasticConnectome);
            lastResult = "waiting for recent neural activity";
        }

        public void End()
        {
            postureObjective?.Stop();
            IsActive = false;
            IsPaused = false;
            FeedbackScore = 0f;
            DopamineLevel = 0f;
            feedbackHistory.Clear();
            undoMemory.Clear();
            brain.ResetTransientLearningState();
            SetMode(ResolveIdleMode());
            ReleaseTrainingLease(this);
            lastResult = "training ended";
        }

        public void TogglePause()
        {
            if (!IsActive) return;
            IsPaused = !IsPaused;
            postureObjective?.ToggleLearningPause();
            brain.ResetTransientLearningState();
            SetMode(ResolvePauseMode());
            if (IsPaused) DopamineLevel = 0f;
            lastResult = IsPaused ? "paused activity discarded" : "learning resumed";
        }

        public int GiveFeedback(bool positive)
        {
            if (!IsActive || IsPaused)
            {
                lastResult = "start or resume training first";
                return 0;
            }

            if (undoMemory.Count >= MaximumUndoEntries) undoMemory.RemoveAt(0);
            undoMemory.Add(brain.SerializeLearnedMemory());
            var feedback = positive ? 1 : -1;
            if (feedbackHistory.Count >= MaximumFeedbackEntries) feedbackHistory.RemoveAt(0);
            feedbackHistory.Add(feedback);
            FeedbackScore += feedback;
            postureObjective?.GiveFeedback(positive);
            var changed = brain.ApplyFeedbackReinforcement(feedback);
            lastResult = (positive ? "Good" : "Bad") + " changed " + changed + " eligible edges";
            if (positive && changed > 0) bestMemory = brain.SerializeLearnedMemory();
            return changed;
        }

        public void ApplyContinuousReward(float reward, float elapsedSeconds)
        {
            if (!IsActive || IsPaused) return;
            brain.AdvanceLearningTime(elapsedSeconds);
            brain.ApplyReinforcement(reward, elapsedSeconds);
        }

        public int ApplyAutonomousFeedback(float reward, string reason, float elapsedSeconds = 0f)
        {
            if (IsPaused || CurrentMode != ConnectomeLearningMode.PlasticConnectome ||
                float.IsNaN(reward) || float.IsInfinity(reward) || Math.Abs(reward) < .0001f)
            {
                return 0;
            }

            reward = Math.Max(-1f, Math.Min(1f, reward));
            brain.AdvanceLearningTime(elapsedSeconds);
            DopamineLevel = reward;
            AutonomousFeedbackCount++;
            var changed = brain.ApplyFeedbackReinforcement(reward);
            lastResult = "dopamine " + (reward > 0f ? "+" : "") + reward.ToString("0.00") +
                " " + (String.IsNullOrWhiteSpace(reason) ? "outcome" : reason) + " changed " + changed + " eligible edges";
            if (reward > 0f && changed > 0) bestMemory = brain.SerializeLearnedMemory();
            return changed;
        }

        public void UpdateDopamineLevel(float level)
        {
            if (!IsActive || IsPaused || CurrentMode != ConnectomeLearningMode.PlasticConnectome)
            {
                DopamineLevel = 0f;
                return;
            }

            DopamineLevel = float.IsNaN(level) || float.IsInfinity(level)
                ? 0f
                : Math.Max(-1f, Math.Min(1f, level));
        }

        public void CaptureBestVersion()
        {
            if (IsActive) bestMemory = brain.SerializeLearnedMemory();
        }

        public void Undo()
        {
            if (!IsActive || undoMemory.Count == 0 || feedbackHistory.Count == 0) return;
            var memoryIndex = undoMemory.Count - 1;
            var feedbackIndex = feedbackHistory.Count - 1;
            if (!brain.TryLoadLearnedMemory(undoMemory[memoryIndex])) return;
            undoMemory.RemoveAt(memoryIndex);
            var feedback = feedbackHistory[feedbackIndex];
            feedbackHistory.RemoveAt(feedbackIndex);
            FeedbackScore -= feedback;
            postureObjective?.UndoLastFeedback();
            lastResult = "last feedback undone";
        }

        public void RestoreBest()
        {
            var memory = bestMemory;
            if (memory == null || memory.Length == 0) return;
            if (brain.TryLoadLearnedMemory(memory)) lastResult = "best learned version restored";
        }

        public void Reset()
        {
            postureObjective?.ResetSkill();
            brain.ResetLearnedMemory();
            undoMemory.Clear();
            feedbackHistory.Clear();
            FeedbackScore = 0f;
            DopamineLevel = 0f;
            bestMemory = null;
            brain.ResetTransientLearningState();
            SetMode(IsActive && !IsPaused ? ConnectomeLearningMode.PlasticConnectome : ResolveIdleMode());
            lastResult = "learned skill reset";
        }

        public void Save()
        {
            PlayerPrefs.SetString(profileKey, postureObjective == null ? EmptyProfile : postureObjective.Serialize());
            PlayerPrefs.SetString(profileKey + PlasticitySuffix, brain.SerializeLearnedMemory());
            if (String.IsNullOrEmpty(bestMemory)) PlayerPrefs.DeleteKey(profileKey + BestPlasticitySuffix);
            else PlayerPrefs.SetString(profileKey + BestPlasticitySuffix, bestMemory);
            PlayerPrefs.Save();
            lastResult = "profile saved";
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(profileKey) || !PlayerPrefs.HasKey(profileKey + PlasticitySuffix))
            {
                lastResult = "no saved profile";
                return;
            }

            var previousObjectiveProfile = postureObjective?.Serialize();
            if (IsActive) End();
            var objectiveProfile = PlayerPrefs.GetString(profileKey, String.Empty);
            if (postureObjective != null && !postureObjective.TryLoad(objectiveProfile))
            {
                lastResult = "profile metadata rejected";
                return;
            }
            if (postureObjective == null && !String.Equals(objectiveProfile, EmptyProfile, StringComparison.Ordinal))
            {
                lastResult = "profile metadata rejected";
                return;
            }
            if (!brain.TryLoadLearnedMemory(PlayerPrefs.GetString(profileKey + PlasticitySuffix, String.Empty)))
            {
                if (postureObjective != null && previousObjectiveProfile != null)
                    postureObjective.TryLoad(previousObjectiveProfile);
                lastResult = "learned weights rejected";
                return;
            }

            bestMemory = PlayerPrefs.HasKey(profileKey + BestPlasticitySuffix)
                ? PlayerPrefs.GetString(profileKey + BestPlasticitySuffix, String.Empty)
                : null;
            SetMode(ResolveIdleMode());
            lastResult = "profile loaded";
        }

        public void Release()
        {
            if (IsActive || ReferenceEquals(activeSession, this)) End();
        }

        private void SetMode(ConnectomeLearningMode mode)
        {
            CurrentMode = mode;
            brain.SetLearningMode(mode);
        }

        private ConnectomeLearningMode ResolveIdleMode()
        {
            if (idleMode == ConnectomeLearningMode.PlasticConnectome) return idleMode;
            return brain.LearnedSynapseCount > 0
                ? ConnectomeLearningMode.FrozenLearnedConnectome
                : ConnectomeLearningMode.FrozenBaseline;
        }

        private string GetStatusState()
        {
            if (!IsActive)
            {
                return idleMode == ConnectomeLearningMode.PlasticConnectome ? "autonomous" : "inactive";
            }

            return IsPaused ? "paused" : "learning";
        }

        private ConnectomeLearningMode ResolvePauseMode()
        {
            if (!IsPaused)
            {
                return ConnectomeLearningMode.PlasticConnectome;
            }

            return brain.LearnedSynapseCount > 0
                ? ConnectomeLearningMode.FrozenLearnedConnectome
                : ConnectomeLearningMode.FrozenBaseline;
        }

        private static bool TryAcquireTrainingLease(ConnectomeTrainingSession session)
        {
            if (activeSession != null && !ReferenceEquals(activeSession, session))
            {
                return false;
            }

            activeSession = session;
            return true;
        }

        private static void ReleaseTrainingLease(ConnectomeTrainingSession session)
        {
            if (ReferenceEquals(activeSession, session))
            {
                activeSession = null;
            }
        }
    }
}
