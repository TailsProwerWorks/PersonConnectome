using System;

namespace Mod
{
    internal enum ManualInputMode
    {
        Mixed,
        ManualOnly
    }

    internal enum ManualInputWaveform
    {
        Continuous,
        Pulse
    }

    internal enum ManualInputChannel
    {
        Light,
        LightOn,
        LightOff,
        Auditory,
        TouchHead,
        TouchArms,
        TouchLegs,
        TouchCore,
        TouchOther,
        Gravity,
        JointPosition,
        JointMotion,
        JointLoad,
        Warm,
        Cool,
        ExpandingVisual,
        LoomingVisual,
        SmallMovingVisual,
        OpticRoll,
        Count
    }

    internal sealed class ManualInputChannelDescriptor
    {
        public readonly ManualInputChannel Channel;
        public readonly string Label;
        public readonly string Details;
        public readonly string[] Targets;
        public readonly string FallbackTarget;
        public readonly bool Directional;

        public ManualInputChannelDescriptor(ManualInputChannel channel, string label, string details, string[] targets, string fallbackTarget = null, bool directional = false)
        {
            Channel = channel;
            Label = label;
            Details = details;
            Targets = targets;
            FallbackTarget = fallbackTarget;
            Directional = directional;
        }
    }

    internal static class ManualInputCatalog
    {
        public static readonly ManualInputChannelDescriptor[] All =
        {
            new ManualInputChannelDescriptor(ManualInputChannel.Light, "Ambient light", "Broad photoreceptor drive · input:light", new[] { "input:light" }),
            new ManualInputChannelDescriptor(ManualInputChannel.LightOn, "Light ON entry", "Explicit luminance-increase route · type:Mi1", new[] { "type:Mi1" }),
            new ManualInputChannelDescriptor(ManualInputChannel.LightOff, "Light OFF entry", "Explicit luminance-decrease route · type:L2 + type:L3", new[] { "type:L2", "type:L3" }),
            new ManualInputChannelDescriptor(ManualInputChannel.Auditory, "Auditory", "Playback proxy · input:auditory", new[] { "input:auditory" }, null, true),
            new ManualInputChannelDescriptor(ManualInputChannel.TouchHead, "Head touch", "Regional bristle proxy · input:touch-head", new[] { "input:touch-head" }),
            new ManualInputChannelDescriptor(ManualInputChannel.TouchArms, "Arm touch", "Regional bristle proxy · input:touch-arms", new[] { "input:touch-arms" }),
            new ManualInputChannelDescriptor(ManualInputChannel.TouchLegs, "Leg touch", "Regional bristle proxy · input:touch-legs", new[] { "input:touch-legs" }),
            new ManualInputChannelDescriptor(ManualInputChannel.TouchCore, "Core touch", "Regional bristle proxy · input:touch-core", new[] { "input:touch-core" }),
            new ManualInputChannelDescriptor(ManualInputChannel.TouchOther, "Other touch", "Remaining tactile class · input:touch-other", new[] { "input:touch-other" }, "input:tactile"),
            new ManualInputChannelDescriptor(ManualInputChannel.Gravity, "Gravity / tilt", "World-horizontal body tilt proxy · input:gravity", new[] { "input:gravity" }, null, true),
            new ManualInputChannelDescriptor(ManualInputChannel.JointPosition, "Joint position", "Connected hinge angle proxy · input:joint-position", new[] { "input:joint-position" }),
            new ManualInputChannelDescriptor(ManualInputChannel.JointMotion, "Joint motion", "Connected hinge speed proxy · input:joint-motion", new[] { "input:joint-motion" }),
            new ManualInputChannelDescriptor(ManualInputChannel.JointLoad, "Joint load", "Connected hinge stress proxy · input:joint-load", new[] { "input:joint-load" }),
            new ManualInputChannelDescriptor(ManualInputChannel.Warm, "Warm", "Thermal drive · input:hot", new[] { "input:hot" }),
            new ManualInputChannelDescriptor(ManualInputChannel.Cool, "Cool", "Thermal drive · input:cold", new[] { "input:cold" }),
            new ManualInputChannelDescriptor(ManualInputChannel.ExpandingVisual, "Expanding visual", "Engineered feature route · type:LC4", new[] { "type:LC4" }, null, true),
            new ManualInputChannelDescriptor(ManualInputChannel.LoomingVisual, "Looming visual", "Engineered feature route · type:LPLC2", new[] { "type:LPLC2" }, null, true),
            new ManualInputChannelDescriptor(ManualInputChannel.SmallMovingVisual, "Small moving visual", "Engineered feature route · type:LC11 + type:LC18", new[] { "type:LC11", "type:LC18" }, null, true),
            new ManualInputChannelDescriptor(ManualInputChannel.OpticRoll, "Optic roll", "World-axis visual motion proxy · input:optic-roll", new[] { "input:optic-roll" })
        };

        public static ManualInputChannelDescriptor For(ManualInputChannel channel)
        {
            return All[(int)channel];
        }
    }

    internal struct ManualInputReading
    {
        public float Live;
        public float Manual;
        public float Effective;
        public float LiveDirection;
        public float ManualDirection;
        public float EffectiveDirection;
        public bool Available;
        public bool Selected;
        public bool PulseActive;
        public int PulseRemaining;
    }

