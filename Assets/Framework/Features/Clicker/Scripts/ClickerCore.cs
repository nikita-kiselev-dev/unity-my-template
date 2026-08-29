using Cysharp.Threading.Tasks;
using Framework.Features.Clicker.Data;
using Framework.Features.Clicker.Configs;
using Framework.Features.Clicker.Model;
using Framework.Features.Clicker.View;
using Framework.Features.Clicker.ViewModel;
using Framework.Foundation.Analytics;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Features.Items;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using VContainer;

namespace Framework.Features.Clicker
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.Clicker)]
    [AutoLogger(ClickerConstants.LogName, LogCategory.Feature, StatusLogs = true)]
    public partial class ClickerCore : LifecycleEntity, IClickerCore
    {
        [Inject] private readonly ClickerData _data;
        [Inject] private readonly IAnalyticsController _analyticsController;
        [Inject] private readonly IInventory _inventory;
        [Inject] private readonly ClickerConfig _config;

        private ClickerModel _model;
        private IClickerAnalytics _analytics;

        [AutoWindow(ClickerConstants.Prefabs.Window)]
        private ClickerWindowView _windowView;
        private ClickerViewModel _viewModel;

        protected override UniTask Init()
        {
            CreateModel();
            CreateAnalytics();
            BindViewModel();
            SetActive();
            return UniTask.CompletedTask;
        }

        private void CreateModel()
        {
            _model = new ClickerModel(_config, _data, _inventory, Logger);
        }

        private void CreateAnalytics()
        {
            _analytics = new ClickerAnalytics(_analyticsController);
        }

        private void BindViewModel()
        {
            _viewModel = new ClickerViewModel(_model, _analytics);
            _windowView.Bind(_viewModel);
        }

        public override void Dispose()
        {
            // VM владеет моделью; отсутствует, если фича была выключена.
            _viewModel?.Dispose();
            base.Dispose();
        }
    }
}
