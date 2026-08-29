namespace Framework.Features.DailyBonus
{
    public static class DailyBonusConstants
    {
        public const string LogName = "DailyBonus";

        public static class Prefabs
        {
            public const string Popup = "DailyBonusPopup";
            public const string PreviousDay = "DailyBonusPreviousDay";
            public const string Today = "DailyBonusToday";
            public const string NextDay = "DailyBonusNextDay";
            public const string LastDay = "DailyBonusLastDay";
            public const string TodayLastDay = "DailyBonusTodayLastDay";
        }

        public static class Configs
        {
            public const string Key = "DailyBonusConfig";
        }

        public static class Analytics
        {
            public const string PopupOpenName = "daily_bonus_popup_open";
            public const string PopupOpenParameterCurrentDay = "current_day";
            public const string StreakLoseName = "daily_bonus_streak_lose";
            public const string StreakLoseParameterStreakLoseDay = "streak_lose_day";
        }

        public static class Localization
        {
            public const string Congratulations = "congratulations";
            public const string TodayDay = "time/today";
            public const string Day = "time/day";
        }
    }
}