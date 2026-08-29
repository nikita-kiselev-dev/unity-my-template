using Cysharp.Threading.Tasks;
using Framework.Features.Settings;
using Framework.Features.MainMenu.View;
using Framework.Features.MainMenu.ViewModel;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using Framework.Foundation.Scenes.StateMachine;
using Framework.Foundation.Utilities;
using VContainer;

namespace Framework.Features.MainMenu
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.MainMenu)]
    [AutoLogger(MainMenuConstants.LogName, LogCategory.Feature, StatusLogs = true)]
    public partial class MainMenuCore : LifecycleEntity
    {
        [Inject] private readonly ISceneStateMachine _sceneStateMachine;
        [Inject] private readonly ISettingsCore _settingsCore;
        [Inject] private readonly IExternalLinkOpener _externalLinkOpener;

        [AutoWindow(MainMenuConstants.Prefabs.Window)]
        private MainMenuWindowView _view;

        private MainMenuViewModel _viewModel;

        protected override UniTask Init()
        {
            // Онбординг не реализован — считаем завершённым, пока не подключён реальный статус.
            var isOnboardingCompleted = true;

            _viewModel = new MainMenuViewModel(
                _sceneStateMachine,
                _settingsCore,
                _externalLinkOpener,
                isOnboardingCompleted);
            
            _view.Bind(_viewModel);
            SetEnabled(true);
            SetActive();
            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            // VM отсутствует, если scope умер до фазы Init.
            _viewModel?.Dispose();
            base.Dispose();
        }
    }
}
