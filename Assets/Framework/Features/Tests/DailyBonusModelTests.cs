using System;
using Framework.Features.DailyBonus.Data;
using Framework.Features.DailyBonus.Model;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    public class DailyBonusModelTests
    {
        private static readonly DateTime Today = new(2026, 7, 10, 15, 30, 0);

        private DailyBonusData _data;
        private FakeLogChannel _logger;

        [SetUp]
        public void SetUp()
        {
            _data = new DailyBonusData();
            _data.PrepareNewData();
            _logger = new FakeLogChannel();
        }

        private DailyBonusModel CreateModel(params int[] streakDays)
        {
            return new DailyBonusModel(FeaturesTestConfigs.DailyBonus(streakDays), _data, _logger);
        }

        [Test]
        public void InitLastRewardDate_SetsYesterday_WhenDateUnset()
        {
            var model = CreateModel(1, 2, 3);

            model.InitLastRewardDate(Today);

            Assert.AreEqual(Today - TimeSpan.FromDays(1), model.LastRewardDate);
        }

        [Test]
        public void InitLastRewardDate_KeepsExistingDate()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(5));
            var model = CreateModel(1, 2, 3);

            model.InitLastRewardDate(Today);

            Assert.AreEqual(Today - TimeSpan.FromDays(5), model.LastRewardDate);
        }

        [Test]
        public void IsTodayRewardReceived_True_WhenRewardWasToday()
        {
            _data.SetLastRewardDate(Today.Date.AddHours(2));
            var model = CreateModel(1, 2, 3);

            Assert.IsTrue(model.IsTodayRewardReceived(Today));
        }

        [Test]
        public void IsTodayRewardReceived_False_OnNextDay()
        {
            _data.SetLastRewardDate(Today);
            var model = CreateModel(1, 2, 3);

            Assert.IsFalse(model.IsTodayRewardReceived(Today.AddDays(1)));
        }

        [Test]
        public void ShouldShowPopup_True_WhenRewardNotReceivedToday()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(1));
            var model = CreateModel(1, 2, 3);

            Assert.IsTrue(model.ShouldShowPopup(Today));
        }

        [Test]
        public void ShouldShowPopup_False_WhenRewardReceivedToday()
        {
            _data.SetLastRewardDate(Today);
            var model = CreateModel(1, 2, 3);

            Assert.IsFalse(model.ShouldShowPopup(Today));
        }

        [Test]
        public void ShouldShowPopup_False_WhenNoConfigForStreakDay()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(1));
            SetStreakDay(4);
            var model = CreateModel(1, 2, 3);

            Assert.IsFalse(model.ShouldShowPopup(Today));
        }

        [Test]
        public void UpdateStreak_None_WhenRewardWasToday()
        {
            _data.SetLastRewardDate(Today);
            var model = CreateModel(1, 2, 3);

            var update = model.UpdateStreak(Today);

            Assert.IsFalse(update.StreakChanged);
            Assert.IsFalse(update.StreakLost);
        }

        [Test]
        public void UpdateStreak_Lost_WhenMissedMoreThanOneDay()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(2));
            SetStreakDay(3);
            var model = CreateModel(1, 2, 3);

            var update = model.UpdateStreak(Today);

            Assert.IsTrue(update.StreakChanged);
            Assert.IsTrue(update.StreakLost);
            Assert.AreEqual(3, update.LostStreakDay);
            Assert.AreEqual(1, model.StreakDay);
        }

        [Test]
        public void UpdateStreak_Restarted_WhenAllRewardsCollected_AndDayPassed()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(1));
            SetStreakDay(4);
            var model = CreateModel(1, 2, 3);

            var update = model.UpdateStreak(Today);

            Assert.IsTrue(update.StreakChanged);
            Assert.IsFalse(update.StreakLost);
            Assert.AreEqual(1, model.StreakDay);
        }

        [Test]
        public void UpdateStreak_None_WhenDayPassed_AndRewardsRemain()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(1));
            SetStreakDay(2);
            var model = CreateModel(1, 2, 3);

            var update = model.UpdateStreak(Today);

            Assert.IsFalse(update.StreakChanged);
            Assert.AreEqual(2, model.StreakDay);
        }

        [Test]
        public void Evaluate_ResetsStreakBeforePopupDecision_WhenSeveralDaysMissed()
        {
            _data.SetLastRewardDate(Today - TimeSpan.FromDays(7));
            SetStreakDay(3);
            var model = CreateModel(1, 2, 3);

            var decision = model.Evaluate(Today);

            Assert.IsTrue(decision.ShouldShowPopup);
            Assert.IsTrue(decision.StreakUpdate.StreakLost);
            Assert.AreEqual(3, decision.StreakUpdate.LostStreakDay);
            Assert.AreEqual(1, model.StreakDay);
        }

        [Test]
        public void ClaimReward_AdvancesStreak_AndStoresDate()
        {
            var model = CreateModel(1, 2, 3);

            model.ClaimReward(Today);

            Assert.AreEqual(2, model.StreakDay);
            Assert.AreEqual(Today, model.LastRewardDate);
        }

        [Test]
        public void TryGetCurrentDayConfig_ReturnsConfigForCurrentStreakDay()
        {
            SetStreakDay(2);
            var model = CreateModel(3, 1, 2);

            var found = model.TryGetCurrentDayConfig(out var config);

            Assert.IsTrue(found);
            Assert.AreEqual(2, config.StreakDay);
        }

        [Test]
        public void TryGetCurrentDayConfig_False_WhenStreakBeyondConfigs()
        {
            SetStreakDay(4);
            var model = CreateModel(1, 2, 3);

            Assert.IsFalse(model.TryGetCurrentDayConfig(out _));
        }

        [Test]
        public void HasCollectedAllRewards_True_WhenStreakBeyondLastDay()
        {
            SetStreakDay(4);
            var model = CreateModel(1, 2, 3);

            Assert.IsTrue(model.HasCollectedAllRewards());
        }

        [Test]
        public void HasCollectedAllRewards_False_WhenRewardsRemain()
        {
            SetStreakDay(2);
            var model = CreateModel(1, 2, 3);

            Assert.IsFalse(model.HasCollectedAllRewards());
        }

        private void SetStreakDay(int streakDay)
        {
            while (_data.StreakDay < streakDay)
            {
                _data.AddStreakDayData();
            }
        }
    }
}
