using Framework.Features.Settings;
using Framework.Foundation.Scenes.StateMachine;
using Framework.Foundation.Scenes.StateMachine.SceneStates;
using Framework.Foundation.Utilities;
using R3;

namespace Framework.Features.MainMenu.ViewModel
{
    public class MainMenuViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        public ReactiveCommand Play { get; } = new();
        public ReactiveCommand OpenSettings { get; } = new();
        public ReactiveCommand OpenWebSite { get; } = new();

        public MainMenuViewModel(
            ISceneStateMachine sceneStateMachine,
            ISettingsCore settingsCore,
            IExternalLinkOpener externalLinkOpener,
            bool isOnboardingCompleted)
        {
            Play.AddTo(ref Subscriptions);
            OpenSettings.AddTo(ref Subscriptions);
            OpenWebSite.AddTo(ref Subscriptions);

            // Переход из Start-сцены выполняется один раз — повторные клики по Play игнорируются.
            Play.Take(1).Subscribe(_ =>
            {
                if (isOnboardingCompleted)
                {
                    sceneStateMachine.EnterState<MetaSceneState>();
                }
                else
                {
                    sceneStateMachine.EnterState<CoreSceneState>();
                }
            }).AddTo(ref Subscriptions);

            OpenSettings.Subscribe(_ => settingsCore.OpenPopup()).AddTo(ref Subscriptions);
            OpenWebSite.Subscribe(_ => externalLinkOpener.OpenPrivacyPolicy()).AddTo(ref Subscriptions);
        }
    }
}
