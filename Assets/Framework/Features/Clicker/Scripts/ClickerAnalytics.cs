using Framework.Foundation.Analytics;

namespace Framework.Features.Clicker
{
    public class ClickerAnalytics : IClickerAnalytics
    {
        private readonly IAnalyticsController _analyticsController;
        
        public ClickerAnalytics(IAnalyticsController analyticsController)
        {
            _analyticsController = analyticsController;
        }
        
        public void LogUpgrade(int level)
        {
            var analyticsEvent = new AnalyticsEvent(ClickerConstants.Analytics.UpgradeName)
                .AddParameter(ClickerConstants.Analytics.UpgradeParameterLevel, level);
            
            _analyticsController.SendEvent(analyticsEvent);
        }
    }
}