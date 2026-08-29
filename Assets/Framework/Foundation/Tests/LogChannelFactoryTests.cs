using Framework.Foundation.Logger;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LogChannelFactoryTests
    {
        [Test]
        public void Get_ReturnsSameInstance_ForSameNameAndType()
        {
            var factory = new LogChannelFactory();

            var first = factory.Get("Feature", LogCategory.Feature);
            var second = factory.Get("Feature", LogCategory.Feature);

            Assert.AreSame(first, second);
        }

        [Test]
        public void Get_ReturnsDifferentInstances_ForDifferentEntityTypes()
        {
            var factory = new LogChannelFactory();

            var feature = factory.Get("Name", LogCategory.Feature);
            var system = factory.Get("Name", LogCategory.System);

            Assert.AreNotSame(feature, system);
        }

        [Test]
        public void Get_ReturnsDifferentInstances_ForDifferentNames()
        {
            var factory = new LogChannelFactory();

            Assert.AreNotSame(factory.Get("First"), factory.Get("Second"));
        }

        [Test]
        public void AreLogsEnabled_IsTrue_ByDefault()
        {
            Assert.IsTrue(new LogChannelFactory().Get("Name").AreLogsEnabled);
        }

        [Test]
        public void AreLogsEnabled_FollowsSetLogsStatus()
        {
            var logger = new LogChannelFactory().Get("Name");

            logger.SetLogsStatus(false);

            Assert.IsFalse(logger.AreLogsEnabled);
        }
    }
}
