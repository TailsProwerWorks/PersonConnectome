using System;

namespace Mod
{
    // Screen geometry only; independent of Unity so supported sizes can be tested.
    internal readonly struct TelemetryLayout
    {
        public readonly float Scale, Width, Height;

        private TelemetryLayout(float scale, float width, float height)
        {
            Scale = scale;
            Width = width;
            Height = height;
        }

        public float ClampHorizontal(int screenWidth, float offset)
        {
            var limit = Math.Max(0f, (screenWidth - Width * Scale) * .5f - 8f);
            return Math.Max(-limit, Math.Min(limit, offset));
        }

        public float ClampVertical(int screenHeight, float top)
        {
            return Math.Max(8f, Math.Min(Math.Max(8f, screenHeight - Height * Scale - 8f), top));
        }

        public static TelemetryLayout Create(int screenWidth, int screenHeight, float textScale, bool collapsed,
            float requestedWidth = 510f, float requestedHeight = 660f)
        {
            var width = Math.Max(1, screenWidth);
            var height = Math.Max(1, screenHeight);
            var sideSpace = Math.Min(180f, width * .2f);
            var availableWidth = width - sideSpace * 2f;
            var availableHeight = Math.Max(1f, height - 16f);
            var preferredScale = Math.Max(1f, Math.Min(2f, height / 1080f)) * Math.Max(.8f, Math.Min(1.6f, textScale));
            requestedWidth = Math.Max(360f, requestedWidth);
            requestedHeight = Math.Max(300f, requestedHeight);
            // Fit a readable control row and viewport before allowing larger text.
            var scale = Math.Min(preferredScale, Math.Min(availableWidth / 400f, availableHeight / 360f));
            return new TelemetryLayout(scale, Math.Min(requestedWidth, availableWidth / scale),
                collapsed ? 48f : Math.Min(requestedHeight, availableHeight / scale));
        }
    }
}
