using Framework.Foundation.UI.Views;
using NUnit.Framework;
using R3;

namespace Framework.Foundation.Tests
{
    public class ViewStateNotifierTests
    {
        private ViewStateNotifier _notifier;

        [SetUp]
        public void Setup()
        {
            _notifier = new ViewStateNotifier();
        }

        [TearDown]
        public void TearDown()
        {
            _notifier.Dispose();
        }

        [Test]
        public void State_StartsClosed_OnCreation()
        {
            Assert.AreEqual(ViewState.Closed, _notifier.State.CurrentValue);
        }

        [Test]
        public void State_ReplaysCurrentValue_OnSubscribe()
        {
            _notifier.SetState(ViewState.Open);

            var observed = ViewState.Closed;
            using var subscription = _notifier.State.Subscribe(state => observed = state);

            Assert.AreEqual(ViewState.Open, observed);
        }

        [Test]
        public void RaiseEvent_RoutesEventToOwnStream_ForEachEvent()
        {
            var openCount = 0;
            var openedCount = 0;
            var closeCount = 0;
            var closedCount = 0;
            using var onOpen = _notifier.OnOpen.Subscribe(_ => openCount++);
            using var onOpened = _notifier.OnOpened.Subscribe(_ => openedCount++);
            using var onClose = _notifier.OnClose.Subscribe(_ => closeCount++);
            using var onClosed = _notifier.OnClosed.Subscribe(_ => closedCount++);

            _notifier.RaiseEvent(ViewEvent.Open);
            _notifier.RaiseEvent(ViewEvent.Opened);
            _notifier.RaiseEvent(ViewEvent.Close);
            _notifier.RaiseEvent(ViewEvent.Closed);

            Assert.AreEqual(1, openCount);
            Assert.AreEqual(1, openedCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, closedCount);
        }

        [Test]
        public void RaiseEvent_DoesNotLeakToOtherStreams_WhenSingleEventRaised()
        {
            var openCount = 0;
            var closedCount = 0;
            using var onOpen = _notifier.OnOpen.Subscribe(_ => openCount++);
            using var onClosed = _notifier.OnClosed.Subscribe(_ => closedCount++);

            _notifier.RaiseEvent(ViewEvent.Closed);

            Assert.AreEqual(0, openCount);
            Assert.AreEqual(1, closedCount);
        }

        [Test]
        public void RaiseEvent_DoesNotReplay_ForLateSubscriber()
        {
            _notifier.RaiseEvent(ViewEvent.Opened);

            var count = 0;
            using var subscription = _notifier.OnOpened.Subscribe(_ => count++);

            Assert.AreEqual(0, count);
        }
    }
}
