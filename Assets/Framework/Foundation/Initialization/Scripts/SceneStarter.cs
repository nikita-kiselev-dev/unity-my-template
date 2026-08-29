using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Configs;
using Framework.Foundation.Initialization.Decorators;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Time;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.Initialization
{
    [AutoLogger(nameof(SceneStarter))]
    public partial class SceneStarter : IAsyncStartable
    {
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly IObjectResolver _objectResolver;
        [Inject] private readonly IConfigProvider _configProvider;
        [Inject] private readonly IClock _clock;
        [Inject] private readonly ILifecycleDecoratorPipeline _decorationController;

        private SceneLoadingProgressReporter _loadingProgressReporter;
        private LifecycleEntity[] _orderedControlEntities;
        private CancellationToken _cancellation;

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            _cancellation = cancellation;
            var sceneScope = SceneManager.GetActiveScene().name;

            try
            {
                // Резолв LifecycleEntity заполняет их [Inject]-поля, включая config, поэтому конфиги
                // должны быть в памяти раньше: WarmUp идемпотентен и реально грузит один раз.
                // Время синхронизируется здесь же, чтобы оно было готово до любой фазы любой
                // сцены — тогда порядок инициализации потребителей на корректность не влияет.
                await UniTask.WhenAll(
                    _configProvider.WarmUp(cancellation),
                    _clock.WarmUp(cancellation));

                var lifecycleEntities = _objectResolver.Resolve<IReadOnlyList<LifecycleEntity>>();
                _orderedControlEntities = LifecycleSceneSelector.SelectForScene(lifecycleEntities, sceneScope);

                if (_orderedControlEntities.Length > 0)
                {
                    ApplyGate();
                    TryDecorateEntities();
                    await StartExecution(sceneScope);
                }
                else
                {
                    Logger.Log($"{sceneScope} - no control entities on scene, skip execution.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // Без сигнала провал фазы оставляет шторку висеть молча; сам exception
                // дальше уходит в EntryPointExceptionHandler scope-а.
                _signalBus.Trigger(new SceneStartFailedSignal(sceneScope, e));
                throw;
            }

            _signalBus.Trigger(new SceneStartedSignal(sceneScope));
        }

        private void ApplyGate()
        {
            foreach (var lifecycleEntity in _orderedControlEntities)
            {
                LifecycleGate.Apply(lifecycleEntity, Logger);
            }
        }

        private void TryDecorateEntities()
        {
            _decorationController.TryDecorate(_orderedControlEntities);
        }

        private async UniTask StartExecution(string sceneName)
        {
            var phases = CreatePhases();
            Logger.Log($"{sceneName} - start executing phases.");
            _loadingProgressReporter = new SceneLoadingProgressReporter(_signalBus);
            _loadingProgressReporter.Init(_orderedControlEntities);

            var stopwatch = new Stopwatch();

            try
            {
                foreach (var phase in phases)
                {
                    Logger.Log($"{sceneName} - {phase.Name} phase execution.");
                    var timings = new LifecyclePhaseTimings();
                    stopwatch.Restart();
                    await ExecutePhase(phase, timings);
                    stopwatch.Stop();
                    Logger.Log($"{sceneName} - {phase.Name} phase completed in " +
                               $"{stopwatch.ElapsedMilliseconds}ms:\n{timings.Describe()}");
                }
            }
            finally
            {
                _loadingProgressReporter.Dispose();
                _loadingProgressReporter = null;
            }
        }

        internal List<LifecyclePhase> CreatePhases() => new()
        {
            new LifecyclePhase(nameof(SceneLoadPhase.Load),
                (entity, ct) => entity.LoadPhase(ct),
                runInParallel: true),

            new LifecyclePhase(nameof(SceneLoadPhase.Init),
                (entity, ct) => entity.InitPhase(ct),
                runInParallel: true),

            new LifecyclePhase(nameof(SceneLoadPhase.PostInit),
                (entity, ct) => entity.PostInitPhase(ct))
        };

        private async UniTask ExecutePhase(LifecyclePhase phase, LifecyclePhaseTimings timings)
        {
            _loadingProgressReporter.SetPhase(phase.Name);

            if (phase.RunInParallel)
            {
                await ExecuteParallelPhase(
                    phase,
                    _orderedControlEntities,
                    _cancellation,
                    () => _loadingProgressReporter.ReportCompleted(),
                    timings);
            }
            else
            {
                foreach (var lifecycleEntity in _orderedControlEntities)
                {
                    // PostInit — единственная последовательная фаза, всегда после Load и Init.
                    if (LifecycleGate.IsDisabled(lifecycleEntity))
                    {
                        _loadingProgressReporter.ReportCompleted(lifecycleEntity.Wrappers.Count + 1);
                        continue;
                    }

                    foreach (var wrapper in lifecycleEntity.Wrappers)
                    {
                        await ExecuteOnEntity(phase, wrapper, _cancellation, timings);
                        _loadingProgressReporter.ReportCompleted();
                    }

                    await ExecuteOnEntity(phase, lifecycleEntity, _cancellation, timings);
                    _loadingProgressReporter.ReportCompleted();
                }
            }
        }

        internal static async UniTask ExecuteParallelPhase(
            LifecyclePhase phase,
            IReadOnlyList<LifecycleEntity> entities,
            CancellationToken ct,
            Action onEntityCompleted = null,
            LifecyclePhaseTimings timings = null)
        {
            var wrapperTasks = new List<UniTask>();
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (LifecycleGate.IsDisabled(entity))
                {
                    // Пропущенная гейтом entity сразу засчитывается в прогресс вместе с wrapper-ами.
                    for (var j = 0; j < entity.Wrappers.Count + 1; j++)
                    {
                        onEntityCompleted?.Invoke();
                    }

                    continue;
                }

                for (var j = 0; j < entity.Wrappers.Count; j++)
                {
                    wrapperTasks.Add(ExecuteAndReport(phase, entity.Wrappers[j], ct, onEntityCompleted, timings));
                }
            }

            // Барьер несущий, а не оптимизация: он — единственное, что гарантирует
            // AutoViewEntity.Init -> binding.Assign(view) до Init хоста, то есть непустое
            // поле view у фичи. Инвариант закреплён тестом
            // SceneStarterTests.ExecuteParallelPhase_StartsBases_AfterAllWrappersComplete.
            await UniTask.WhenAll(wrapperTasks).AttachExternalCancellation(ct);

            var entityTasks = new List<UniTask>(entities.Count);
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!LifecycleGate.IsDisabled(entity))
                {
                    entityTasks.Add(ExecuteAndReport(phase, entity, ct, onEntityCompleted, timings));
                }
            }

            await UniTask.WhenAll(entityTasks).AttachExternalCancellation(ct);
        }

        private static async UniTask ExecuteAndReport(
            LifecyclePhase phase,
            LifecycleEntity entity,
            CancellationToken ct,
            Action onCompleted,
            LifecyclePhaseTimings timings)
        {
            await ExecuteOnEntity(phase, entity, ct, timings);
            onCompleted?.Invoke();
        }

        private static async UniTask ExecuteOnEntity(
            LifecyclePhase phase,
            LifecycleEntity entity,
            CancellationToken ct,
            LifecyclePhaseTimings timings = null)
        {
            var stopwatch = timings == null ? null : Stopwatch.StartNew();

            try
            {
                await phase.Function(entity, ct);
                timings?.Add(entity.GetType().Name, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new LifecyclePhaseException(phase.Name, entity.GetType(), e);
            }
        }

    }
}
