using System;
using System.Collections.Generic;
using Framework.Features.DailyBonus.Data;
using Framework.Features.DailyBonus.Configs;
using Framework.Foundation.Logger;
using ZLinq;

namespace Framework.Features.DailyBonus.Model
{
    public class DailyBonusModel
    {
        private readonly IDailyBonusConfig _config;
        private readonly DailyBonusData _data;
        private readonly ILogChannel _logger;

        private DailyBonusDayConfig[] _sortedDays;

        public IReadOnlyList<DailyBonusDayConfig> DayConfigs => GetSortedDays();
        public int StreakDay => _data.StreakDay;
        public DateTime LastRewardDate => _data.LastRewardDate;

        public DailyBonusModel(IDailyBonusConfig config, DailyBonusData data, ILogChannel logger)
        {
            _config = config;
            _data = data;
            _logger = logger;
        }

        public bool TryGetCurrentDayConfig(out DailyBonusDayConfig config)
        {
            config = GetSortedDays().AsValueEnumerable().FirstOrDefault(day => day.StreakDay == StreakDay);
            return config != null;
        }

        public bool IsTodayRewardReceived(DateTime localNow)
        {
            return localNow.Date == _data.LastRewardDate.Date;
        }

        // Первый запуск: даты нет — считаем, что награда была «вчера», чтобы сегодня попап показался.
        public void InitLastRewardDate(DateTime localNow)
        {
            if (_data.LastRewardDate == default)
            {
                _data.SetLastRewardDate(localNow - TimeSpan.FromDays(1));
            }
        }

        public bool ShouldShowPopup(DateTime localNow)
        {
            return TryGetCurrentDayConfig(out _) && !IsTodayRewardReceived(localNow);
        }

        public DailyBonusDecision Evaluate(DateTime localNow)
        {
            InitLastRewardDate(localNow);
            var streakUpdate = UpdateStreak(localNow);
            var shouldShowPopup = ShouldShowPopup(localNow);
            return new DailyBonusDecision(shouldShowPopup, streakUpdate);
        }

        public StreakUpdate UpdateStreak(DateTime localNow)
        {
            var daysPassed = (localNow.Date - _data.LastRewardDate.Date).Days;

            if (daysPassed > 1)
            {
                var lostStreakDay = StreakDay;
                _data.ResetStreak();
                return StreakUpdate.Lost(lostStreakDay);
            }

            if (daysPassed == 1 && HasCollectedAllRewards())
            {
                _data.ResetStreak();
                return StreakUpdate.Restarted;
            }

            return StreakUpdate.None;
        }

        public void ClaimReward(DateTime localNow)
        {
            _data.AddStreakDayData();
            _data.SetLastRewardDate(localNow);
        }

        public bool HasCollectedAllRewards()
        {
            var lastDayConfig = GetSortedDays().AsValueEnumerable().LastOrDefault();

            if (lastDayConfig == null)
            {
                _logger.LogError("Last day config does not exist!");
                return true;
            }

            return StreakDay > lastDayConfig.StreakDay;
        }

        private DailyBonusDayConfig[] GetSortedDays()
        {
            return _sortedDays ??= _config.Days.AsValueEnumerable().OrderBy(config => config.StreakDay).ToArray();
        }
    }
}
