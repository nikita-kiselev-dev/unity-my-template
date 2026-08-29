using System;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Signals;
using Framework.Foundation.Scenes;
using Framework.Foundation.Signals;
using R3;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Framework.Foundation.Initialization
{
    public class SceneLoadingProgressReporter : IDisposable
    {
        private static readonly int _phaseCount = Enum.GetValues(typeof(SceneLoadPhase)).Length;

        private readonly ISignalBus _signalBus;
        private readonly SceneLoadingProgress _progress;

        private readonly ReactiveProperty<string> _phase = new();
        private readonly ReactiveProperty<int> _completed = new();
        private readonly ReactiveProperty<int> _total = new();

        private DisposableBag _subscriptions;

        public SceneLoadingProgressReporter(ISignalBus signalBus)
        {
            _signalBus = signalBus;
            _progress = new SceneLoadingProgress();
        }

        public void Init(LifecycleEntity[] orderedControlEntities)
        {
            if (SceneManager.GetActiveScene().name == SceneConstants.Scenes.Bootstrap)
            {
                return;
            }

            Subscribe();

            var totalEntities = orderedControlEntities
                                    .AsValueEnumerable()
                                    .Count() +
                                orderedControlEntities
                                    .AsValueEnumerable()
                                    .SelectMany(entity => entity.Wrappers)
                                    .Count();

            _total.Value = totalEntities * _phaseCount;
        }

        public void SetPhase(string phaseName)
        {
            _phase.Value = phaseName;
        }

        public void ReportCompleted(int count = 1)
        {
            _completed.Value += count;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _phase.Dispose();
            _completed.Dispose();
            _total.Dispose();
        }

        private void Subscribe()
        {
            _phase.Skip(1).Subscribe(_ => ReportLoading()).AddTo(ref _subscriptions);
            _completed.Skip(1).Subscribe(_ => ReportLoading()).AddTo(ref _subscriptions);
        }

        private void ReportLoading()
        {
            _progress.SetPhase(_phase.Value);
            _progress.SetTotal(_total.Value);
            _progress.SetCompleted(_completed.Value);
            _signalBus.Trigger(new SceneLoadingProgressSignal(_progress));
        }
    }
}