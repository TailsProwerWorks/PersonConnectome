using UnityEngine;

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

public static class ModAPI
{
    internal static Texture2D? Texture;
    public static Spawnable? FindSpawnable(string _) => new();
    public static object FindCategory(string _) => new();
    public static void Register(Modification _) { }
    public static Texture2D? LoadTexture(string _) => Texture;
}

namespace Mod
{
    internal sealed class PersonConnectomeController { }
}

namespace UnityEngine
{

    public static class Debug
    {
        public static void Log(string _) { }
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
        public static float Sign(float value) => value < 0f ? -1f : value > 0f ? 1f : 0f;
        public static int Min(int left, int right) => Math.Min(left, right);
    }
}
