using System.Numerics;
using Framework.Features.Clicker.Data;
using Framework.Features.Clicker.Model;
using Framework.Features.Tests.Fakes;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    public class ClickerModelTests
    {
        private FakeInventory _inventory;
        private ClickerData _data;
        private FakeLogChannel _logger;

        [SetUp]
        public void SetUp()
        {
            _inventory = new FakeInventory();
            _logger = new FakeLogChannel();
            _data = new ClickerData();
            _data.PrepareNewData();
        }

        private ClickerModel CreateModel(params (long income, long cost)[] levels)
        {
            return new ClickerModel(FeaturesTestConfigs.Clicker(levels), _data, _inventory, _logger);
        }

        private void UpgradeData(int times)
        {
            for (var i = 0; i < times; i++)
            {
                _data.Upgrade();
            }
        }

        [Test]
        public void Constructor_ExposesInitialLevelState()
        {
            var model = CreateModel((5, 10), (7, 20));

            Assert.AreEqual(0, model.Level.CurrentValue);
            Assert.AreEqual(new BigInteger(5), model.CurrentLevelConfig.CurrentValue.IncomePerClick);
            Assert.IsTrue(model.CanUpgrade.CurrentValue);
        }

        [Test]
        public void Constructor_ClampsLevelToLastConfig_WhenSaveIsAheadOfConfig()
        {
            UpgradeData(5);

            var model = CreateModel((5, 10), (7, 20));

            Assert.AreEqual(1, model.Level.CurrentValue);
            Assert.AreEqual(new BigInteger(7), model.CurrentLevelConfig.CurrentValue.IncomePerClick);
            Assert.IsFalse(model.CanUpgrade.CurrentValue);
            Assert.AreEqual(1, _logger.Errors.Count);
        }

        [Test]
        public void Click_AddsLastLevelIncome_WhenSaveIsAheadOfConfig()
        {
            UpgradeData(5);
            var model = CreateModel((5, 10), (7, 20));

            model.Click();

            Assert.AreEqual(new BigInteger(7), _inventory.Added[0].Value);
        }

        [Test]
        public void Click_AddsIncomeForCurrentLevel()
        {
            var model = CreateModel((5, 10), (7, 20));

            model.Click();

            Assert.AreEqual(1, _inventory.Added.Count);
            Assert.AreEqual(new BigInteger(5), _inventory.Added[0].Value);
        }

        [Test]
        public void Click_IncrementsClickCount()
        {
            var model = CreateModel((5, 10));

            model.Click();
            model.Click();

            Assert.AreEqual(2, _data.ClickCount);
        }

        [Test]
        public void Click_Logs_WhenLogsEnabled()
        {
            var model = CreateModel((5, 10));

            model.Click();

            Assert.AreEqual(1, _logger.Messages.Count);
        }

        [Test]
        public void Click_SkipsLogging_WhenLogsDisabled()
        {
            _logger.SetLogsStatus(false);
            var model = CreateModel((5, 10));

            model.Click();

            Assert.AreEqual(1, _data.ClickCount);
            Assert.IsEmpty(_logger.Messages);
        }

        [Test]
        public void TryUpgrade_SpendsCost_AndRaisesLevel()
        {
            var model = CreateModel((5, 10), (7, 20));

            var result = model.TryUpgrade();

            Assert.IsTrue(result);
            Assert.AreEqual(new BigInteger(10), _inventory.Removed[0].Value);
            Assert.AreEqual(1, model.Level.CurrentValue);
            Assert.AreEqual(new BigInteger(7), model.CurrentLevelConfig.CurrentValue.IncomePerClick);
        }

        [Test]
        public void TryUpgrade_ReturnsFalse_OnMaxLevel_WithoutSpending()
        {
            var model = CreateModel((5, 10));

            var result = model.TryUpgrade();

            Assert.IsFalse(result);
            Assert.AreEqual(0, _inventory.Removed.Count);
            Assert.AreEqual(0, _data.Level);
        }

        [Test]
        public void TryUpgrade_ReturnsFalse_WhenNotEnoughCurrency()
        {
            _inventory.RemoveResult = false;
            var model = CreateModel((5, 10), (7, 20));

            var result = model.TryUpgrade();

            Assert.IsFalse(result);
            Assert.AreEqual(0, model.Level.CurrentValue);
            Assert.AreEqual(0, _data.Level);
        }

        [Test]
        public void CanUpgrade_BecomesFalse_OnLastLevel()
        {
            var model = CreateModel((5, 10), (7, 20));

            model.TryUpgrade();

            Assert.IsFalse(model.CanUpgrade.CurrentValue);
        }
    }
}
