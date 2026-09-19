namespace ShadowNineX.PersonConnectome.Core
{
    /// <summary>Creates brains from the single validated, shared graph asset.</summary>
    internal sealed partial class LifBrain
    {
        private static readonly object AssetLock = new();
        private static ModAsset? sharedAsset;

        /// <summary>Load while ModAPI has a Main/AfterSpawn context, before prefab Awake.</summary>
        public static bool PrepareAsset(out string status)
        {
            lock (AssetLock)
            {
                if (sharedAsset == null)
                {
                    if (!ModAsset.TryLoad(out var loadedAsset, out status) || loadedAsset == null)
                    {
                        return false;
                    }

                    sharedAsset = loadedAsset;
                }

                status = "MaleCNS v1.0 loaded";
                return sharedAsset != null;
            }
        }

        public static LifBrain? TryCreate(out string status)
        {
            if (!PrepareAsset(out status)) return null;
            lock (AssetLock)
            {
                var asset = sharedAsset;
                return asset == null ? null : new LifBrain(asset);
            }
        }
    }
}
