using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using UnityEngine.AddressableAssets;

namespace Framework.Foundation.UI.Views
{
    // owner обязателен и не имеет умолчания: инстанс view принадлежит тому же владельцу, что и
    // ключ префаба, иначе Dispose владельца освободил бы ключ, а окно осталось бы жить.
    public interface IViewFactory
    {
        public UniTask<T> CreateView<T>(string viewKey, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default);
        public UniTask<T> CreateView<T>(AssetReference reference, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default);
    }
}
