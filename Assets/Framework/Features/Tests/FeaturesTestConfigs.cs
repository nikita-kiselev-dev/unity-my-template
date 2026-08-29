using System.Linq;
using Framework.Features.Clicker.Configs;
using Framework.Features.DailyBonus.Configs;
using Newtonsoft.Json;

namespace Framework.Features.Tests
{
    // Конфиги заполняются Newtonsoft-ом из JSON (поля приватные) — в тестах строим их так же, как прод.
    internal static class FeaturesTestConfigs
    {
        public static ClickerConfig Clicker(params (long income, long cost)[] levels)
        {
            var levelsJson = string.Join(",", levels.Select(level =>
                $"{{\"income_per_click\":{level.income},\"upgrade_cost\":{level.cost}}}"));

            return JsonConvert.DeserializeObject<ClickerConfig>(
                $"{{\"is_enabled\":true,\"clicker_levels\":[{levelsJson}]}}");
        }

        public static DailyBonusConfig DailyBonus(params int[] streakDays)
        {
            var daysJson = string.Join(",", streakDays.Select(day =>
                $"{{\"streak_day\":{day},\"item_name\":\"gold\",\"item_sprite\":\"gold_icon\",\"item_count\":{day * 10}}}"));

            return JsonConvert.DeserializeObject<DailyBonusConfig>(
                $"{{\"is_enabled\":true,\"streak_days\":[{daysJson}]}}");
        }
    }
}
