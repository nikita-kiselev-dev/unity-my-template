using System;
using Framework.Foundation.Analytics;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class AnalyticsControllerTests
    {
        private static AnalyticsEvent CreateEvent() => new("test_event");

        [Test]
        public void SendEvent_RoutesToAllServices_WhenTargetsNotSpecified()
        {
            var first = new FakeAnalyticsService();
            var second = new SecondFakeAnalyticsService();
            var manager = new AnalyticsController(new IAnalyticsService[] { first, second }, new FakeLogChannel());

            manager.SendEvent(CreateEvent());

            Assert.AreEqual(1, first.SentEvents.Count);
            Assert.AreEqual(1, second.SentEvents.Count);
        }

        [Test]
        public void SendEvent_RoutesOnlyToTargetService_WhenTargetSpecified()
        {
            var first = new FakeAnalyticsService();
            var second = new SecondFakeAnalyticsService();
            var manager = new AnalyticsController(new IAnalyticsService[] { first, second }, new FakeLogChannel());

            manager.SendEvent(CreateEvent().To<SecondFakeAnalyticsService>());

            Assert.AreEqual(0, first.SentEvents.Count);
            Assert.AreEqual(1, second.SentEvents.Count);
        }

        [Test]
        public void SendEvent_SkipsService_WhenItsInitFailed()
        {
            var failed = new FakeAnalyticsService { InitSucceeds = false };
            var active = new SecondFakeAnalyticsService();
            var manager = new AnalyticsController(new IAnalyticsService[] { failed, active }, new FakeLogChannel());

            manager.SendEvent(CreateEvent());

            Assert.AreEqual(1, failed.InitCount);
            Assert.AreEqual(0, failed.SentEvents.Count);
            Assert.AreEqual(1, active.SentEvents.Count);
        }

        [Test]
        public void SendEvent_LogsError_WhenNoActiveServices()
        {
            var logger = new FakeLogChannel();
            var manager = new AnalyticsController(Array.Empty<IAnalyticsService>(), logger);

            manager.SendEvent(CreateEvent());

            Assert.AreEqual(1, logger.Errors.Count);
        }

        [Test]
        public void SendEvent_LogsError_WhenTargetServiceMissing()
        {
            var first = new FakeAnalyticsService();
            var logger = new FakeLogChannel();
            var manager = new AnalyticsController(new IAnalyticsService[] { first }, logger);

            manager.SendEvent(CreateEvent().To<SecondFakeAnalyticsService>());

            Assert.AreEqual(0, first.SentEvents.Count);
            Assert.AreEqual(1, logger.Errors.Count);
        }

        private sealed class SecondFakeAnalyticsService : FakeAnalyticsService
        {
        }
    }
}
