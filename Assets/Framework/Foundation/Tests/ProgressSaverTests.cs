using System;
using Framework.Foundation.UnityLifecycle;
using Framework.Foundation.SaveLoad;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;
using VContainer.Unity;

namespace Framework.Foundation.Tests
{
    public class ProgressSaverTests
    {
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(15);

        private ReactiveSignalBus _signalBus;
        private FakeDataSaver _dataSaver;
        private FakeTimeProvider _timeProvider;
        private ProgressSaver _saver;

        [SetUp]
        public void Setup()
        {
            _signalBus = new ReactiveSignalBus();
            _dataSaver = new FakeDataSaver();
            _timeProvider = new FakeTimeProvider();
            _saver = new ProgressSaver(_signalBus, _dataSaver, _timeProvider);
            ((IStartable)_saver).Start();
        }

        [TearDown]
        public void TearDown()
        {
            ((IDisposable)_saver).Dispose();
            _signalBus.Dispose();
        }

        [Test]
        public void AutoSave_SavesEveryInterval_AfterStart()
        {
            _timeProvider.Advance(AutoSaveInterval);
            Assert.AreEqual(1, _dataSaver.SaveCount);

            _timeProvider.Advance(AutoSaveInterval);
            Assert.AreEqual(2, _dataSaver.SaveCount);
        }

        [Test]
        public void AutoSave_DoesNotSave_BeforeIntervalElapsed()
        {
            _timeProvider.Advance(AutoSaveInterval - TimeSpan.FromSeconds(1));

            Assert.AreEqual(0, _dataSaver.SaveCount);
        }

        [Test]
        public void AutoSave_Starts_WhenAwakeSignalWasMissed()
        {
            _timeProvider.Advance(AutoSaveInterval);

            Assert.AreEqual(1, _dataSaver.SaveCount);
        }

        [Test]
        public void Pause_SavesImmediately_NotAsynchronously()
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(true));

            Assert.AreEqual(1, _dataSaver.ImmediateSaveCount);
            Assert.AreEqual(0, _dataSaver.SaveCount);
        }

        [Test]
        public void Pause_DoesNotSave_WhenUnpaused()
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(false));

            Assert.AreEqual(0, _dataSaver.SaveCount);
            Assert.AreEqual(0, _dataSaver.ImmediateSaveCount);
        }

        [Test]
        public void Pause_SavesImmediately_OnEveryPause()
        {
            _timeProvider.Advance(AutoSaveInterval);

            _signalBus.Trigger(new ApplicationPauseChangedSignal(true));
            _signalBus.Trigger(new ApplicationPauseChangedSignal(true));

            Assert.AreEqual(2, _dataSaver.ImmediateSaveCount);
        }

        [Test]
        public void Pause_DoesNotDelayAutoSave()
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(true));

            _timeProvider.Advance(AutoSaveInterval);

            Assert.AreEqual(1, _dataSaver.SaveCount);
        }

        [Test]
        public void Quit_SavesImmediately_AfterAutoSave()
        {
            _timeProvider.Advance(AutoSaveInterval);

            _signalBus.Trigger<ApplicationQuittingSignal>();

            Assert.AreEqual(1, _dataSaver.SaveCount);
            Assert.AreEqual(1, _dataSaver.ImmediateSaveCount);
        }

        [Test]
        public void SceneChange_SavesUnconditionally()
        {
            _timeProvider.Advance(AutoSaveInterval);

            _signalBus.Trigger<SceneChangedSignal>();

            Assert.AreEqual(2, _dataSaver.SaveCount);
        }

        [Test]
        public void Dispose_StopsAutoSave()
        {
            ((IDisposable)_saver).Dispose();

            _timeProvider.Advance(AutoSaveInterval);

            Assert.AreEqual(0, _dataSaver.SaveCount);
        }
    }
}
