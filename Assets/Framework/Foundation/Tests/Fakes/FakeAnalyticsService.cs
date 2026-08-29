using System.Collections.Generic;
using Framework.Foundation.Analytics;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeAnalyticsService : IAnalyticsService
    {
        public bool InitSucceeds = true;

        public int InitCount { get; private set; }
        public List<IAnalyticsEvent> SentEvents { get; } = new();

        public bool IsInited { get; private set; }

        public void Init()
        {
            InitCount++;
            IsInited = InitSucceeds;
        }

        public void SendEvent(IAnalyticsEvent analyticsEvent) => SentEvents.Add(analyticsEvent);
    }
}
