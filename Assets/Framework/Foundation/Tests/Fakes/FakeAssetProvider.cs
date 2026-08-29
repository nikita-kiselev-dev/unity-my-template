using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeAssetProvider : IAssetProvider, IAssetOwnerHost
    {
        // Корневой владелец фейка: прямые вызовы IAssetProvider в настоящем провайдере тоже
        // принадлежат одному владельцу, а не «никому».
        private readonly object _rootOwner = new();

        public List<string> LoadedKeys { get; } = new();
        public List<string> PersistentKeys { get; } = new();
        public List<string> InstantiatedKeys { get; } = new();
        public List<string> ReleasedAssets { get; } = new();
        public List<string> ReleasedCompletely { get; } = new();

        public List<OwnedKey> LoadedByOwner { get; } = new();
        public List<OwnedKey> InstantiatedByOwner { get; } = new();
        public List<OwnedKey> ReleasedCompletelyByOwner { get; } = new();

        public UniTask<T> LoadAssetAsync<T>(
            string key,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : Object
        {
            if (persistent)
            {
                PersistentKeys.Add(key);
            }

            return LoadAssetAsync<T>(key, _rootOwner, cancellationToken);
        }

        public UniTask<T> LoadAssetAsync<T>(
            AssetReference reference,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : Object =>
            LoadAssetAsync<T>(reference.RuntimeKey.ToString(), persistent, cancellationToken);

        public UniTask<T> LoadAssetAsync<T>(
            string key,
            object owner,
            CancellationToken cancellationToken) where T : Object
        {
            LoadedKeys.Add(key);
            LoadedByOwner.Add(new OwnedKey(key, owner));
            return UniTask.FromResult<T>(null);
        }

        public UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default) =>
            InstantiateAsync<T>(key, _rootOwner, parent, worldPositionStays, setActive, cancellationToken);

        public UniTask<T> InstantiateAsync<T>(
            AssetReference reference,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default) =>
            InstantiateAsync<T>(reference.RuntimeKey.ToString(), parent, worldPositionStays, setActive, persistent, cancellationToken);

        public UniTask<T> InstantiateAsync<T>(
            string key,
            object owner,
            Transform parent,
            bool worldPositionStays,
            bool setActive,
            CancellationToken cancellationToken)
        {
            InstantiatedKeys.Add(key);
            InstantiatedByOwner.Add(new OwnedKey(key, owner));
            return UniTask.FromResult<T>(default);
        }

        public void ReleaseInstance(GameObject instance)
        {
        }

        public void ReleaseAsset(string key) => ReleasedAssets.Add(key);

        public void ReleaseAsset(AssetReference reference) => ReleaseAsset(reference.RuntimeKey.ToString());

        public void ReleaseCompletely(string key) => ReleaseCompletely(key, _rootOwner);

        public void ReleaseCompletely(string key, object owner)
        {
            ReleasedCompletely.Add(key);
            ReleasedCompletelyByOwner.Add(new OwnedKey(key, owner));
        }

        public IAssetScope CreateScope() => new AssetScope(this);

        public readonly struct OwnedKey
        {
            public OwnedKey(string key, object owner)
            {
                Key = key;
                Owner = owner;
            }

            public string Key { get; }
            public object Owner { get; }
        }
    }
}
