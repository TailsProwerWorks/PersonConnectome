namespace Mod.Core
{
    /// <summary>Creates brains from the single validated, shared graph asset.</summary>
    internal sealed partial class LifBrain
    {
        private static readonly object AssetLock = new();
        private static ModAsset? sharedAsset;

        public static LifBrain? TryCreate(out string status)
        {
            lock (AssetLock)
            {
                if (sharedAsset == null)
                {
                    if (!ModAsset.TryLoad(out var loadedAsset, out status) || loadedAsset == null)
                    {
                        return null;
                    }

                    sharedAsset = loadedAsset;
                }

                status = "MaleCNS v1.0 loaded";
                var asset = sharedAsset;
                return asset == null ? null : new LifBrain(asset);
            }
        }
    }
}
