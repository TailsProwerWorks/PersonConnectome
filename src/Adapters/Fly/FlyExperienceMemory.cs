using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>
    /// Bounded per-fly memory that associates a spawned item category with pain.
    /// Categories come from the game's SpawnableAsset identity when available;
    /// this does not claim semantic object understanding.
    /// </summary>
    internal sealed class FlyExperienceMemory : MonoBehaviour
    {
        private const int MaximumCategories = 16;
        private readonly Dictionary<string, Experience> experiences = new(StringComparer.OrdinalIgnoreCase);

        public string LastHarmfulObject { get; private set; } = "none";
        public int KnownThreatCount => experiences.Count;

        public string Summary
        {
            get
            {
                if (experiences.Count == 0) return "none learned";
                var text = new StringBuilder();
                foreach (var pair in experiences)
                {
                    if (text.Length > 0) text.Append("  ");
                    text.Append(pair.Key);
                    text.Append('=');
                    text.Append(pair.Value.Threat.ToString("0.00", CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        public bool RecordHarm(GameObject? source, float severity)
        {
            var category = CategoryOf(source);
            if (String.IsNullOrEmpty(category) || !IsFinite(severity) || severity <= 0f) return false;
            if (!experiences.TryGetValue(category, out var experience))
            {
                EnsureCapacity();
                experience = new Experience();
            }

            // One real painful encounter is enough to create a recognizable cue;
            // repeated harm strengthens the association but remains bounded.
            var learning = Math.Max(.2f, Unit(severity));
            experience.Threat = Unit(experience.Threat + learning * (1f - experience.Threat));
            experience.HarmCount++;
            experiences[category] = experience;
            LastHarmfulObject = category;
            return true;
        }

        public float ThreatFor(GameObject? candidate)
        {
            var category = CategoryOf(candidate);
            return !String.IsNullOrEmpty(category) && experiences.TryGetValue(category, out var experience)
                ? experience.Threat
                : 0f;
        }

        public int HarmCountFor(GameObject? candidate)
        {
            var category = CategoryOf(candidate);
            return !String.IsNullOrEmpty(category) && experiences.TryGetValue(category, out var experience)
                ? experience.HarmCount
                : 0;
        }

        public void Clear()
        {
            experiences.Clear();
            LastHarmfulObject = "none";
        }

        internal static string CategoryOf(GameObject? source)
        {
            if (source == null) return String.Empty;
            var identity = source.transform.GetComponentInParent<SerialiseInstructions>();
            var assetName = identity?.OriginalSpawnableAsset?.name;
            if (!String.IsNullOrWhiteSpace(assetName)) return CleanName(assetName);
            var physical = source.transform.GetComponentInParent<PhysicalBehaviour>();
            return CleanName(physical == null ? source.name : physical.gameObject.name);
        }

        private void EnsureCapacity()
        {
            if (experiences.Count < MaximumCategories) return;
            string? weakestKey = null;
            var weakestThreat = float.MaxValue;
            foreach (var pair in experiences)
            {
                if (pair.Value.Threat < weakestThreat ||
                    (Math.Abs(pair.Value.Threat - weakestThreat) < .0001f && String.CompareOrdinal(pair.Key, weakestKey) < 0))
                {
                    weakestKey = pair.Key;
                    weakestThreat = pair.Value.Threat;
                }
            }
            if (weakestKey != null) experiences.Remove(weakestKey);
        }

        private static string CleanName(string? value)
        {
            if (value == null || value.Trim().Length == 0) return String.Empty;
            var name = value.Trim();
            const string CloneSuffix = "(Clone)";
            if (name.EndsWith(CloneSuffix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - CloneSuffix.Length).Trim();
            return name.Length > 48 ? name.Substring(0, 48) : name;
        }

        private sealed class Experience
        {
            public float Threat;
            public int HarmCount;
        }

        private static float Unit(float value) => IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
