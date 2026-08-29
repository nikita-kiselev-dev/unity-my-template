using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    // До этого типа join-путь провайдера держал одну Preserve()-задачу на
    // всех, а UniTask допускает ровно один continuation на незавершённую задачу.
    public class InflightLoadsTests
    {
        private const string Key = "atlas";

        private InflightLoads<int> _loads;

        [SetUp]
        public void Setup()
        {
            _loads = new InflightLoads<int>();
        }

        [Test]
        public void Complete_ResumesEveryJoiner_WhenManyWaitConcurrently()
        {
            _loads.Begin(Key, typeof(int));
            var first = Await(_loads.Join(Key, typeof(int), CancellationToken.None));
            var second = Await(_loads.Join(Key, typeof(int), CancellationToken.None));
            var third = Await(_loads.Join(Key, typeof(int), CancellationToken.None));

            _loads.Complete(Key, 42);

            Assert.AreEqual(42, first.GetAwaiter().GetResult());
            Assert.AreEqual(42, second.GetAwaiter().GetResult());
            Assert.AreEqual(42, third.GetAwaiter().GetResult());
        }

        [Test]
        public void Fail_ThrowsInEveryJoiner()
        {
            _loads.Begin(Key, typeof(int));
            var first = Await(_loads.Join(Key, typeof(int), CancellationToken.None));
            var second = Await(_loads.Join(Key, typeof(int), CancellationToken.None));

            _loads.Fail(Key, new InvalidOperationException("boom"));

            // Сверяется сообщение, а не только тип: у незавершённой задачи GetResult тоже бросает
            // InvalidOperationException, и проверка по типу пропускала бы отсутствие фан-аута.
            Assert.AreEqual("boom", Assert.Throws<InvalidOperationException>(
                () => first.GetAwaiter().GetResult()).Message);
            Assert.AreEqual("boom", Assert.Throws<InvalidOperationException>(
                () => second.GetAwaiter().GetResult()).Message);
        }

        [Test]
        public void Join_ThrowsOnTypeMismatch_BeforeWaiting()
        {
            _loads.Begin(Key, typeof(int));

            Assert.Throws<InvalidOperationException>(
                () => _loads.Join(Key, typeof(string), CancellationToken.None));
        }

        [Test]
        public void Complete_ResumesRemainingJoiners_WhenOneWasCancelled()
        {
            _loads.Begin(Key, typeof(int));
            var cts = new CancellationTokenSource();
            var cancelled = Await(_loads.Join(Key, typeof(int), cts.Token));
            var alive = Await(_loads.Join(Key, typeof(int), CancellationToken.None));

            cts.Cancel();
            _loads.Complete(Key, 5);

            Assert.Throws<OperationCanceledException>(() => cancelled.GetAwaiter().GetResult());
            Assert.AreEqual(5, alive.GetAwaiter().GetResult());
        }

        [Test]
        public void IsInflight_IsFalse_AfterComplete()
        {
            _loads.Begin(Key, typeof(int));
            Assert.IsTrue(_loads.IsInflight(Key));

            _loads.Complete(Key, 1);

            Assert.IsFalse(_loads.IsInflight(Key));
        }

        private static UniTask<int> Await(UniTask<int> task) => Consume(task);

        private static async UniTask<int> Consume(UniTask<int> task) => await task;
    }
}
