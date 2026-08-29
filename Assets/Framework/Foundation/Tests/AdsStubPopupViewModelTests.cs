using Cysharp.Threading.Tasks;
using Framework.Foundation.Ads;
using Framework.Foundation.Ads.Stub.ViewModel;
using NUnit.Framework;
using R3;

namespace Framework.Foundation.Tests
{
    public class AdsStubPopupViewModelTests
    {
        private AdsStubPopupViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _viewModel = new AdsStubPopupViewModel();
        }

        [TearDown]
        public void TearDown()
        {
            _viewModel.Dispose();
        }

        private static AdResult Await(UniTask<AdResult> task) => task.GetAwaiter().GetResult();

        [Test]
        public void Prepare_CompletesWithSuccess_WhenSuccessPressed()
        {
            var pending = _viewModel.Prepare(AdFormat.Rewarded);

            _viewModel.Success.Execute(Unit.Default);

            Assert.AreEqual(AdResult.Success, Await(pending));
        }

        [Test]
        public void Prepare_CompletesWithFailed_WhenFailPressed()
        {
            var pending = _viewModel.Prepare(AdFormat.Rewarded);

            _viewModel.Fail.Execute(Unit.Default);

            Assert.AreEqual(AdResult.Failed, Await(pending));
        }

        [Test]
        public void Prepare_CompletesWithSkipped_WhenPopupClosed()
        {
            var pending = _viewModel.Prepare(AdFormat.Interstitial);

            _viewModel.Complete(AdResult.Skipped);

            Assert.AreEqual(AdResult.Skipped, Await(pending));
        }

        [Test]
        public void Complete_KeepsFirstResult_WhenCalledTwice()
        {
            var pending = _viewModel.Prepare(AdFormat.Rewarded);

            _viewModel.Success.Execute(Unit.Default);
            _viewModel.Fail.Execute(Unit.Default);
            _viewModel.Complete(AdResult.Skipped);

            Assert.AreEqual(AdResult.Success, Await(pending));
        }

        [Test]
        public void Complete_IsIgnored_WhenNothingPrepared()
        {
            Assert.DoesNotThrow(() => _viewModel.Complete(AdResult.Skipped));
        }

        [Test]
        public void Prepare_ShowsFailButton_ForRewardedOnly()
        {
            _viewModel.Prepare(AdFormat.Rewarded);
            Assert.IsTrue(_viewModel.IsFailAvailable.CurrentValue);

            _viewModel.Complete(AdResult.Skipped);
            _viewModel.Prepare(AdFormat.Interstitial);

            Assert.IsFalse(_viewModel.IsFailAvailable.CurrentValue);
        }

        [Test]
        public void Prepare_SetsTitle_ToFormatName()
        {
            _viewModel.Prepare(AdFormat.Interstitial);

            Assert.AreEqual(nameof(AdFormat.Interstitial), _viewModel.Title.CurrentValue);
        }

        [Test]
        public void Prepare_StartsNewSession_AfterPreviousCompleted()
        {
            var first = _viewModel.Prepare(AdFormat.Rewarded);
            _viewModel.Success.Execute(Unit.Default);
            Await(first);

            var second = _viewModel.Prepare(AdFormat.Rewarded);
            _viewModel.Fail.Execute(Unit.Default);

            Assert.AreEqual(AdResult.Failed, Await(second));
        }
    }
}
