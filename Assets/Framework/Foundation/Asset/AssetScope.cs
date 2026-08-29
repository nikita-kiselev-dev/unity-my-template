using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Framework.Foundation.Asset
{
    // Владелец подмножества ключей: сам себя передаёт провайдеру владельцем, поэтому его
    // Dispose снимает только его владение и уничтожает только его инстансы. Ключ, который
    // держит кто-то ещё, переживает этот Dispose.
    internal sealed class AssetScope : IAssetScope
    {
        private readonly IAssetOwnerHost _host;
        private readonly HashSet<string> _keys = new();

        private bool _disposed;

        public AssetScope(IAssetOwnerHost host)
        {
            _host = host;
        }

        public UniTask<T> LoadAssetAsync<T>(
            string key,
            CancellationToken cancellationToken = default) where T : Object
        {
            Track(key);
            return _host.LoadAssetAsync<T>(key, this, cancellationToken);
        }

        public UniTask<T> LoadAssetAsync<T>(
            AssetReference reference,
            CancellationToken cancellationToken = default) where T : Object =>
            LoadAssetAsync<T>(ResolveKey(reference), cancellationToken);

        public UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            CancellationToken cancellationToken = default)
        {
            Track(key);
            return _host.InstantiateAsync<T>(key, this, parent, worldPositionStays, setActive, cancellationToken);
        }

        public UniTask<T> InstantiateAsync<T>(
            AssetReference reference,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            CancellationToken cancellationToken = default) =>
            InstantiateAsync<T>(ResolveKey(reference), parent, worldPositionStays, setActive, cancellationToken);

        public void ReleaseInstance(GameObject instance) => _host.ReleaseInstance(instance);

        public void ReleaseCompletely(string key)
        {
            _keys.Remove(key);
            _host.ReleaseCompletely(key, this);
        }

        // Вложенные scope-ы — независимые соседи поверх одного провайдера,
        // а не иерархия: dispose внешнего не трогает ключи вложенного.
        public IAssetScope CreateScope() => _host.CreateScope();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var key in _keys)
            {
                _host.ReleaseCompletely(key, this);
            }

            _keys.Clear();
        }

        private void Track(string key)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AssetScope));
            }

            _keys.Add(key);
        }

        private static string ResolveKey(AssetReference reference) => reference.RuntimeKey.ToString();
    }
}
