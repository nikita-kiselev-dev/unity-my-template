using NUnit.Framework;
using R3;

namespace Framework.Foundation.Tests
{
    public class ViewModelTests
    {
        private sealed class TestViewModel : Framework.Foundation.UI.Mvvm.ViewModel
        {
            public int ReceivedCount { get; private set; }

            public TestViewModel(Observable<int> source)
            {
                source.Subscribe(_ => ReceivedCount++).AddTo(ref Subscriptions);
            }
        }

        [Test]
        public void Dispose_CleansUpSubscriptions()
        {
            var subject = new Subject<int>();
            var viewModel = new TestViewModel(subject);

            subject.OnNext(1);
            viewModel.Dispose();
            subject.OnNext(2);

            Assert.AreEqual(1, viewModel.ReceivedCount);
        }
    }
}
