using UnityEngine;

namespace Mod
{
    internal sealed partial class ConnectomeBrain
    {
        private void DriveSensoryPopulations(SensoryFrame sensory)
        {
            var healthDeficit = 1f - sensory.Health;
            var oxygenDeficit = 1f - sensory.Oxygen;
            var consciousnessDeficit = 1f - sensory.Consciousness;
            var injury = Mathf.Clamp01(
                sensory.Pain + sensory.Damage + sensory.Bleeding + healthDeficit + sensory.Blood +
                sensory.LimbLoss + sensory.JointStress + sensory.BrainDamage + sensory.InternalBleeding +
                sensory.Wounds + sensory.LungDamage + (1f - sensory.Vitality) + sensory.Stabbed + sensory.Seizure);
            var circulationDeficit = 1f - sensory.Circulation;
            var hazard = Mathf.Clamp01(
                sensory.Fire + sensory.Heat + sensory.Cold + sensory.Shock + sensory.SubmergedHypoxia + oxygenDeficit +
                sensory.Wetness + sensory.Charge + sensory.Infection + sensory.LiquidHazard + sensory.Lava +
                sensory.BurnProgress + sensory.Disconnected + sensory.Frozen + circulationDeficit);
            var bodyMotion = Mathf.Clamp01(
                sensory.Impact + sensory.Sound * .6f + sensory.Velocity * .6f + sensory.Rotation * .4f +
                sensory.Balance * .4f + sensory.Numbness + sensory.Paralysis + sensory.Weightless +
                sensory.Sliding + sensory.PhysicalContact * .15f + sensory.Touch * .15f + sensory.LiquidExposure + sensory.LiquidWater +
                sensory.Heartbeat * .1f);
            var visual = Mathf.Clamp01(sensory.Light + sensory.Nearby);
            var arousal = Mathf.Clamp01(
                sensory.Adrenaline + sensory.Unconscious + consciousnessDeficit + sensory.LiquidStimulation);
            injuryDrive = injury;
            hazardDrive = hazard;
            motionDrive = bodyMotion;
            arousalDrive = arousal;
            LastSensoryDrive = Mathf.Clamp01(injury + hazard + bodyMotion + arousal);

            // These population names are present in the bundled asset metadata.
            // LiquidHealing remains a direct, bounded restorative-output request,
            // and NearbyDirection remains geometry for escape direction; neither
            // has a validated MaleCNS population mapping and is not injected here.
            // UnderWater is retained separately so the adapter can gate SubmergedHypoxia;
            // wetness and liquid-water already provide its exposure drive here.
            Drive("superclass:ol_sensory", LastSensoryDrive, 2048);
            Drive("type:R1-R6", visual, 256);
            Drive("type:R7R8_unclear", sensory.Light, 64);
            Drive("type:R7_unclear", sensory.Light, 64);
            Drive("type:R7d", sensory.Light, 64);
            Drive("type:R7p", sensory.Light, 64);
            Drive("type:R7y", sensory.Light, 64);
            Drive("type:R8_unclear", sensory.Light, 64);
            Drive("type:R8d", sensory.Light, 64);
            Drive("type:R8p", sensory.Light, 64);
            Drive("type:R8y", sensory.Light, 64);
            Drive("type:LC4", Mathf.Clamp01(hazard + sensory.Nearby), 128);
            Drive("type:LPLC2", Mathf.Clamp01(injury + hazard + sensory.LiquidSedation), 128);
            Drive("type:MDN", Mathf.Clamp01(injury + sensory.SubmergedHypoxia), 128);
            Drive("type:DNp09", Mathf.Clamp01(visual + bodyMotion), 128);
        }

        private float LastSensoryDrive { get; set; }

        private void Drive(string population, float value, int maximum)
        {
            value = Mathf.Clamp(value, 0f, 4f);
            if (value <= .001f)
            {
                return;
            }

            var ids = asset.Population(population);
            var count = Mathf.Min(maximum, ids.Count);
            for (var i = 0; i < count; i++)
            {
                var id = ids[i];
                if (!pending.TryGetValue(id, out var queued))
                {
                    queued = 0f;
                }

                pending[id] = Mathf.Clamp(queued + value, -4f, 4f);
                active.Add(id);
                priority.Add(id);
            }
        }
    }
}
