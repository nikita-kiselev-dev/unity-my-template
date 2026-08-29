using System;
using System.Collections.Generic;
using System.Threading;
using Framework.Foundation.Ads;
using Framework.Foundation.Ads.Data;
using Framework.Foundation.Ads.Configs;
using Framework.Foundation.Ads.Signals;
using Framework.Foundation.Initialization;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.Time;
using Framework.Foundation.Utilities;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class AdsControllerTests
    {
        private static readonly DateTime ServerUtc = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

        private ReactiveSignalBus _signalBus;
        private FakeRealtimeSource _realtime;
        private FakeTimeProvider _timeProvider;
        private FakeServerTimeSource _serverTimeSource;
        private FakeAdsProvider _provider;
        private FakeAudioController _audioController;
        private FakeLogChannel _logger;
        private Clock _clock;
        private AdsData _data;
        private AdsController _controller;

        [SetUp]
        public void Setup()
        {
            _signalBus = new ReactiveSignalBus();
            _realtime = new FakeRealtimeSource();
            _timeProvider = new FakeTimeProvider();
            _serverTimeSource = new FakeServerTimeSource { NextResult = Result<DateTime>.Success(ServerUtc) };
            _provider = new FakeAdsProvider();
            _audioController = new FakeAudioController();
            _logger = new FakeLogChannel();

            _clock = new Clock(
                _serverTimeSource, _realtime, _timeProvider, _signalBus, new FakeLogChannelFactory());
            _clock.WarmUp(CancellationToken.None).GetAwaiter().GetResult();

            _data = new AdsData();
            _data.PrepareNewData();
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            _clock.Dispose();
            _signalBus.Dispose();
        }

        private void CreateController(AdsConfig config = null)
        {
            _controller = new AdsController(
                config ?? FoundationTestConfigs.Ads(interstitialCooldownSeconds: (int)Cooldown.TotalSeconds),
                _data,
                _provider,
                _clock,
                _signalBus,
                _audioController,
                _logger);

            // Повторяет SceneStarter: выключенная гейтом сущность не проходит ни одной фазы.
            LifecycleGate.Apply(_controller);

            if (!LifecycleGate.IsDisabled(_controller))
            {
                _controller.InitPhase(CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private AdResult Show(AdFormat format) =>
            _controller.ShowAsync(format, CancellationToken.None).GetAwaiter().GetResult();

        private void AdvanceBoth(TimeSpan delta)
        {
            _realtime.Advance(delta);
            _timeProvider.Advance(delta);
        }

        [Test]
        public void Init_InitializesProvider()
        {
            CreateController();

            Assert.AreEqual(1, _provider.InitCount);
        }

        [Test]
        public void ShowAsync_ReturnsSuccess_WhenProviderSucceeds()
        {
            CreateController();

            Assert.AreEqual(AdResult.Success, Show(AdFormat.Interstitial));
            Assert.AreEqual(new[] { AdFormat.Interstitial }, _provider.ShowCalls.ToArray());
        }

        [Test]
        public void ShowAsync_ReturnsNotReady_WhenProviderIsNotReady()
        {
            _provider.ReadyFormats.Remove(AdFormat.Rewarded);
            CreateController();

            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Rewarded));
            Assert.IsEmpty(_provider.ShowCalls);
        }

        [Test]
        public void ShowAsync_ReturnsNotReady_WithoutCallingProvider_WhenCooldownIsActive()
        {
            CreateController();

            Show(AdFormat.Interstitial);

            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Interstitial));
            Assert.AreEqual(1, _provider.ShowCalls.Count);
        }

        [Test]
        public void ShowAsync_ReturnsNotReady_WhenAnotherAdIsPlaying()
        {
            _provider.ManualCompletion = true;
            CreateController();

            var pending = _controller.ShowAsync(AdFormat.Rewarded, CancellationToken.None);

            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Interstitial));

            _provider.CompletePending(AdResult.Success);

            Assert.AreEqual(AdResult.Success, pending.GetAwaiter().GetResult());
            Assert.AreEqual(1, _provider.ShowCalls.Count);
        }

        [Test]
        public void ShowAsync_RaisesAndReleasesIsAdPlaying()
        {
            _provider.ManualCompletion = true;
            CreateController();

            var pending = _controller.ShowAsync(AdFormat.Rewarded, CancellationToken.None);

            Assert.IsTrue(_controller.IsAdPlaying.CurrentValue);

            _provider.CompletePending(AdResult.Success);
            pending.GetAwaiter().GetResult();

            Assert.IsFalse(_controller.IsAdPlaying.CurrentValue);
        }

        [Test]
        public void ShowAsync_ReleasesIsAdPlaying_WhenProviderThrows()
        {
            _provider.NextException = new InvalidOperationException("sdk failed");
            CreateController();

            Assert.AreEqual(AdResult.Failed, Show(AdFormat.Rewarded));
            Assert.IsFalse(_controller.IsAdPlaying.CurrentValue);
            Assert.AreEqual(1, _logger.Errors.Count);
        }

        [Test]
        public void ShowAsync_MutesAudio_AroundAdSession()
        {
            CreateController();

            Show(AdFormat.Rewarded);

            Assert.AreEqual(new[] { true, false }, _audioController.MuteCalls.ToArray());
        }

        [Test]
        public void ShowAsync_DoesNotTouchAudio_WhenAdIsNotReady()
        {
            _provider.ReadyFormats.Remove(AdFormat.Rewarded);
            CreateController();

            Show(AdFormat.Rewarded);

            Assert.IsEmpty(_audioController.MuteCalls);
        }

        [Test]
        public void ShowAsync_TriggersStartedAndFinishedSignals()
        {
            CreateController();
            var started = new List<AdFormat>();
            var finished = new List<AdResult>();
            _signalBus.Subscribe<AdStartedSignal>(signal => started.Add(signal.Format));
            _signalBus.Subscribe<AdFinishedSignal>(signal => finished.Add(signal.Result));

            _provider.NextResult = AdResult.Skipped;
            Show(AdFormat.Rewarded);

            Assert.AreEqual(new[] { AdFormat.Rewarded }, started.ToArray());
            Assert.AreEqual(new[] { AdResult.Skipped }, finished.ToArray());
        }

        [Test]
        public void ShowAsync_DoesNotTriggerSignals_WhenAdIsNotReady()
        {
            _provider.ReadyFormats.Remove(AdFormat.Interstitial);
            CreateController();
            var started = 0;
            _signalBus.Subscribe<AdStartedSignal>(_ => started++);

            Show(AdFormat.Interstitial);

            Assert.AreEqual(0, started);
        }

        [Test]
        public void ShowAsync_IncrementsCounter_OnSuccessOnly()
        {
            CreateController();

            _provider.NextResult = AdResult.Skipped;
            Show(AdFormat.Rewarded);
            _provider.NextResult = AdResult.Success;
            Show(AdFormat.Rewarded);

            Assert.AreEqual(1, _controller.RewardedWatched);
            Assert.AreEqual(0, _controller.InterstitialWatched);
        }

        [Test]
        public void ShowAsync_ShowsBanner_WithoutAdSession()
        {
            CreateController();

            Assert.AreEqual(AdResult.Success, Show(AdFormat.Banner));
            Assert.AreEqual(true, _provider.BannerVisible);
            Assert.IsEmpty(_provider.ShowCalls);
            Assert.IsEmpty(_audioController.MuteCalls);
        }

        [Test]
        public void SetFormatEnabled_HidesBanner_WhenDisabled()
        {
            CreateController();
            Show(AdFormat.Banner);

            _controller.SetFormatEnabled(AdFormat.Banner, false);

            Assert.AreEqual(false, _provider.BannerVisible);
        }

        [Test]
        public void IsReady_ReflectsCooldown_OnServerTick()
        {
            CreateController();

            Assert.IsTrue(_controller.IsReady(AdFormat.Interstitial).CurrentValue);

            Show(AdFormat.Interstitial);

            Assert.IsFalse(_controller.IsReady(AdFormat.Interstitial).CurrentValue);

            AdvanceBoth(Cooldown);

            Assert.IsTrue(_controller.IsReady(AdFormat.Interstitial).CurrentValue);
        }

        /// Старт сессии фиксируется в Init: interstitial молчит первые секунды игры,
        /// rewarded этим кулдауном не ограничен.
        [Test]
        public void IsReady_RespectsSessionStartCooldown()
        {
            CreateController(FoundationTestConfigs.Ads(interstitialSessionStartCooldownSeconds: 30));

            Assert.IsFalse(_controller.IsReady(AdFormat.Interstitial).CurrentValue);
            Assert.IsTrue(_controller.IsReady(AdFormat.Rewarded).CurrentValue);
            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Interstitial));
            Assert.IsEmpty(_provider.ShowCalls);

            AdvanceBoth(TimeSpan.FromSeconds(30));

            Assert.IsTrue(_controller.IsReady(AdFormat.Interstitial).CurrentValue);
            Assert.AreEqual(AdResult.Success, Show(AdFormat.Interstitial));
        }

        [Test]
        public void InterstitialCooldownLeft_ReportsSessionStartWait_AndWaitAfterShow()
        {
            CreateController(FoundationTestConfigs.Ads(
                interstitialCooldownSeconds: 60,
                interstitialSessionStartCooldownSeconds: 30));

            Assert.AreEqual(TimeSpan.FromSeconds(30), _controller.InterstitialCooldownLeft);

            AdvanceBoth(TimeSpan.FromSeconds(30));
            Assert.AreEqual(TimeSpan.Zero, _controller.InterstitialCooldownLeft);

            Show(AdFormat.Interstitial);
            Assert.AreEqual(TimeSpan.FromSeconds(60), _controller.InterstitialCooldownLeft);
        }

        [Test]
        public void InterstitialCooldownLeft_IsZero_WhenFeatureIsDisabledByConfig()
        {
            CreateController(FoundationTestConfigs.Ads(isEnabled: false, interstitialSessionStartCooldownSeconds: 30));

            Assert.AreEqual(TimeSpan.Zero, _controller.InterstitialCooldownLeft);
        }

        [Test]
        public void IsReady_IsFalse_WhileAdIsPlaying()
        {
            _provider.ManualCompletion = true;
            CreateController();

            var pending = _controller.ShowAsync(AdFormat.Rewarded, CancellationToken.None);

            Assert.IsFalse(_controller.IsReady(AdFormat.Rewarded).CurrentValue);

            _provider.CompletePending(AdResult.Success);
            pending.GetAwaiter().GetResult();

            Assert.IsTrue(_controller.IsReady(AdFormat.Rewarded).CurrentValue);
        }

        [Test]
        public void IsReady_IsFalse_WhenFormatIsDisabledAtRuntime()
        {
            CreateController();

            _controller.SetFormatEnabled(AdFormat.Rewarded, false);

            Assert.IsFalse(_controller.IsReady(AdFormat.Rewarded).CurrentValue);
            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Rewarded));
        }

        /// Гейт выключает сущность целиком: фазы не выполняются, но объект инжектится в фичи —
        /// вызовы обязаны возвращать NotReady, а не падать.
        [Test]
        public void ShowAsync_ReturnsNotReady_WhenFeatureIsDisabledByConfig()
        {
            CreateController(FoundationTestConfigs.Ads(isEnabled: false));

            Assert.AreEqual(AdResult.NotReady, Show(AdFormat.Rewarded));
            Assert.IsFalse(_controller.IsReady(AdFormat.Rewarded).CurrentValue);
            Assert.IsEmpty(_provider.ShowCalls);
            Assert.AreEqual(0, _provider.InitCount);
        }

        [Test]
        public void Show_InvokesSuccessCallback_OnSuccess()
        {
            CreateController();
            var success = 0;
            var failures = new List<AdResult>();

            _controller.Show(AdFormat.Rewarded, () => success++, result => failures.Add(result));

            Assert.AreEqual(1, success);
            Assert.IsEmpty(failures);
        }

        [Test]
        public void Show_InvokesFailCallback_WithResult()
        {
            _provider.ReadyFormats.Remove(AdFormat.Rewarded);
            CreateController();
            var failures = new List<AdResult>();

            _controller.Show(AdFormat.Rewarded, null, result => failures.Add(result));

            Assert.AreEqual(new[] { AdResult.NotReady }, failures.ToArray());
        }
    }
}
