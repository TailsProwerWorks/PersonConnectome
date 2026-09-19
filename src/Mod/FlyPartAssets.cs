using System;
using UnityEngine;

namespace ShadowNineX.PersonConnectome
{
    /// <summary>Loads physical part skins at the same world scale as the fly's joints.</summary>
    internal static class FlyPartAssets
    {
        private const float PixelsPerUnit = 200f;
        private const float NativePixelsPerUnit = 35f;

        // Call during Main, while ModAPI permits path-dependent asset loading.
        public static Sprite Load(string name)
        {
            // PPG multiplies this argument by 35; it is NOT pixels-per-unit.
            var sprite = ModAPI.LoadSprite("fly/" + name + ".png", scale: PixelsPerUnit / NativePixelsPerUnit);
            if (sprite == null || Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > .01f)
                throw new InvalidOperationException("Fly part " + name + " has an invalid world scale. Expected 200 pixels per unit; restart PPG to clear cached mod assets.");
            return sprite;
        }
    }
}
