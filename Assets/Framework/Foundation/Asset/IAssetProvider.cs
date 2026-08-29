using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Framework.Foundation.Asset
{
    // Полная поверхность загрузки — для Foundation. Фичи работают через IAssetScope,
    // который получают из IAssetScopeFactory.
    // Все вызовы здесь идут от одного корневого владельца: ReleaseAsset и ReleaseCompletely
    // отпускают ключ от его имени, а handle освобождается, только когда ключ не держит никто
    // ещё — ассет из-под чужого scope-а этот интерфейс выдернуть не может.
    public interface IAssetProvider : IAssetScopeFactory
    {
        UniTask<T> LoadAssetAsync<T>(
            string key,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : Object;

        UniTask<T> LoadAssetAsync<T>(
            AssetReference reference,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : Object;

        UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default);

        UniTask<T> InstantiateAsync<T>(
            AssetReference reference,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default);

        void ReleaseInstance(GameObject instance);
        void ReleaseAsset(string key);
        void ReleaseAsset(AssetReference reference);

        // Уничтожает инстансы корневого владельца, снимает его persistent-заявку и отпускает
        // ключ. Инстансы и заявки других владельцев не трогает.
        void ReleaseCompletely(string key);
    }
}