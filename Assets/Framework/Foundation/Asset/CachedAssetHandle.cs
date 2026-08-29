using System;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Framework.Foundation.Asset
{
    public class CachedAssetHandle
    {
        public AsyncOperationHandle Handle { get; }
        public UnityEngine.Object Asset { get; }
        public Type AssetType { get; }

        public CachedAssetHandle(AsyncOperationHandle handle, UnityEngine.Object asset, Type assetType)
        {
            Handle = handle;
            Asset = asset;
            AssetType = assetType;
        }
    }
}
