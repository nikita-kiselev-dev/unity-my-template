using Framework.Features.Clicker.Model;
using R3;

namespace Framework.Features.Clicker.ViewModel
{
    public class ClickerViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        private readonly ClickerModel _model;
        private readonly IClickerAnalytics _analytics;

        public ReactiveCommand Click { get; } = new();
        public ReactiveCommand Upgrade { get; } = new();
        public ReadOnlyReactiveProperty<bool> CanUpgrade => _model.CanUpgrade;

        public ClickerViewModel(ClickerModel model, IClickerAnalytics analytics)
        {
            _model = model;
            _analytics = analytics;

            _model.AddTo(ref Subscriptions);
            Click.AddTo(ref Subscriptions);
            Upgrade.AddTo(ref Subscriptions);
            Click.Subscribe(_ => _model.Click()).AddTo(ref Subscriptions);
            Upgrade.Subscribe(_ => OnUpgrade()).AddTo(ref Subscriptions);
        }

        private void OnUpgrade()
        {
            if (_model.TryUpgrade())
            {
                _analytics.LogUpgrade(_model.Level.CurrentValue + 1);
            }
        }
    }
}
