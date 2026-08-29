using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.UI.Views;
using UnityEngine.AddressableAssets;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeViewFactory : IViewFactory
    {
        public List<string> CreatedKeys { get; } = new();
        public List<IAssetScope> CreatedOwners { get; } = new();

        public UniTask<T> CreateView<T>(string viewKey, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default)
        {
            CreatedKeys.Add(viewKey);
            CreatedOwners.Add(owner);
            return UniTask.FromResult<T>(default);
        }

        public UniTask<T> CreateView<T>(AssetReference reference, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default) =>
            CreateView<T>(reference.RuntimeKey.ToString(), viewKind, owner, cancellationToken);
    }
}
