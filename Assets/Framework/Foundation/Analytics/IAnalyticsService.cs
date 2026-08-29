namespace Framework.Foundation.Analytics
{
    public interface IAnalyticsService
    {
        public bool IsInited { get; }
        public void Init();
        public void SendEvent(IAnalyticsEvent analyticsEvent);
    }
}