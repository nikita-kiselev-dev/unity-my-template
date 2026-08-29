using Framework.Foundation.Initialization;
using Framework.Foundation.UI.Views;
using Framework.Foundation.Utilities;
using VContainer;

namespace Framework.Features.Items.View
{
    [AutoRegistration]
    public class CurrencyViewSetupStep : IViewSetupStep
    {
        [Inject] private readonly IObjectResolver _objectResolver;

        public void Setup(MonoView view)
        {
            ChildComponentInjector.Inject<CurrencyViewHostAttribute, CurrencyView>(_objectResolver, view);
        }
    }
}
