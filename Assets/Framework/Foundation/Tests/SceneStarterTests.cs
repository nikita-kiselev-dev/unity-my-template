using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Configs;
using Framework.Foundation.Initialization;
using NUnit.Framework;
using VContainer;

namespace Framework.Foundation.Tests
{
    public class SceneStarterTests
    {
        [Test]
        public void CreatePhases_RunsLoadInitPostInit_WithoutConfigPhase()
        {
            var phases = new SceneStarter().CreatePhases();

            Assert.AreEqual(3, phases.Count);
            Assert.AreEqual(nameof(SceneLoadPhase.Load), phases[0].Name);
            Assert.AreEqual(nameof(SceneLoadPhase.Init), phases[1].Name);
            Assert.AreEqual(nameof(SceneLoadPhase.PostInit), phases[2].Name);
            Assert.IsTrue(phases[0].RunInParallel);
            Assert.IsTrue(phases[1].RunInParallel);
            Assert.IsFalse(phases[2].RunInParallel);
        }

        [Test]
        public void ExecuteParallelPhase_StartsBases_AfterAllWrappersComplete()
        {
            var wrapperGate = new UniTaskCompletionSource();
            var firstBaseStarted = false;
            var secondBaseStarted = false;
            var first = new PhaseEntity(() =>
            {
                firstBaseStarted = true;
                return UniTask.CompletedTask;
            });
            var second = new PhaseEntity(() =>
            {
                secondBaseStarted = true;
                return UniTask.CompletedTask;
            });
            var firstWrapper = new PhaseEntity(() => wrapperGate.Task);
            var secondWrapper = new PhaseEntity(() => UniTask.CompletedTask);
            first.AddWrapper(firstWrapper);
            second.AddWrapper(secondWrapper);
            var phase = new LifecyclePhase("Test", (entity, _) => ((PhaseEntity)entity).Execute(), true);

            var task = SceneStarter.ExecuteParallelPhase(
                phase,
                new LifecycleEntity[] { first, second },
                CancellationToken.None);

            Assert.IsFalse(firstBaseStarted);
            Assert.IsFalse(secondBaseStarted);

            wrapperGate.TrySetResult();
            task.GetAwaiter().GetResult();

            Assert.IsTrue(firstBaseStarted);
            Assert.IsTrue(secondBaseStarted);
        }

        [Test]
        public void ExecuteParallelPhase_ReportsCompletionPerEntity_WhileOthersRun()
        {
            var gate = new UniTaskCompletionSource();
            var completedCount = 0;
            var blocked = new PhaseEntity(() => gate.Task);
            var instant = new PhaseEntity(() => UniTask.CompletedTask);
            var phase = new LifecyclePhase("Test", (entity, _) => ((PhaseEntity)entity).Execute(), true);

            var task = SceneStarter.ExecuteParallelPhase(
                phase,
                new LifecycleEntity[] { blocked, instant },
                CancellationToken.None,
                () => completedCount++);

            Assert.AreEqual(1, completedCount);

            gate.TrySetResult();
            task.GetAwaiter().GetResult();

            Assert.AreEqual(2, completedCount);
        }

        [Test]
        public void ExecuteParallelPhase_ReportsSkippedEntityCompleted_WhenDisabledByConfig()
        {
            var completedCount = 0;
            var executed = false;
            var disabled = new DisabledHostEntity(() =>
            {
                executed = true;
                return UniTask.CompletedTask;
            });
            disabled.Status.SetEnabled(false);
            var phase = new LifecyclePhase("Test", (entity, _) => ((DisabledHostEntity)entity).Execute(), true);

            SceneStarter.ExecuteParallelPhase(
                    phase,
                    new LifecycleEntity[] { disabled },
                    CancellationToken.None,
                    () => completedCount++)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(executed);
            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void ExecuteParallelPhase_SkipsEntityAndWrappers_WhenConditionRejected()
        {
            var completedCount = 0;
            var wrapperExecuted = false;
            var entityExecuted = false;
            var conditional = new ConditionalEntity(() =>
            {
                entityExecuted = true;
                return UniTask.CompletedTask;
            });
            conditional.AddWrapper(new PhaseEntity(() =>
            {
                wrapperExecuted = true;
                return UniTask.CompletedTask;
            }));
            LifecycleGate.Apply(conditional);
            var phase = new LifecyclePhase(
                "Test",
                (entity, _) => entity is ConditionalEntity conditionalEntity
                    ? conditionalEntity.Execute()
                    : ((PhaseEntity)entity).Execute(),
                true);

            SceneStarter.ExecuteParallelPhase(
                    phase,
                    new LifecycleEntity[] { conditional },
                    CancellationToken.None,
                    () => completedCount++)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(entityExecuted);
            Assert.IsFalse(wrapperExecuted);
            Assert.AreEqual(2, completedCount);
        }

        private sealed class PhaseEntity : LifecycleEntity
        {
            private readonly System.Func<UniTask> _execute;

            public PhaseEntity(System.Func<UniTask> execute)
            {
                _execute = execute;
            }

            public UniTask Execute() => _execute();
        }

        private sealed class DisabledHostEntity : LifecycleEntity
        {
            private readonly System.Func<UniTask> _execute;

            [Inject] private readonly TestConfig _config;

            public DisabledHostEntity(System.Func<UniTask> execute)
            {
                _execute = execute;
            }

            public UniTask Execute() => _execute();
        }

        private sealed class ConditionalEntity : LifecycleEntity, IConditionalEntity
        {
            private readonly System.Func<UniTask> _execute;

            public ConditionalEntity(System.Func<UniTask> execute)
            {
                _execute = execute;
            }

            public bool ShouldRun() => false;

            public UniTask Execute() => _execute();
        }

        private sealed class TestConfig : IConfig
        {
            public bool IsEnabled => false;
        }
    }
}
