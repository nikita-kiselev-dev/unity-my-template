using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.UI.Views;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class PopupStackTests
    {
        private FakeViewAnimator _background;
        private PopupStack _stack;

        [SetUp]
        public void Setup()
        {
            _background = new FakeViewAnimator();
            _stack = new PopupStack(_background);
        }

        [Test]
        public void Open_ShowsPopupAndBackground_OnFirstPopup()
        {
            var (popup, animator) = CreatePopup();

            Run(_stack.Open(popup, CancellationToken.None));

            Assert.AreEqual(ViewState.Open, popup.State);
            Assert.AreEqual(1, animator.ShowCount);
            Assert.AreEqual(1, _background.ShowCount);
            Assert.AreSame(popup, _stack.Top);
        }

        [Test]
        public void Open_SuspendsAndHidesCurrentPopup_WhenSecondOpens()
        {
            var closeCount = 0;
            var (first, firstAnimator) = CreatePopup(CountClosed(() => closeCount++));
            var (second, _) = CreatePopup();

            Run(_stack.Open(first, CancellationToken.None));
            Run(_stack.Open(second, CancellationToken.None));

            Assert.AreEqual(ViewState.Suspended, first.State);
            Assert.AreEqual(1, firstAnimator.HideCount);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(1, _background.ShowCount);
            Assert.AreSame(second, _stack.Top);
        }

        [Test]
        public void Close_NotifiesClosedOnce_WhenPopupClosedRepeatedly()
        {
            var closeCount = 0;
            var (popup, _) = CreatePopup(CountClosed(() => closeCount++));
            Run(_stack.Open(popup, CancellationToken.None));

            Run(_stack.Close(popup, CancellationToken.None));
            Run(_stack.Close(popup, CancellationToken.None));

            Assert.AreEqual(1, closeCount);
        }

        [Test]
        public void OpenBatch_ShowsOnlyTop_WithoutAnimatingIntermediates()
        {
            var (first, firstAnimator) = CreatePopup();
            var (second, secondAnimator) = CreatePopup();
            var (third, thirdAnimator) = CreatePopup();

            Run(_stack.OpenBatch(new[] { first, second, third }, CancellationToken.None));

            Assert.AreEqual(ViewState.Suspended, first.State);
            Assert.AreEqual(ViewState.Suspended, second.State);
            Assert.AreEqual(ViewState.Open, third.State);
            Assert.AreEqual(0, firstAnimator.ShowCount);
            Assert.AreEqual(0, firstAnimator.HideCount);
            Assert.AreEqual(0, secondAnimator.ShowCount);
            Assert.AreEqual(0, secondAnimator.HideCount);
            Assert.AreEqual(1, thirdAnimator.ShowCount);
            Assert.AreEqual(1, _background.ShowCount);
            Assert.AreSame(third, _stack.Top);
        }

        [Test]
        public void OpenBatch_HidesExistingTopOnce_BeforeShowingBatchTop()
        {
            var (existing, existingAnimator) = CreatePopup();
            var (first, firstAnimator) = CreatePopup();
            var (second, secondAnimator) = CreatePopup();
            Run(_stack.Open(existing, CancellationToken.None));

            Run(_stack.OpenBatch(new[] { first, second }, CancellationToken.None));

            Assert.AreEqual(ViewState.Suspended, existing.State);
            Assert.AreEqual(1, existingAnimator.HideCount);
            Assert.AreEqual(ViewState.Suspended, first.State);
            Assert.AreEqual(0, firstAnimator.ShowCount);
            Assert.AreEqual(ViewState.Open, second.State);
            Assert.AreEqual(1, secondAnimator.ShowCount);
            Assert.AreEqual(1, _background.ShowCount);
        }

        [Test]
        public void OpenBatch_RestoresPrevious_WhenBatchTopClosed()
        {
            var (first, firstAnimator) = CreatePopup();
            var (second, secondAnimator) = CreatePopup();
            Run(_stack.OpenBatch(new[] { first, second }, CancellationToken.None));

            Run(_stack.Close(second, CancellationToken.None));

            Assert.AreEqual(ViewState.Open, first.State);
            Assert.AreEqual(1, firstAnimator.ShowCount);
            Assert.AreEqual(1, secondAnimator.HideCount);
            Assert.AreSame(first, _stack.Top);
        }

        [Test]
        public void OpenBatch_AddsSingleStackEntry_WhenSameWrapperDuplicatedInBatch()
        {
            var (popup, animator) = CreatePopup();

            Run(_stack.OpenBatch(new[] { popup, popup }, CancellationToken.None));
            Run(_stack.Close(popup, CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, popup.State);
            Assert.IsNull(_stack.Top);
            Assert.AreEqual(1, animator.ShowCount);
            Assert.AreEqual(1, _background.HideCount);
        }

        [Test]
        public void Open_Ignores_AlreadyOpenPopup()
        {
            var (popup, animator) = CreatePopup();

            Run(_stack.Open(popup, CancellationToken.None));
            Run(_stack.Open(popup, CancellationToken.None));

            Assert.AreEqual(1, animator.ShowCount);
        }

        [Test]
        public void Close_HidesBackground_WhenLastPopupClosed()
        {
            var (popup, animator) = CreatePopup();
            Run(_stack.Open(popup, CancellationToken.None));

            Run(_stack.Close(popup, CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, popup.State);
            Assert.AreEqual(1, animator.HideCount);
            Assert.AreEqual(1, _background.HideCount);
            Assert.IsNull(_stack.Top);
        }

        [Test]
        public void Close_ReopensPreviousPopup_WhenTopClosed()
        {
            var (first, firstAnimator) = CreatePopup();
            var (second, secondAnimator) = CreatePopup();
            Run(_stack.Open(first, CancellationToken.None));
            Run(_stack.Open(second, CancellationToken.None));

            Run(_stack.Close(second, CancellationToken.None));

            Assert.AreEqual(ViewState.Open, first.State);
            Assert.AreEqual(2, firstAnimator.ShowCount);
            Assert.AreEqual(1, secondAnimator.HideCount);
            Assert.AreEqual(0, _background.HideCount);
            Assert.AreSame(first, _stack.Top);
        }

        [Test]
        public void Close_RemovesPopupSilently_WhenNotTop()
        {
            var closeCount = 0;
            var (first, firstAnimator) = CreatePopup(CountClosed(() => closeCount++));
            var (second, _) = CreatePopup();
            Run(_stack.Open(first, CancellationToken.None));
            Run(_stack.Open(second, CancellationToken.None));

            Run(_stack.Close(first, CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, first.State);
            // Единственный Hide — от suspend при открытии второго popup-а.
            Assert.AreEqual(1, firstAnimator.HideCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreSame(second, _stack.Top);
            Assert.AreEqual(0, _background.HideCount);
        }

        [Test]
        public void CloseAll_ClosesAllPopups_AndHidesOnlyTop()
        {
            var (first, firstAnimator) = CreatePopup();
            var (second, secondAnimator) = CreatePopup();
            Run(_stack.Open(first, CancellationToken.None));
            Run(_stack.Open(second, CancellationToken.None));

            Run(_stack.CloseAll(CancellationToken.None));

            Assert.AreEqual(ViewState.Closed, first.State);
            Assert.AreEqual(ViewState.Closed, second.State);
            Assert.AreEqual(1, firstAnimator.HideCount);
            Assert.AreEqual(1, secondAnimator.HideCount);
            Assert.AreEqual(1, _background.HideCount);
            Assert.IsNull(_stack.Top);
        }

        private static (ViewWrapper popup, FakeViewAnimator animator) CreatePopup(System.Action<ViewEvent> eventRaised = null)
        {
            var animator = new FakeViewAnimator();
            // View в тестах не нужен: PopupStack работает только с State и Animator.
            return (new ViewWrapper("popup", ViewKind.Popup, null, animator, eventRaised: eventRaised), animator);
        }

        private static System.Action<ViewEvent> CountClosed(System.Action onClosed) =>
            viewEvent =>
            {
                if (viewEvent == ViewEvent.Closed)
                {
                    onClosed();
                }
            };

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();
    }
}
