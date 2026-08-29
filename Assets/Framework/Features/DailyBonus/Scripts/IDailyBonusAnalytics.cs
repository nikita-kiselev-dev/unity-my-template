namespace Framework.Features.DailyBonus
{
    public interface IDailyBonusAnalytics
    {
        public void LogPopupOpen(int currentDay);
        public void LogStreakLose(int streakDay);
    }
}