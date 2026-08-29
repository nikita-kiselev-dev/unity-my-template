using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LifecycleEntityTests
    {
        [Test]
        public void InitPhase_SetsInited_WhenInitCompleted()
        {
            var entity = new InitEntity(_ => UniTask.CompletedTask);

            entity.InitPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(entity.Status.IsInited);
        }

        [Test]
        public void InitPhase_SetsInited_AfterInitCompleted()
        {
            var gate = new UniTaskCompletionSource();
            var entity = new InitEntity(_ => gate.Task);

            var task = entity.InitPhase(CancellationToken.None);

            Assert.IsFalse(entity.Status.IsInited);

            gate.TrySetResult();
            task.GetAwaiter().GetResult();

            Assert.IsTrue(entity.Status.IsInited);
        }

        [Test]
        public void InitPhase_LeavesInitedFalse_WhenInitThrows()
        {
            var entity = new InitEntity(_ => throw new InvalidOperationException("init failed"));

            Assert.Throws<InvalidOperationException>(
                () => entity.InitPhase(CancellationToken.None).GetAwaiter().GetResult());
            Assert.IsFalse(entity.Status.IsInited);
        }

        [Test]
        public void InitPhase_KeepsInitedFalse_WhenInitDeclinedExplicitly()
        {
            var entity = new InitEntity(e =>
            {
                e.Decline();
                return UniTask.CompletedTask;
            });

            entity.InitPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(entity.Status.IsInited);
        }

        [Test]
        public void LoadPhase_LeavesInitedFalse_WhenCompleted()
        {
            var entity = new InitEntity(_ => UniTask.CompletedTask);

            entity.LoadPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(entity.Status.IsInited);
        }

        private sealed class InitEntity : LifecycleEntity
        {
            private readonly Func<InitEntity, UniTask> _init;

            public InitEntity(Func<InitEntity, UniTask> init)
            {
                _init = init;
            }

            public void Decline() => SetInited(false);

            protected override UniTask Init() => _init(this);
        }
    }
}
