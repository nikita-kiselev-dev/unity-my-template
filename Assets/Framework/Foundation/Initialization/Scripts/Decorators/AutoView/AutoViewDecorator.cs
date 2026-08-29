using Framework.Foundation.Asset;
using Framework.Foundation.UI.Views;
using VContainer;

namespace Framework.Foundation.Initialization.Decorators.AutoView
{
    [AutoRegistration]
    public class AutoViewDecorator : ILifecycleDecorator
    {
        [Inject] private readonly IViewFactory _viewFactory;
        [Inject] private readonly IViewRouter _viewRouter;
        [Inject] private readonly IAssetScopeFactory _assetScopeFactory;

        public bool IsDecoratable(LifecycleEntity lifecycleEntity)
        {
            return lifecycleEntity is IAutoViewHost;
        }

        public LifecycleEntity Decorate(LifecycleEntity lifecycleEntity)
        {
            var bindings = ((IAutoViewHost)lifecycleEntity).GetAutoViewBindings();
            return new AutoViewEntity(bindings, _viewFactory, _viewRouter, _assetScopeFactory);
        }
    }
}
