#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Ads.Stub.View;
using Framework.Foundation.Ads.Stub.ViewModel;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Scenes;
using VContainer;

namespace Framework.Foundation.Ads.Stub
{
    /// <summary>
    /// Владелец попапа-заглушки. Scoped, потому что view живёт сценой: Singleton-хост
    /// после первой же смены сцены держал бы уничтоженный попап.
    /// </summary>
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.AdsStubPopupHost)]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.AdsStubPopupHost)]
    public partial class AdsStubPopupHost : LifecycleEntity, IAdsStubHost
    {
        [Inject] private readonly EditorAdsProvider _provider;

        [AutoPopup(AdsConstants.Prefabs.StubPopup)]
        private AdsStubPopupView _view;

        private AdsStubPopupViewModel _viewModel;

        public async UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct)
        {
            var completion = _viewModel.Prepare(format);

            // Подписка живёт ровно одну сессию: закрытие по фону приходит после анимации,
            // и «опоздавший» OnClosed не должен закрыть следующий показ.
            using (_view.SubscribeOnClosed(() => _viewModel.Complete(AdResult.Skipped)))
            {
                _view.Open();
                var result = await completion;

                // Close идемпотентен: если попап уже закрыт кликом по фону, это no-op.
                _view.Close();
                return result;
            }
        }

        protected override UniTask Init()
        {
            SetEnabled(true);
            _viewModel = new AdsStubPopupViewModel();
            _view.Bind(_viewModel);
            _provider.SetHost(this);
            SetActive();
            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            SetActive(false);
            _provider.ClearHost(this);
            _viewModel?.Dispose();
            _viewModel = null;
            base.Dispose();
        }
    }
}
#endif
