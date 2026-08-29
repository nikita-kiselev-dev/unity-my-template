using Cysharp.Threading.Tasks;
using Framework.Features.Settings.Data;
using Framework.Features.Settings.Model;
using Framework.Features.Settings.View;
using Framework.Features.Settings.ViewModel;
using Framework.Foundation.Audio;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using VContainer;

namespace Framework.Features.Settings
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.SettingsPopup)]
    [AutoLogger(SettingsConstants.LogName, LogCategory.Feature, StatusLogs = true)]
    public partial class SettingsCore : LifecycleEntity, ISettingsCore
    {
        [Inject] private readonly SettingsData _data;
        [Inject] private readonly IAudioController _audioController;

        [AutoPopup(SettingsConstants.Prefabs.Popup)]
        private SettingsPopupView _view;

        private SettingsViewModel _viewModel;

        protected override UniTask Init()
        {
            SetEnabled(true);
            _viewModel = new SettingsViewModel(new SettingsModel(_data), _audioController);
            _view.Bind(_viewModel);
            SetActive();
            return UniTask.CompletedTask;
        }

        void ISettingsCore.OpenPopup()
        {
            _view.Open();
        }

        public override void Dispose()
        {
            // VM отсутствует, если scope умер до фазы Init.
            _viewModel?.Dispose();
            base.Dispose();
        }
    }
}
