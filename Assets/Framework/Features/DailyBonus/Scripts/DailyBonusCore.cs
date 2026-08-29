using System;
using Cysharp.Threading.Tasks;
using Framework.Features.DailyBonus.Data;
using Framework.Features.DailyBonus.Configs;
using Framework.Features.DailyBonus.Factory;
using Framework.Features.DailyBonus.Model;
using Framework.Features.DailyBonus.View;
using Framework.Features.DailyBonus.ViewModel;
using Framework.Foundation.Analytics;
using Framework.Foundation.Asset;
using Framework.Foundation.Asset.Icons;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Features.Items;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using Framework.Foundation.Time;
using VContainer;

namespace Framework.Features.DailyBonus
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.DailyBonus)]
    [AutoLogger(DailyBonusConstants.LogName, LogCategory.Feature, StatusLogs = true)]
    public partial class DailyBonusCore : LifecycleEntity, IDailyBonusCore, IConditionalEntity
    {
        [Inject] private readonly DailyBonusData _data;
        [Inject] private readonly IAssetScopeFactory _assetScopeFactory;
        [Inject] private readonly IIconProvider _iconProvider;
        [Inject] private readonly IInventory _inventory;
        [Inject] private readonly IAnalyticsController _analyticsController;
        [Inject] private readonly IClock _clock;
        [Inject] private readonly DailyBonusConfig _config;

        private DailyBonusModel _model;
        private IDailyBonusAnalytics _analytics;
        private IAssetScope _assets;
        private DateTime _localNow;

        [AutoPopup(DailyBonusConstants.Prefabs.Popup)]
        private DailyBonusPopupView _popupView;
        private DailyBonusViewModel _popupViewModel;

        public bool ShouldRun()
        {
            CreateModel();
            CreateAnalytics();

            // Сброс дня — в местную полночь игрока, поэтому модель считает по времени устройства,
            // а не по UTC: иначе новый день наступал бы, например, в 07:00 локального времени.
            _localNow = _clock.ServerLocalNow;

            return NeedToShowPopup(_localNow);
        }

        protected override async UniTask Init()
        {
            await CreatePopup();
            _popupView.SubscribeOnClosed(Dispose);
            _popupView.Open();
            _analytics.LogPopupOpen(_model.StreakDay);
            GiveReward(_localNow);
            SetActive();
        }

        private void CreateModel()
        {
            _model = new DailyBonusModel(_config, _data, Logger);
        }

        private void CreateAnalytics()
        {
            _analytics = new DailyBonusAnalytics(_analyticsController);
        }

        private async UniTask CreatePopup()
        {
            var viewModelFactory = new DailyBonusDayViewModelFactory(_model, _popupView.RewardRowLayout, _iconProvider);
            var days = await viewModelFactory.Create();

            _popupViewModel = new DailyBonusViewModel(days);
            _popupView.Bind(_popupViewModel);

            _assets = _assetScopeFactory.CreateScope();
            var dayFactory = new DailyBonusDayViewSpawner(_assets);
            await dayFactory.CreateDayViews(days);
        }

        private bool NeedToShowPopup(DateTime localNow)
        {
            var decision = _model.Evaluate(localNow);
            var update = decision.StreakUpdate;

            if (update.StreakLost)
            {
                _analytics.LogStreakLose(update.LostStreakDay);
            }

            return decision.ShouldShowPopup;
        }

        private void GiveReward(DateTime localNow)
        {
            if (!_model.TryGetCurrentDayConfig(out var config))
            {
                return;
            }

            var itemOperation = new ItemOperation(config.ItemName, config.ItemCount);

            if (!_inventory.Add(itemOperation))
            {
                return;
            }

            _model.ClaimReward(localNow);
        }

        public override void Dispose()
        {
            SetActive(false);
            SetInited(false);
            _popupViewModel?.Dispose();
            _popupViewModel = null;
            _assets?.Dispose();
            _assets = null;
            base.Dispose();
        }
    }
}
