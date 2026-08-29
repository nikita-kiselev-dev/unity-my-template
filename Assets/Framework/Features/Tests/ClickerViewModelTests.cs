using Framework.Features.Clicker.Data;
using Framework.Features.Clicker.Model;
using Framework.Features.Clicker.ViewModel;
using Framework.Features.Tests.Fakes;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;
using R3;

namespace Framework.Features.Tests
{
    public class ClickerViewModelTests
    {
        private FakeInventory _inventory;
        private FakeClickerAnalytics _analytics;
        private ClickerData _data;

        [SetUp]
        public void SetUp()
        {
            _inventory = new FakeInventory();
            _analytics = new FakeClickerAnalytics();
            _data = new ClickerData();
            _data.PrepareNewData();
        }

        private ClickerViewModel CreateViewModel(params (long income, long cost)[] levels)
        {
            var model = new ClickerModel(FeaturesTestConfigs.Clicker(levels), _data, _inventory, new FakeLogChannel());
            return new ClickerViewModel(model, _analytics);
        }

        [Test]
        public void ClickCommand_AddsIncome()
        {
            var viewModel = CreateViewModel((5, 10), (7, 20));

            viewModel.Click.Execute(Unit.Default);

            Assert.AreEqual(1, _inventory.Added.Count);
            Assert.AreEqual(1, _data.ClickCount);
        }

        [Test]
        public void UpgradeCommand_LogsAnalytics_OnSuccess()
        {
            var viewModel = CreateViewModel((5, 10), (7, 20));

            viewModel.Upgrade.Execute(Unit.Default);

            Assert.AreEqual(new[] { 2 }, _analytics.LoggedUpgradeLevels.ToArray());
        }

        [Test]
        public void UpgradeCommand_DoesNotLogAnalytics_WhenUpgradeFails()
        {
            _inventory.RemoveResult = false;
            var viewModel = CreateViewModel((5, 10), (7, 20));

            viewModel.Upgrade.Execute(Unit.Default);

            Assert.AreEqual(0, _analytics.LoggedUpgradeLevels.Count);
        }
    }
}
