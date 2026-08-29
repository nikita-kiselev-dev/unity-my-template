using Framework.Foundation.Utilities;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class EntityStatusTests
    {
        [Test]
        public void SetStatuses_DoNotThrow_AfterDispose()
        {
            var status = new EntityStatus("TestEntity");
            status.SetEnabled(true).SetInited(true).SetActive(true);
            status.Dispose();

            Assert.DoesNotThrow(() => status.SetEnabled(false).SetInited(false).SetActive(false));
        }

        [Test]
        public void Dispose_DoesNotThrow_WhenCalledTwice()
        {
            var status = new EntityStatus("TestEntity");
            status.Dispose();

            Assert.DoesNotThrow(() => status.Dispose());
        }
    }
}
