using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Framework.Foundation.Asset
{
    // Владение подмножеством ключей: всё, что загружено через scope, освобождает Dispose.
    // Поверхность узкая осознанно — ни persistent, ни ReleaseAsset здесь нет, иначе владелец
    // мог бы вывести ключ из-под собственного релиза.
    // Владелец распоряжается только своим: Dispose уничтожает инстансы, созданные через этот
    // scope, и снимает его владение ключом. Ассет освобождается, лишь когда ключ не держит
    // никто другой, поэтому две фичи могут грузить один префаб независимо.
    public interface IAssetScope : IAssetScopeFactory, IDisposable
    {
        UniTask<T> LoadAssetAsync<T>(
            string key,
            CancellationToken cancellationToken = default) where T : Object;

        UniTask<T> LoadAssetAsync<T>(
            AssetReference reference,
            CancellationToken cancellationToken = default) where T : Object;

        UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            CancellationToken cancellationToken = default);

        UniTask<T> InstantiateAsync<T>(
            AssetReference reference,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            CancellationToken cancellationToken = default);

        void ReleaseInstance(GameObject instance);

        // Досрочный релиз одного ключа этого владельца: то же, что сделал бы Dispose, но только
        // для него.
        void ReleaseCompletely(string key);
    }
}
