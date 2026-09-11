namespace PersonConnectome;

public readonly record struct Vec2(float X, float Y)
{
    public float Length => MathF.Sqrt(X * X + Y * Y);
    public Vec2 Normalized => Length > 0.0001f ? new Vec2(X / Length, Y / Length) : default;
    public static float Clamp01(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
    public static float Signed(float value) => float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
}

public static class Numbers
{
    public static float Finite(float value, float fallback = 0f) => float.IsFinite(value) ? value : fallback;
    public static float Clamp(float value, float min, float max) => Math.Clamp(Finite(value), min, max);
    public static float Smooth(float previous, float target, float alpha) => previous + (Clamp(alpha, 0f, 1f) * (Finite(target) - previous));
}
