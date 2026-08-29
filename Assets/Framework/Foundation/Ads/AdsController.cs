using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Ads.Data;
using Framework.Foundation.Ads.Configs;
using Framework.Foundation.Ads.Signals;
using Framework.Foundation.Audio;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using Framework.Foundation.Signals;
using Framework.Foundation.Time;
using R3;
using VContainer;

namespace Framework.Foundation.Ads
{
    [AutoRegistration(Lifetime.Singleton)]
    [LifecycleOrder(SceneConstants.Scenes.Bootstrap, (int)BootstrapSceneInitOrder.AdsController)]
    [AutoLogger(AdsConstants.LogName, LogCategory.System, StatusLogs = true)]
    public partial class AdsController : LifecycleEntity, IAdsController
    {
        [Inject] private readonly AdsConfig _config;
        [Inject] private readonly AdsData _data;
        [Inject] private readonly IAdsProvider _provider;
        [Inject] private readonly IClock _clock;
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly IAudioController _audioController;

        private readonly Dictionary<AdFormat, ReactiveProperty<bool>> _readiness = new()
        {
            [AdFormat.Banner] = new ReactiveProperty<bool>(),
            [AdFormat.Interstitial] = new ReactiveProperty<bool>(),
            [AdFormat.Rewarded] = new ReactiveProperty<bool>()
        };

        private readonly ReactiveProperty<bool> _isAdPlaying = new();

        private DisposableBag _subscriptions;

        // Политика создаётся в Init: у выключенной гейтом сущности фаз нет, и её отсутствие —
        // это и есть признак «реклама выключена», по которому все вызовы отдают NotReady.
        private AdsPolicy _policy;

        public ReadOnlyReactiveProperty<bool> IsAdPlaying => _isAdPlaying;
        public int InterstitialWatched => _data.InterstitialWatched;
        public int RewardedWatched => _data.RewardedWatched;

        public TimeSpan InterstitialCooldownLeft =>
            _policy?.GetCooldownLeft(AdFormat.Interstitial, _clock.ServerUtcNow) ?? TimeSpan.Zero;

        [Inject]
        public AdsController()
        {
        }

        // Тестовый шов: в проде поля заполняет VContainer, логгер приходит от [AutoLogger].
        internal AdsController(
            AdsConfig config,
            AdsData data,
            IAdsProvider provider,
            IClock clock,
            ISignalBus signalBus,
            IAudioController audioController,
            ILogChannel logger)
        {
            _config = config;
            _data = data;
            _provider = provider;
            _clock = clock;
            _signalBus = signalBus;
            _audioController = audioController;
            Logger = logger;
        }

        public ReadOnlyReactiveProperty<bool> IsReady(AdFormat format) => _readiness[format];

        public void SetFormatEnabled(AdFormat format, bool enabled)
        {
            if (_policy == null)
            {
                return;
            }

            _policy.SetFormatEnabled(format, enabled);

            if (format == AdFormat.Banner)
            {
                _provider.SetBannerVisible(_policy.IsFormatEnabled(AdFormat.Banner));
            }

            RefreshReadiness();
        }

        public async UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct = default)
        {
            if (!CanShow(format))
            {
                return AdResult.NotReady;
            }

            // Баннер — не сессия: он не забирает экран, не мьютит звук и не считается просмотром.
            if (format == AdFormat.Banner)
            {
                _provider.SetBannerVisible(true);
                return AdResult.Success;
            }

            return await ShowFullscreen(format, ct);
        }

        public void Show(AdFormat format, Action onSuccess = null, Action<AdResult> onFail = null)
        {
            ShowAndForget(format, onSuccess, onFail).Forget();
        }

        public Observable<TimeSpan> InterstitialCooldown()
        {
            return _policy == null
                ? Observable.Return(TimeSpan.Zero)
                : _clock.Countdown(_policy.InterstitialDeadlineUtc);
        }

        protected override async UniTask Init()
        {
            // Старт сессии — момент прохождения фазы на Bootstrap-сцене, то есть запуск процесса.
            _policy = new AdsPolicy(_config, _data, _clock.ServerUtcNow);

            await _provider.InitAsync(CancellationToken);

            // Единого события «готовность изменилась» у сетей нет, поэтому статус пересчитывается
            // по секундному тику: точности до секунды кнопке достаточно.
            _clock.ServerNow.Subscribe(_ => RefreshReadiness()).AddTo(ref _subscriptions);
            RefreshReadiness();
            SetActive();
        }

        public override void Dispose()
        {
            SetActive(false);
            _subscriptions.Dispose();

            foreach (var readiness in _readiness.Values)
            {
                readiness.Dispose();
            }

            _isAdPlaying.Dispose();
            base.Dispose();
        }

        private async UniTask<AdResult> ShowFullscreen(AdFormat format, CancellationToken ct)
        {
            var result = AdResult.Failed;

            _isAdPlaying.Value = true;
            _audioController.SetMuted(true);
            RefreshReadiness();
            _signalBus.Trigger(new AdStartedSignal(format));

            try
            {
                result = await _provider.ShowAsync(format, ct);
            }
            catch (Exception exception)
            {
                // Исход у показа ровно один: исключение сети — это Failed, а не проброс наружу,
                // иначе fire-and-forget Show() уронил бы вызывающий код.
                Logger.LogError($"Ad {format} failed: {exception}");
            }
            finally
            {
                _audioController.SetMuted(false);
                _isAdPlaying.Value = false;
                _policy.RegisterShown(format, result, _clock.ServerUtcNow);
                RefreshReadiness();
                _signalBus.Trigger(new AdFinishedSignal(format, result));
            }

            if (Logger.AreLogsEnabled)
            {
                Logger.Log($"Ad {format.ToString().SetFeatureColor()} finished: {result.ToString().SetFeatureColor()}");
            }

            return result;
        }

        private async UniTaskVoid ShowAndForget(AdFormat format, Action onSuccess, Action<AdResult> onFail)
        {
            // Не CancellationToken сущности: он принадлежит фазе Bootstrap-сцены, а показ
            // происходит сильно позже — по такому токену показ отменялся бы сразу.
            var result = await ShowAsync(format);

            if (result == AdResult.Success)
            {
                onSuccess?.Invoke();
                return;
            }

            onFail?.Invoke(result);
        }

        private bool CanShow(AdFormat format)
        {
            if (_policy == null || _isAdPlaying.Value)
            {
                return false;
            }

            return _policy.IsAllowed(format, _clock.ServerUtcNow) && _provider.IsReady(format);
        }

        private void RefreshReadiness()
        {
            foreach (var readiness in _readiness)
            {
                readiness.Value.Value = CanShow(readiness.Key);
            }
        }
    }
}
