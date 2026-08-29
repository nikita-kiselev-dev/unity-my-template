using System;
using Framework.Features.SaveLoad;
using Framework.Foundation.SaveLoad;
using MemoryPack;

namespace Framework.Features.DailyBonus.Data
{
    [SaveTag(FeaturesSaveTags.DailyBonusData)]
    [MemoryPackable]
    public partial class DailyBonusData : global::Framework.Foundation.SaveLoad.SaveBlob
    {
        public int StreakDay { get; private set; }

        /// Местное время игрока (`IClock.ServerLocalNow`), не UTC: день сбрасывается
        /// в местную полночь, поэтому и сравнивать даты нужно в той же шкале.
        public DateTime LastRewardDate { get; private set; }

        public override void PrepareNewData()
        {
            ResetStreak();
        }

        public void ResetStreak()
        {
            StreakDay = 1;
        }

        public void AddStreakDayData()
        {
            StreakDay++;
        }

        public void SetLastRewardDate(DateTime date)
        {
            LastRewardDate = date;
        }
    }
}
