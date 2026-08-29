using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.UI.Views;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ViewWrapperTests
    {
        private List<ViewState> _stateChanges;
        // Снимок счётчиков аниматора в момент события — проверяет порядок «до/после анимации».
        private List<(ViewEvent viewEvent, int shows, int hides)> _events;
        private FakeViewAnimator _animator;
        private ViewWrapper _wrapper;

        [SetUp]
        public void Setup()
        {
            _stateChanges = new List<ViewState>();
            _events = new List<(ViewEvent, int, int)>();
            _animator = new FakeViewAnimator();
            // View в тестах не нужен: нотификации идут через колбэки.
            _wrapper = new ViewWrapper(
                "view",
                ViewKind.Popup,
                null,
                _animator,
                _stateChanges.Add,
                viewEvent => _events.Add((viewEvent, _animator.ShowCount, _animator.HideCount)));
        }

        [Test]
        public void Open_RaisesOpenBeforeAndOpenedAfterAnimation()
        {
            Run(_wrapper.Open(CancellationToken.None));

            CollectionAssert.AreEqual(
                new[] { (ViewEvent.Open, 0, 0), (ViewEvent.Opened, 1, 0) },
                _events);
            Assert.AreEqual(ViewState.Open, _wrapper.State);
        }

        [Test]
        public void Open_DoesNothing_WhenAlreadyOpen()
        {
            Run(_wrapper.Open(CancellationToken.None));
            Run(_wrapper.Open(CancellationToken.None));

            Assert.AreEqual(2, _events.Count);
            Assert.AreEqual(1, _animator.ShowCount);
        }

        [Test]
        public void OpenImmediate_RaisesBothEvents_WithoutAnimation()
        {
            _wrapper.OpenImmediate();

            CollectionAssert.AreEqual(
                new[] { (ViewEvent.Open, 0, 0), (ViewEvent.Opened, 0, 0) },
                _events);
            Assert.AreEqual(ViewState.Open, _wrapper.State);
        }

        [Test]
        public void Close_RaisesCloseBeforeAndClosedAfterAnimation()
        {
            Run(_wrapper.Open(CancellationToken.None));
            _events.Clear();

            Run(_wrapper.Close(CancellationToken.None));

            CollectionAssert.AreEqual(
                new[] { (ViewEvent.Close, 1, 0), (ViewEvent.Closed, 1, 1) },
                _events);
            Assert.AreEqual(ViewState.Closed, _wrapper.State);
        }

        [Test]
        public void Close_DoesNothing_WhenAlreadyClosed()
        {
            Run(_wrapper.Close(CancellationToken.None));

            Assert.IsEmpty(_events);
            Assert.AreEqual(0, _animator.HideCount);
        }

        [Test]
        public void CloseImmediate_RaisesBothEvents_WithoutAnimation()
        {
            Run(_wrapper.Open(CancellationToken.None));
            _events.Clear();

            _wrapper.CloseImmediate();

            CollectionAssert.AreEqual(
                new[] { (ViewEvent.Close, 1, 0), (ViewEvent.Closed, 1, 0) },
                _events);
            Assert.AreEqual(ViewState.Closed, _wrapper.State);
        }

        [Test]
        public void Suspend_RaisesNoEvents_AndHidesView()
        {
            Run(_wrapper.Open(CancellationToken.None));
            _events.Clear();

            Run(_wrapper.Suspend(CancellationToken.None));

            Assert.IsEmpty(_events);
            Assert.AreEqual(1, _animator.HideCount);
            Assert.AreEqual(ViewState.Suspended, _wrapper.State);
        }

        [Test]
        public void SuspendImmediate_RaisesNoEventsAndNoAnimation()
        {
            _wrapper.SuspendImmediate();

            Assert.IsEmpty(_events);
            Assert.AreEqual(0, _animator.HideCount);
            Assert.AreEqual(ViewState.Suspended, _wrapper.State);
        }

        [Test]
        public void Open_RaisesEventsAgain_AfterSuspend()
        {
            Run(_wrapper.Open(CancellationToken.None));
            Run(_wrapper.Suspend(CancellationToken.None));
            _events.Clear();

            Run(_wrapper.Open(CancellationToken.None));

            CollectionAssert.AreEqual(
                new[] { ViewEvent.Open, ViewEvent.Opened },
                _events.ConvertAll(entry => entry.viewEvent));
        }

        [Test]
        public void Transitions_NotifyStateChanges_WithoutDuplicates()
        {
            Run(_wrapper.Open(CancellationToken.None));
            Run(_wrapper.Suspend(CancellationToken.None));
            Run(_wrapper.Open(CancellationToken.None));
            Run(_wrapper.Close(CancellationToken.None));
            Run(_wrapper.Close(CancellationToken.None));

            CollectionAssert.AreEqual(
                new[] { ViewState.Open, ViewState.Suspended, ViewState.Open, ViewState.Closed },
                _stateChanges);
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();
    }
}
