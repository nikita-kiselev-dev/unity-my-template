using System;
using Framework.Foundation.Initialization;
using Framework.Foundation.UnityLifecycle;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using R3;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.SaveLoad
{
    [AutoRegistration(Lifetime.Singleton)]
    public class ProgressSaver : IStartable, IDisposable
    {
        private const bool IsAutoSaveEnabled = true;
        private const int AutoSaveIntervalInSeconds = 15;

        private readonly ISignalBus _signalBus;
        private readonly IDataSaver _dataSaver;
        private readonly TimeProvider _timeProvider;

        private DisposableBag _subscriptions;

        public ProgressSaver(ISignalBus signalBus, IDataSaver dataSaver, TimeProvider timeProvider)
        {
            _signalBus = signalBus;
            _dataSaver = dataSaver;
            _timeProvider = timeProvider;
        }

        void IStartable.Start()
        {
            _signalBus.Subscribe<ApplicationPauseChangedSignal>(OnApplicationPause).AddTo(ref _subscriptions);
            _signalBus.Subscribe<ApplicationQuittingSignal>(OnApplicationQuit).AddTo(ref _subscriptions);
            _signalBus.Subscribe<SceneChangedSignal>(Save).AddTo(ref _subscriptions);
            StartAutoSave();
        }

        void IDisposable.Dispose()
        {
            _subscriptions.Dispose();
        }

        private void StartAutoSave()
        {
            if (!IsAutoSaveEnabled)
            {
                return;
            }

            Observable
                .Interval(TimeSpan.FromSeconds(AutoSaveIntervalInSeconds), _timeProvider)
                .Subscribe(_ => Save())
                .AddTo(ref _subscriptions);
        }

        // Уход в фон на мобильных — точка невозврата того же класса, что и quit: процесс может
        // умереть, не дождавшись async-записи, поэтому пишем синхронно.
        private void OnApplicationPause(ApplicationPauseChangedSignal signal)
        {
            if (signal.IsPaused)
            {
                _dataSaver.SaveDataImmediate();
            }
        }

        private void OnApplicationQuit()
        {
            _dataSaver.SaveDataImmediate();
        }

        private void Save()
        {
            _dataSaver.SaveData();
        }
    }
}
