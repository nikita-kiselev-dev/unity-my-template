using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.LiveOps;
using Framework.Foundation.Logger;
using Framework.Foundation.UnityLifecycle;
using Framework.Foundation.Signals;
using R3;
using VContainer;

namespace Framework.Foundation.Time
{
    [AutoRegistration(Lifetime.Singleton)]
    public sealed class Clock : IClock, IDisposable
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

        private readonly IServerTimeSource _serverTimeSource;
        private readonly IRealtimeSource _realtime;
        private readonly ISignalBus _signalBus;
        private readonly ILogChannel _logger;
        private readonly ReactiveProperty<DateTime> _serverNow = new();

        private DisposableBag _subscriptions;
        private DateTime _anchorUtc;
        private TimeSpan _anchorElapsed;
        private bool _isWarmedUp;

        public DateTime ServerUtcNow => _anchorUtc + (_realtime.Elapsed - _anchorElapsed);
        public DateTime ServerLocalNow => ToDeviceTimeZone(ServerUtcNow);
        public ClockTrust Trust { get; private set; }
        public ReadOnlyReactiveProperty<DateTime> ServerNow => _serverNow;

        public Clock(
            IServerTimeSource serverTimeSource,
            IRealtimeSource realtime,
            TimeProvider timeProvider,
            ISignalBus signalBus,
            ILogChannelFactory logChannelFactory)
        {
            _serverTimeSource = serverTimeSource;
            _realtime = realtime;
            _signalBus = signalBus;
            _logger = logChannelFactory.Get(nameof(Clock));

            // Часы работают с первой же секунды процесса: до WarmUp они идут от локального
            // времени, а Trust честно говорит, что доверия нет. Исключение в геттере времени
            // означало бы краш там, где механике достаточно знать про недоверие.
            SetAnchor(DateTime.UtcNow, ClockTrust.LocalFallback);

            Observable
                .Interval(TickInterval, timeProvider)
                .Subscribe(_ => _serverNow.Value = ServerUtcNow)
                .AddTo(ref _subscriptions);

            _signalBus.Subscribe<ApplicationPauseChangedSignal>(OnApplicationPause).AddTo(ref _subscriptions);
        }

        public async UniTask WarmUp(CancellationToken ct)
        {
            // SceneStarter зовёт WarmUp на каждой сцене — синхронизация нужна один раз за процесс.
            if (_isWarmedUp)
            {
                return;
            }

            // Флаг только после успешного прохода: отмена (teardown scope) не должна навсегда
            // оставить часы на LocalFallback.
            await Synchronize(ct);
            _isWarmedUp = true;
        }

        public Observable<TimeSpan> Countdown(DateTime deadlineUtc)
        {
            return _serverNow
                .Select(now => Remaining(deadlineUtc, now))
                .TakeWhile(remaining => remaining > TimeSpan.Zero)
                .Append(TimeSpan.Zero);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _serverNow.Dispose();
        }

        private async UniTask Synchronize(CancellationToken ct)
        {
            var result = await _serverTimeSource.TryFetchUtc(ct);

            if (result.TryGet(out var serverUtc))
            {
                SetAnchor(serverUtc, ClockTrust.ServerVerified);
                return;
            }

            SetAnchor(DateTime.UtcNow, ClockTrust.LocalFallback);
            _logger.Log("Server time is unavailable, clock falls back to device time.");
        }

        // Уход в background может заморозить процесс: монотонный тик отстаёт от реального времени.
        private void OnApplicationPause(ApplicationPauseChangedSignal signal)
        {
            if (signal.IsPaused || !_isWarmedUp)
            {
                return;
            }

            Synchronize(CancellationToken.None).Forget();
        }

        private void SetAnchor(DateTime utc, ClockTrust trust)
        {
            _anchorUtc = utc;
            _anchorElapsed = _realtime.Elapsed;
            Trust = trust;
            _serverNow.Value = ServerUtcNow;
        }

        private static DateTime ToDeviceTimeZone(DateTime utc) => utc + TimeZoneInfo.Local.GetUtcOffset(utc);

        private static TimeSpan Remaining(DateTime deadlineUtc, DateTime now)
        {
            var remaining = deadlineUtc - now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