    // Session-local state owned by one controller/person. It deliberately has no
    // Unity or persistence dependency so the input boundary is testable directly.
    internal sealed class ManualInputState
    {
        private readonly bool[] selected = new bool[(int)ManualInputChannel.Count];
        private readonly float[] values = new float[(int)ManualInputChannel.Count];
        private readonly float[] directions = new float[(int)ManualInputChannel.Count];
        private readonly ManualInputWaveform[] waveforms = new ManualInputWaveform[(int)ManualInputChannel.Count];
        private readonly int[] pulseLengths = new int[(int)ManualInputChannel.Count];
        private readonly int[] pulseRemaining = new int[(int)ManualInputChannel.Count];
        private readonly ManualInputReading[] readings = new ManualInputReading[(int)ManualInputChannel.Count];

        public bool OverrideEnabled { get; private set; }
        public ManualInputMode Mode { get; private set; } = ManualInputMode.Mixed;

        public ManualInputState()
        {
            for (var i = 0; i < pulseLengths.Length; i++) pulseLengths[i] = 5;
        }

        public bool IsSelected(ManualInputChannel channel) => selected[(int)channel];
        public void SetSelected(ManualInputChannel channel, bool value)
        {
            selected[(int)channel] = value;
            if (!value) pulseRemaining[(int)channel] = 0;
        }
        public float GetValue(ManualInputChannel channel) => values[(int)channel];
        public void SetValue(ManualInputChannel channel, float value) => values[(int)channel] = Unit(value);
        public float GetDirection(ManualInputChannel channel) => directions[(int)channel];
        public void SetDirection(ManualInputChannel channel, float value) => directions[(int)channel] = Signed(value);
        public ManualInputWaveform GetWaveform(ManualInputChannel channel) => waveforms[(int)channel];
        public void SetWaveform(ManualInputChannel channel, ManualInputWaveform waveform)
        {
            waveforms[(int)channel] = waveform == ManualInputWaveform.Pulse ? ManualInputWaveform.Pulse : ManualInputWaveform.Continuous;
            if (waveforms[(int)channel] == ManualInputWaveform.Continuous) pulseRemaining[(int)channel] = 0;
        }

        public int GetPulseLength(ManualInputChannel channel) => pulseLengths[(int)channel];
        public void SetPulseLength(ManualInputChannel channel, int ticks) => pulseLengths[(int)channel] = Math.Max(1, Math.Min(200, ticks));
        public int GetPulseRemaining(ManualInputChannel channel) => pulseRemaining[(int)channel];
        public bool HasSelectedChannels
        {
            get
            {
                for (var i = 0; i < selected.Length; i++) if (selected[i]) return true;
                return false;
            }
        }

        public void SetOverrideEnabled(bool enabled)
        {
            OverrideEnabled = enabled;
            if (!enabled) CancelPulses();
        }

        public void SetMode(ManualInputMode mode) => Mode = mode == ManualInputMode.ManualOnly ? ManualInputMode.ManualOnly : ManualInputMode.Mixed;

        public void TriggerPulse(ManualInputChannel channel)
        {
            var index = (int)channel;
            if (!OverrideEnabled || !selected[index] || waveforms[index] != ManualInputWaveform.Pulse) return;
            pulseRemaining[index] = pulseLengths[index];
        }

        public void ZeroManualValues()
        {
            Array.Clear(values, 0, values.Length);
            CancelPulses();
        }

        public void ReturnToLive() => SetOverrideEnabled(false);

        public void Deactivate() => SetOverrideEnabled(false);

        public ManualInputReading GetReading(ManualInputChannel channel) => readings[(int)channel];

        public float Resolve(ManualInputChannel channel, float live, float liveDirection, bool available)
        {
            var index = (int)channel;
            live = Unit(live);
            liveDirection = Signed(liveDirection);
            var selectedNow = OverrideEnabled && selected[index];
            var pulseActive = selectedNow && waveforms[index] == ManualInputWaveform.Pulse && pulseRemaining[index] > 0;
            var effective = live;
            var effectiveDirection = liveDirection;
            if (OverrideEnabled)
            {
                if (selected[index])
                {
                    effective = pulseActive || waveforms[index] == ManualInputWaveform.Continuous ? values[index] : 0f;
                    effectiveDirection = effective > .001f ? directions[index] : 0f;
                }
                else if (Mode == ManualInputMode.ManualOnly)
                {
                    effective = 0f;
                    effectiveDirection = 0f;
                }
            }

            var applied = available ? effective : 0f;
            readings[index] = new ManualInputReading
            {
                Live = live,
                Manual = values[index],
                Effective = applied,
                LiveDirection = liveDirection,
                ManualDirection = directions[index],
                EffectiveDirection = applied > .001f ? effectiveDirection : 0f,
                Available = available,
                Selected = selected[index],
                PulseActive = pulseActive,
                PulseRemaining = pulseRemaining[index]
            };
            return applied;
        }

        public void FinishTick()
        {
            if (!OverrideEnabled) return;
            for (var i = 0; i < pulseRemaining.Length; i++)
            {
                if (selected[i] && waveforms[i] == ManualInputWaveform.Pulse && pulseRemaining[i] > 0) pulseRemaining[i]--;
            }
        }

        private void CancelPulses() => Array.Clear(pulseRemaining, 0, pulseRemaining.Length);

        private static float Unit(float value) => IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
        private static float Signed(float value) => IsFinite(value) ? Math.Max(-1f, Math.Min(1f, value)) : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
