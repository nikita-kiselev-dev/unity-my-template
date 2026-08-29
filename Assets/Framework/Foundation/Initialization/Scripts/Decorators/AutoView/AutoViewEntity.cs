using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.UI.Views;
using UnityEngine;
using ZLinq;

namespace Framework.Foundation.Initialization.Decorators.AutoView
{
    public class AutoViewEntity : LifecycleEntity, IDisposableLifecycleWrapper
    {
        private readonly AutoViewBinding[] _bindings;
        private readonly IViewFactory _viewFactory;
        private readonly IViewRouter _viewRouter;
        private readonly IAssetScopeFactory _assetScopeFactory;

        private IAssetScope _assets;

        public AutoViewEntity(
            AutoViewBinding[] bindings,
            IViewFactory viewFactory,
            IViewRouter viewRouter,
            IAssetScopeFactory assetScopeFactory)
        {
            _bindings = bindings;
            _viewFactory = viewFactory;
            _viewRouter = viewRouter;
            _assetScopeFactory = assetScopeFactory;
        }

        // Ключи и инстансы освобождает scope: он знает, что загружено через него, а
        // ReleaseCompletely уничтожает инстансы ключа. Гейт мог пропустить фазы целиком —
        // тогда scope-а нет и релизить нечего.
        public override void Dispose()
        {
            _assets?.Dispose();
            _assets = null;
            base.Dispose();
        }

        protected override UniTask Load()
        {
            _assets = _assetScopeFactory.CreateScope();

            return UniTask.WhenAll(_bindings
                .AsValueEnumerable()
                .Select(binding => _assets.LoadAssetAsync<GameObject>(binding.ViewKey, CancellationToken))
                .ToArray());
        }

        protected override async UniTask Init()
        {
            var tasks = new UniTask[_bindings.Length];
            for (var i = 0; i < _bindings.Length; i++)
            {
                tasks[i] = CreateView(i);
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask CreateView(int index)
        {
            var binding = _bindings[index];
            var view = await _viewFactory.CreateView<MonoView>(binding.ViewKey, binding.ViewKind, _assets, CancellationToken);
            _viewRouter.Register(
                binding.ViewKey,
                view,
                binding.ViewKind,
                new ViewRegistration(enableOnStart: binding.ViewKind != ViewKind.Popup));
            binding.Assign(view);
        }
    }
}
