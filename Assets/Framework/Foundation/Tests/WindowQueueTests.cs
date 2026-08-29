using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.UI.Views;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class WindowQueueTests
    {
        private WindowQueue _queue;

        [SetUp]
        public void Setup()
        {
            _queue = new WindowQueue();
        }

        [Test]
        public void Open_ShowsWindow_WhenNoActiveWindow()
        {
            var (window, animator) = CreateWindow();

            Run(_queue.Open(window, CancellationToken.None));

            Assert.AreEqual(ViewState.Open, window.State);
            Assert.AreEqual(1, animator.ShowCount);
        }

        [Test]
        public void Open_QueuesWindow_WhenAnotherWindowActive()
        {
            var (first, _) = CreateWindow();
            var (second, secondAnimator) = CreateWindow();

            Run(_queue.Open(first, CancellationToken.None));
            Run(_queue.Open(second, CancellationToken.None));

            Assert.AreEqual(ViewState.Suspended, second.State);
            Assert.AreEqual(0, secondAnimator.ShowCount);
        }

        [Test]
        public void Open_Ignores_NonClosedWindow()
        {
            var (window, animator) = CreateWindow();

            Run(_queue.Open(window, CancellationToken.None));
            Run(_queue.Open(window, CancellationToken.None));

            Assert.AreEqual(1, animator.ShowCount);
        }

        [Test]
        public void Close_ShowsNextPendingWindow()
        {
            var (first, firstAnimator) = CreateWindow();
            var (second, secondAnimator) = CreateWindow();
            Run(_queue.Open(first, CancellationToken.None));
            Run(_queue.Open(second, CancellationToken.None));

            Run(_queue.Close(first, CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, first.State);
            Assert.AreEqual(1, firstAnimator.HideCount);
            Assert.AreEqual(ViewState.Open, second.State);
            Assert.AreEqual(1, secondAnimator.ShowCount);
        }

        [Test]
        public void Close_RemovesPendingWindow_WithoutAnimations()
        {
            var (first, firstAnimator) = CreateWindow();
            var closeCount = 0;
            var (second, secondAnimator) = CreateWindow(viewEvent =>
            {
                if (viewEvent == ViewEvent.Closed)
                {
                    closeCount++;
                }
            });
            Run(_queue.Open(first, CancellationToken.None));
            Run(_queue.Open(second, CancellationToken.None));

            Run(_queue.Close(second, CancellationToken.None));
            Run(_queue.Close(first, CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, second.State);
            Assert.AreEqual(0, secondAnimator.ShowCount);
            Assert.AreEqual(0, secondAnimator.HideCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, firstAnimator.HideCount);
        }

        [Test]
        public void CloseAll_ClosesCurrentAndPending()
        {
            var (first, firstAnimator) = CreateWindow();
            var (second, secondAnimator) = CreateWindow();
            Run(_queue.Open(first, CancellationToken.None));
            Run(_queue.Open(second, CancellationToken.None));

            Run(_queue.CloseAll(CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, first.State);
            Assert.AreEqual(ViewState.Closed, second.State);
            Assert.AreEqual(1, firstAnimator.HideCount);
            Assert.AreEqual(0, secondAnimator.HideCount);
        }

        private static (ViewWrapper window, FakeViewAnimator animator) CreateWindow(System.Action<ViewEvent> eventRaised = null)
        {
            var animator = new FakeViewAnimator();
            // View в тестах не нужен: Open/Close/CloseAll работают только с State и Animator.
            return (new ViewWrapper("window", ViewKind.Window, null, animator, eventRaised: eventRaised), animator);
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();
    }
}
