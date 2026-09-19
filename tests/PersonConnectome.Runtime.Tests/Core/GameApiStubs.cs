using UnityEngine;

// These global types deliberately mirror People Playground's un-namespaced API,
// and Spawnable's members remain instance members because linked production
// sources reference them by those exact names and invocation shapes.
#pragma warning disable S3903
#pragma warning disable S2325
public sealed class Spawnable
{
    public T? GetComponent<T>() where T : class => null;
    public T AddComponent<T>() where T : new() => new();
}

public sealed class Modification
{
    public Spawnable? OriginalItem { get; set; }
    public string? NameOverride { get; set; }
    public string? DescriptionOverride { get; set; }
    public object? CategoryOverride { get; set; }
    public Action<Spawnable>? AfterSpawn { get; set; }
}

#pragma warning restore S2325

// ModAPI is the exact game API type name consumed by the linked runtime source.
#pragma warning disable S101
public static class ModAPI
{
    internal static Texture2D? Texture;
    internal static bool ThrowOnTextureLoad;
    internal static int LoadTextureCalls;
    public static Spawnable? FindSpawnable(string _) => new();
    public static object FindCategory(string _) => new();
    public static Modification? LastRegisteredModification { get; private set; }
    public static void Register(Modification modification) => LastRegisteredModification = modification;
    public static Texture2D? LoadTexture(string _)
    {
        LoadTextureCalls++;
        if (ThrowOnTextureLoad) throw new InvalidOperationException("ModAPI texture loading is forbidden in this test.");
        return Texture;
    }
}
#pragma warning restore S101
#pragma warning restore S3903

namespace UnityEngine
{

    public static class Debug
    {
        internal static string? LastMessage { get; private set; }
        public static void Log(string message) => LastMessage = message;
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte red, byte green, byte blue, byte alpha = 255)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }
    }

    public sealed class Texture2D
    {
        private readonly Color32[] pixels;
        public int width { get; }
        public int height { get; }
        public Texture2D(int textureWidth, int textureHeight, Color32[] sourcePixels)
        {
            width = textureWidth;
            height = textureHeight;
            pixels = sourcePixels;
        }
        public Color32[] GetPixels32() => pixels;
    }

    public static class Mathf
    {
        public static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Abs(float value) => MathF.Abs(value);
        public static float Sign(float value)
        {
            if (value < 0f)
                return -1f;

            if (value > 0f)
                return 1f;

            return 0f;
        }
        public static int Min(int left, int right) => Math.Min(left, right);
    }
}
