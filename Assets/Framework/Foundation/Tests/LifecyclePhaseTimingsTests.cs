using Framework.Foundation.Initialization;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LifecyclePhaseTimingsTests
    {
        [Test]
        public void Describe_OrdersEntities_BySlowestFirst()
        {
            var timings = new LifecyclePhaseTimings();
            timings.Add("Fast", 1);
            timings.Add("Slow", 30);
            timings.Add("Medium", 10);

            Assert.AreEqual("Slow: 30ms\nMedium: 10ms\nFast: 1ms", timings.Describe());
        }

        [Test]
        public void Describe_ReturnsEmpty_WhenNothingMeasured()
        {
            var timings = new LifecyclePhaseTimings();

            Assert.AreEqual(string.Empty, timings.Describe());
        }
    }
}
