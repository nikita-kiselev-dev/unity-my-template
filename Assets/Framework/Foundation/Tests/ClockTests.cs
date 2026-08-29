using System;
using System.Collections.Generic;
using System.Threading;
using Framework.Foundation.UnityLifecycle;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.Time;
using Framework.Foundation.Utilities;
using NUnit.Framework;
using R3;

namespace Framework.Foundation.Tests
{
    public class ClockTests
    {
        private static readonly DateTime ServerUtc = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

        private ReactiveSignalBus _signalBus;
        private FakeServerTimeSource _serverTimeSource;
        private FakeRealtimeSource _realtime;
        private FakeTimeProvider _timeProvider;
        private FakeLogChannelFactory _logFactory;
        private Clock _controller;

        [SetUp]
        public void Setup()
        {
            _signalBus = new ReactiveSignalBus();
            _serverTimeSource = new FakeServerTimeSource { NextResult = Result<DateTime>.Success(ServerUtc) };
            _realtime = new FakeRealtimeSource();
            _timeProvider = new FakeTimeProvider();
            _logFactory = new FakeLogChannelFactory();
            _controller = new Clock(_serverTimeSource, _realtime, _timeProvider, _signalBus, _logFactory);
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _signalBus.Dispose();
        }

        private void WarmUp() => _controller.WarmUp(CancellationToken.None).GetAwaiter().GetResult();

        private void AdvanceBoth(TimeSpan delta)
        {
            _realtime.Advance(delta);
            _timeProvider.Advance(delta);
        }

        [Test]
        public void ServerUtcNow_UsesServerTime_WhenSourceSucceeds()
        {
            WarmUp();

            Assert.AreEqual(ServerUtc, _controller.ServerUtcNow);
            Assert.AreEqual(ClockTrust.ServerVerified, _controller.Trust);
        }

        [Test]
        public void Trust_IsLocalFallback_BeforeWarmUp()
        {
            Assert.AreEqual(ClockTrust.LocalFallback, _controller.Trust);
            Assert.AreEqual(0, _serverTimeSource.FetchCount);
        }

        [Test]
        public void WarmUp_FallsBackToLocalTime_WhenSourceFails()
        {
            _serverTimeSource.NextResult = Result<DateTime>.Failure();

            WarmUp();

            Assert.AreEqual(ClockTrust.LocalFallback, _controller.Trust);
            Assert.That(_controller.ServerUtcNow, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, _logFactory.Logger.Messages.Count);
        }

        [Test]
        public void ServerUtcNow_Advances_WithRealtimeSource()
        {
            WarmUp();

            _realtime.Advance(TimeSpan.FromMinutes(3));

            Assert.AreEqual(ServerUtc + TimeSpan.FromMinutes(3), _controller.ServerUtcNow);
        }

        [Test]
        public void ServerUtcNow_DoesNotAdvance_WhenRealtimeSourceIsFrozen()
        {
            WarmUp();

            _timeProvider.Advance(TimeSpan.FromMinutes(3));

            Assert.AreEqual(ServerUtc, _controller.ServerUtcNow);
        }

        [Test]
        public void WarmUp_FetchesOnce_WhenCalledTwice()
        {
            WarmUp();
            WarmUp();

            Assert.AreEqual(1, _serverTimeSource.FetchCount);
        }

        [Test]
        public void WarmUp_FetchesAgain_WhenPreviousAttemptWasCancelled()
        {
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(
                () => _controller.WarmUp(cancelled.Token).GetAwaiter().GetResult());

            WarmUp();

            Assert.AreEqual(2, _serverTimeSource.FetchCount);
            Assert.AreEqual(ClockTrust.ServerVerified, _controller.Trust);
        }

        [Test]
        public void Resync_UpdatesAnchor_OnApplicationResume()
        {
            WarmUp();
            _realtime.Advance(TimeSpan.FromSeconds(10));
            var resumedServerUtc = ServerUtc + TimeSpan.FromHours(2);
            _serverTimeSource.NextResult = Result<DateTime>.Success(resumedServerUtc);

            _signalBus.Trigger(new ApplicationPauseChangedSignal(false));

            Assert.AreEqual(resumedServerUtc, _controller.ServerUtcNow);
            Assert.AreEqual(2, _serverTimeSource.FetchCount);
        }

        [Test]
        public void Resync_DoesNotFetch_WhenApplicationGoesToBackground()
        {
            WarmUp();

            _signalBus.Trigger(new ApplicationPauseChangedSignal(true));

            Assert.AreEqual(1, _serverTimeSource.FetchCount);
        }

        [Test]
        public void Resync_DoesNotFetch_BeforeWarmUp()
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(false));

            Assert.AreEqual(0, _serverTimeSource.FetchCount);
        }

        [Test]
        public void ServerNow_Ticks_EverySecond()
        {
            WarmUp();

            Assert.AreEqual(ServerUtc, _controller.ServerNow.CurrentValue);

            AdvanceBoth(Tick);

            Assert.AreEqual(ServerUtc + Tick, _controller.ServerNow.CurrentValue);
        }

        [Test]
        public void Countdown_EmitsDecreasingValues_AndCompletesWithZero()
        {
            WarmUp();
            var values = new List<TimeSpan>();
            var isCompleted = false;

            _controller.Countdown(ServerUtc + TimeSpan.FromSeconds(3))
                .Subscribe(remaining => values.Add(remaining), _ => isCompleted = true);

            AdvanceBoth(Tick);
            AdvanceBoth(Tick);
            AdvanceBoth(Tick);

            Assert.AreEqual(
                new[]
                {
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.Zero
                },
                values);
            Assert.IsTrue(isCompleted);
        }

        [Test]
        public void Countdown_EmitsZeroOnly_WhenDeadlineAlreadyPassed()
        {
            WarmUp();
            var values = new List<TimeSpan>();

            _controller.Countdown(ServerUtc - TimeSpan.FromHours(1))
                .Subscribe(remaining => values.Add(remaining));

            Assert.AreEqual(new[] { TimeSpan.Zero }, values);
        }

        [Test]
        public void ServerLocalNow_AppliesDeviceOffset_ToServerTime()
        {
            WarmUp();

            var expected = ServerUtc + TimeZoneInfo.Local.GetUtcOffset(ServerUtc);

            Assert.AreEqual(expected, _controller.ServerLocalNow);
        }

        [Test]
        public void Dispose_StopsTicking()
        {
            WarmUp();

            _controller.Dispose();
            AdvanceBoth(Tick);

            Assert.AreEqual(ServerUtc, _controller.ServerNow.CurrentValue);
        }

        [Test]
        public void ServerUtcNow_UsesDeviceTime_BeforeWarmUp()
        {
            Assert.That(_controller.ServerUtcNow, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            Assert.That(_controller.ServerNow.CurrentValue, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void WarmUp_ReanchorsToDeviceTime_WhenSourceFailsAfterRealtimeAdvanced()
        {
            _serverTimeSource.NextResult = Result<DateTime>.Failure();
            _realtime.Advance(TimeSpan.FromHours(1));

            WarmUp();

            Assert.That(_controller.ServerUtcNow, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void Dispose_CompletesServerNow()
        {
            WarmUp();
            var isCompleted = false;
            using var subscription = _controller.ServerNow.Subscribe(_ => { }, _ => isCompleted = true);

            _controller.Dispose();

            Assert.IsTrue(isCompleted);
        }
    }
}
