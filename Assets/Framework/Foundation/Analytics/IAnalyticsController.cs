namespace Framework.Foundation.Analytics
{
    public interface IAnalyticsController
    {
        public void SendEvent(IAnalyticsEvent analyticsEvent);
    }
}