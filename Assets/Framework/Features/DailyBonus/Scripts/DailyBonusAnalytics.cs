using Framework.Foundation.Analytics;

namespace Framework.Features.DailyBonus
{
    public class DailyBonusAnalytics : IDailyBonusAnalytics
    {
        private readonly IAnalyticsController _analyticsController;
        
        public DailyBonusAnalytics(IAnalyticsController analyticsController)
        {
            _analyticsController = analyticsController;
        }

        public void LogPopupOpen(int currentDay)
        {
            var analyticsEvent = new AnalyticsEvent(DailyBonusConstants.Analytics.PopupOpenName)
                .AddParameter(DailyBonusConstants.Analytics.PopupOpenParameterCurrentDay, currentDay);
            
            _analyticsController.SendEvent(analyticsEvent);
        }

        public void LogStreakLose(int streakDay)
        {
            var analyticsEvent = new AnalyticsEvent(DailyBonusConstants.Analytics.StreakLoseName)
                .AddParameter(DailyBonusConstants.Analytics.StreakLoseParameterStreakLoseDay, streakDay);
            
            _analyticsController.SendEvent(analyticsEvent);
        }
    }
}