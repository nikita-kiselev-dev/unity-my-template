using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.UI.Views;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ViewOperationPumpTests
    {
        private readonly List<string> _opened = new();
        private readonly List<string> _batchOpened = new();
        private readonly List<string> _closed = new();
        private int _closeAllCount;
        private int _waitFrameCalls;

        private ViewOperationPump _pump;

        [SetUp]
        public void Setup()
        {
            _opened.Clear();
            _batchOpened.Clear();
            _closed.Clear();
            _closeAllCount = 0;
            _waitFrameCalls = 0;

            _pump = CreatePump(WaitFrame);
        }

        [Test]
        public void Start_WaitsOneFrame_BeforeDrain()
        {
            _pump.Enqueue(ViewOperation.Open(Popup("a")));
            _pump.Start();

            Assert.AreEqual(1, _waitFrameCalls);
            Assert.AreEqual(1, _batchOpened.Count);
            Assert.AreEqual("a", _batchOpened[0]);
        }

        [Test]
        public void Start_BatchesConsecutivePopupOpens_AsSingleBatch()
        {
            _pump.Enqueue(ViewOperation.Open(Popup("a")));
            _pump.Enqueue(ViewOperation.Open(Popup("b")));
            _pump.Enqueue(ViewOperation.Open(Popup("c")));
            _pump.Start();

            Assert.AreEqual(1, _waitFrameCalls);
            Assert.AreEqual(1, _batchOpened.Count);
            Assert.AreEqual("a,b,c", _batchOpened[0]);
            Assert.AreEqual(0, _opened.Count);
        }

        [Test]
        public void Start_DoesNotBatch_WhenWindowOpenBetweenPopups()
        {
            _pump.Enqueue(ViewOperation.Open(Popup("a")));
            _pump.Enqueue(ViewOperation.Open(Window("w")));
            _pump.Enqueue(ViewOperation.Open(Popup("b")));
            _pump.Start();

            Assert.AreEqual(2, _batchOpened.Count);
            Assert.AreEqual("a", _batchOpened[0]);
            Assert.AreEqual("b", _batchOpened[1]);
            Assert.AreEqual(1, _opened.Count);
            Assert.AreEqual("w", _opened[0]);
        }

        [Test]
        public void Start_PreservesFifo_AcrossMixedOperations()
        {
            _pump.Enqueue(ViewOperation.Open(Popup("a")));
            _pump.Enqueue(ViewOperation.Close(Popup("a")));
            _pump.Enqueue(ViewOperation.Open(Popup("b")));
            _pump.Enqueue(ViewOperation.CloseAll());
            _pump.Start();

            Assert.AreEqual("a", _batchOpened[0]);
            Assert.AreEqual("a", _closed[0]);
            Assert.AreEqual("b", _batchOpened[1]);
            Assert.AreEqual(1, _closeAllCount);
        }

        [Test]
        public void Enqueue_DoesNotPump_BeforeStart()
        {
            _pump.Enqueue(ViewOperation.Open(Popup("a")));

            Assert.AreEqual(0, _waitFrameCalls);
            Assert.AreEqual(0, _batchOpened.Count);

            _pump.Start();

            Assert.AreEqual(1, _batchOpened.Count);
        }

        [Test]
        public void Enqueue_IncludesOpsArrivingDuringCoalesceWait()
        {
            var delayGate = new UniTaskCompletionSource();
            _pump = CreatePump(_ => delayGate.Task);

            _pump.Enqueue(ViewOperation.Open(Popup("a")));
            _pump.Start();
            _pump.Enqueue(ViewOperation.Open(Popup("b")));

            Assert.AreEqual(0, _batchOpened.Count);

            delayGate.TrySetResult();

            Assert.AreEqual(1, _batchOpened.Count);
            Assert.AreEqual("a,b", _batchOpened[0]);
        }

        private ViewOperationPump CreatePump(System.Func<CancellationToken, UniTask> waitFrame) =>
            new(waitFrame, new RecordingExecutor(this), CancellationToken.None, new FakeLogChannel());

        private sealed class RecordingExecutor : IViewOperationExecutor
        {
            private readonly ViewOperationPumpTests _test;

            public RecordingExecutor(ViewOperationPumpTests test)
            {
                _test = test;
            }

            public UniTask OpenWindow(ViewWrapper window, CancellationToken ct)
            {
                _test._opened.Add(window.ViewKey);
                return UniTask.CompletedTask;
            }

            public UniTask OpenPopupBatch(IReadOnlyList<ViewWrapper> popups, CancellationToken ct)
            {
                _test._batchOpened.Add(string.Join(",", BatchKeys(popups)));
                return UniTask.CompletedTask;
            }

            public UniTask Close(ViewWrapper view, CancellationToken ct)
            {
                _test._closed.Add(view.ViewKey);
                return UniTask.CompletedTask;
            }

            public UniTask CloseAll(CancellationToken ct)
            {
                _test._closeAllCount++;
                return UniTask.CompletedTask;
            }
        }

        private UniTask WaitFrame(CancellationToken ct)
        {
            _waitFrameCalls++;
            return UniTask.CompletedTask;
        }

        private static ViewWrapper Popup(string key) =>
            new(key, ViewKind.Popup, null, new FakeViewAnimator());

        private static ViewWrapper Window(string key) =>
            new(key, ViewKind.Window, null, new FakeViewAnimator());

        private static IEnumerable<string> BatchKeys(IReadOnlyList<ViewWrapper> batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                yield return batch[i].ViewKey;
            }
        }
    }
}
