using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Features.DailyBonus.Configs;
using Framework.Features.DailyBonus.Model;
using Framework.Features.DailyBonus.ViewModel;
using Framework.Foundation.Asset.Icons;
using Framework.Foundation.Localization;
using Framework.Foundation.Localization.Extensions;
using Framework.Foundation.Utilities;
using UnityEngine;
using Framework.Features.UI;

namespace Framework.Features.DailyBonus.Factory
{
    public class DailyBonusDayViewModelFactory
    {
        private readonly DailyBonusModel _model;
        private readonly IRewardRowLayout _rewardRowLayout;
        private readonly IIconProvider _iconProvider;

        public DailyBonusDayViewModelFactory(
            DailyBonusModel model,
            IRewardRowLayout rewardRowLayout,
            IIconProvider iconProvider)
        {
            _model = model;
            _rewardRowLayout = rewardRowLayout;
            _iconProvider = iconProvider;
        }

        public async UniTask<List<DailyBonusDayViewModel>> Create()
        {
            var dayConfigs = _model.DayConfigs;
            var currentStreakDay = _model.StreakDay;
            var days = new UniTask<DailyBonusDayViewModel>[dayConfigs.Count];

            for (var index = 0; index < dayConfigs.Count; index++)
            {
                days[index] = CreateDay(dayConfigs[index], index, dayConfigs.Count, currentStreakDay);
            }

            return new List<DailyBonusDayViewModel>(await UniTask.WhenAll(days));
        }

        private async UniTask<DailyBonusDayViewModel> CreateDay(
            DailyBonusDayConfig dayConfig,
            int index,
            int dayCount,
            int currentStreakDay)
        {
            var isLastDay = index == dayCount - 1;

            var parent = isLastDay
                ? _rewardRowLayout.GetLastRewardParent()
                : _rewardRowLayout.GetRewardParent(index, dayCount - 1);

            var prefabKey = GetPrefabKey(dayConfig.StreakDay, currentStreakDay, isLastDay);
            var (itemSprite, dayText) = await UniTask.WhenAll(
                _iconProvider.GetIconFromAtlas(dayConfig.ItemSprite, dayConfig.ItemName),
                GetDayText(prefabKey, dayConfig.StreakDay));

            return new DailyBonusDayViewModel(prefabKey, parent, dayText, $"x{dayConfig.ItemCount}", itemSprite);
        }

        internal static string GetPrefabKey(int streakDay, int currentStreakDay, bool isLastDay)
        {
            if (currentStreakDay > streakDay)
            {
                return DailyBonusConstants.Prefabs.PreviousDay;
            }

            if (currentStreakDay < streakDay && isLastDay)
            {
                return DailyBonusConstants.Prefabs.LastDay;
            }

            if (currentStreakDay < streakDay)
            {
                return DailyBonusConstants.Prefabs.NextDay;
            }

            if (isLastDay)
            {
                return DailyBonusConstants.Prefabs.TodayLastDay;
            }

            return DailyBonusConstants.Prefabs.Today;
        }

        private static async UniTask<string> GetDayText(string prefabKey, int dayNumber)
        {
            if (prefabKey == DailyBonusConstants.Prefabs.TodayLastDay)
            {
                var congratulationsText = await DailyBonusConstants.Localization.Congratulations
                    .Localize(LocalizationConstants.Tables.General);
                return $"{congratulationsText}!";
            }

            if (prefabKey == DailyBonusConstants.Prefabs.Today)
            {
                return await DailyBonusConstants.Localization.TodayDay.Localize(LocalizationConstants.Tables.General);
            }

            var dayText = await DailyBonusConstants.Localization.Day.Localize(LocalizationConstants.Tables.General);
            return $"{dayText} {dayNumber}";
        }
    }
}
