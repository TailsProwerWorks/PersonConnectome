using System.Buffers.Binary;
using ShadowNineX.PersonConnectome;
using UnityEngine;
using Xunit;

namespace ShadowNineX.PersonConnectome.AdapterTests;

public sealed class FlyPartAssetsTests
{
    [Theory]
    [InlineData("head", .32f, .32f)]
    [InlineData("thorax", .38f, .32f)]
    [InlineData("abdomen", .56f, .32f)]
    [InlineData("wing", .76f, .26f)]
    [InlineData("femur", .07f, .24f)]
    [InlineData("tibia", .09f, .29f)]
    public void PartLayersMatchJointWorldScale(string part, float width, float height)
    {
        var previousSize = ModAPI.SpritePixelSize;
        var previousGate = ModAPI.ThrowOnAssetLoad;
        var previousCache = ModAPI.CachedSprite;
        try
        {
            ModAPI.ThrowOnAssetLoad = false;
            ModAPI.CachedSprite = null;
            foreach (var suffix in new[] { "", "-flesh", "-bone" })
            {
                // Read shipped PNG dimensions rather than a second copy of the artwork sizes.
                var bytes = File.ReadAllBytes(Path.Combine(AssetDirectory(), part + suffix + ".png"));
                ModAPI.SpritePixelSize = new Vector2(
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));

                var sprite = FlyPartAssets.Load(part + suffix);

                Assert.Equal(200f, sprite.pixelsPerUnit, 3);
                Assert.Equal(width, sprite.bounds.extents.x * 2f, 4);
                Assert.Equal(height, sprite.bounds.extents.y * 2f, 4);
            }
        }
        finally
        {
            ModAPI.SpritePixelSize = previousSize;
            ModAPI.ThrowOnAssetLoad = previousGate;
            ModAPI.CachedSprite = previousCache;
        }
    }

    [Fact]
    public void StaleMicroscopicPartIsRejectedBeforeBuildingTheBody()
    {
        var previousCache = ModAPI.CachedSprite;
        var previousGate = ModAPI.ThrowOnAssetLoad;
        try
        {
            ModAPI.ThrowOnAssetLoad = false;
            // PPG caches sprites by path, even if a later call changes scale.
            ModAPI.CachedSprite = new Sprite { pixelsPerUnit = 7000f };
            var exception = Assert.Throws<InvalidOperationException>(() => FlyPartAssets.Load("femur"));
            Assert.Contains("restart PPG", exception.Message);
        }
        finally
        {
            ModAPI.CachedSprite = previousCache;
            ModAPI.ThrowOnAssetLoad = previousGate;
        }
    }

    private static string AssetDirectory()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "assets", "fly");
            if (Directory.Exists(path)) return path;
        }
        throw new DirectoryNotFoundException("Could not locate shipped fly part assets.");
    }
}
