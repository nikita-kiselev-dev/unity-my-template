using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Framework.Foundation.Asset
{
    // Грань провайдера с явным владельцем — её видит только AssetScope. Публичный IAssetProvider
    // делает то же самое от имени корневого владельца, поэтому владелец не протекает в поверхность,
    // которую инжектит Foundation. Владелец сравнивается по ссылке: идентичность и есть контракт.
    internal interface IAssetOwnerHost
    {
        UniTask<T> LoadAssetAsync<T>(
            string key,
            object owner,
            CancellationToken cancellationToken) where T : Object;

        UniTask<T> InstantiateAsync<T>(
            string key,
            object owner,
            Transform parent,
            bool worldPositionStays,
            bool setActive,
            CancellationToken cancellationToken);

        void ReleaseInstance(GameObject instance);

        void ReleaseCompletely(string key, object owner);

        IAssetScope CreateScope();
    }
}
