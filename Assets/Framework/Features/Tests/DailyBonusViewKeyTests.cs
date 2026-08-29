using Framework.Features.DailyBonus;
using Framework.Features.DailyBonus.Factory;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    public class DailyBonusViewKeyTests
    {
        [Test]
        public void GetPrefabKey_PreviousDay_WhenStreakAhead()
        {
            var key = DailyBonusDayViewModelFactory.GetPrefabKey(streakDay: 1, currentStreakDay: 2, isLastDay: false);

            Assert.AreEqual(DailyBonusConstants.Prefabs.PreviousDay, key);
        }

        [Test]
        public void GetPrefabKey_Today_WhenStreakMatches()
        {
            var key = DailyBonusDayViewModelFactory.GetPrefabKey(streakDay: 2, currentStreakDay: 2, isLastDay: false);

            Assert.AreEqual(DailyBonusConstants.Prefabs.Today, key);
        }

        [Test]
        public void GetPrefabKey_TodayLastDay_WhenStreakMatches_OnLastDay()
        {
            var key = DailyBonusDayViewModelFactory.GetPrefabKey(streakDay: 3, currentStreakDay: 3, isLastDay: true);

            Assert.AreEqual(DailyBonusConstants.Prefabs.TodayLastDay, key);
        }

        [Test]
        public void GetPrefabKey_NextDay_WhenStreakBehind()
        {
            var key = DailyBonusDayViewModelFactory.GetPrefabKey(streakDay: 3, currentStreakDay: 2, isLastDay: false);

            Assert.AreEqual(DailyBonusConstants.Prefabs.NextDay, key);
        }

        [Test]
        public void GetPrefabKey_LastDay_WhenStreakBehind_OnLastDay()
        {
            var key = DailyBonusDayViewModelFactory.GetPrefabKey(streakDay: 3, currentStreakDay: 1, isLastDay: true);

            Assert.AreEqual(DailyBonusConstants.Prefabs.LastDay, key);
        }
    }
}
