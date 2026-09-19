namespace ShadowNineX.PersonConnectome.Adapters
{
    /// <summary>Small, Unity-free validation helpers shared by liquid/status readers.</summary>
    internal static class LiquidAndStatusReader
    {
        public static bool TryNormalizeFraction(float amount, float total, out float fraction)
        {
            fraction = 0f;
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f ||
                float.IsNaN(total) || float.IsInfinity(total) || total <= 0f)
                return false;

            fraction = amount >= total ? 1f : amount / total;
            return fraction > 0f && !float.IsNaN(fraction) && !float.IsInfinity(fraction);
        }
    }
}
