using System.Collections.Generic;

namespace Framework.Foundation.Asset
{
    public interface IAssetProviderDiagnostics
    {
        AssetProviderSnapshot GetSnapshot();
    }

    public sealed class AssetProviderSnapshot
    {
        public AssetProviderSnapshot(
            IReadOnlyList<CachedAssetInfo> cachedAssets,
            IReadOnlyList<string> inflightKeys,
            IReadOnlyList<string> persistentKeys,
            IReadOnlyList<InstanceGroupInfo> instances)
        {
            CachedAssets = cachedAssets;
            InflightKeys = inflightKeys;
            PersistentKeys = persistentKeys;
            Instances = instances;
        }

        public IReadOnlyList<CachedAssetInfo> CachedAssets { get; }
        public IReadOnlyList<string> InflightKeys { get; }
        public IReadOnlyList<string> PersistentKeys { get; }
        public IReadOnlyList<InstanceGroupInfo> Instances { get; }
    }

    public readonly struct CachedAssetInfo
    {
        public CachedAssetInfo(string key, string assetType, bool persistent, int aliveInstances)
        {
            Key = key;
            AssetType = assetType;
            Persistent = persistent;
            AliveInstances = aliveInstances;
        }

        public string Key { get; }
        public string AssetType { get; }
        public bool Persistent { get; }
        public int AliveInstances { get; }
    }

    public readonly struct InstanceGroupInfo
    {
        public InstanceGroupInfo(string key, int aliveCount)
        {
            Key = key;
            AliveCount = aliveCount;
        }

        public string Key { get; }
        public int AliveCount { get; }
    }
}
