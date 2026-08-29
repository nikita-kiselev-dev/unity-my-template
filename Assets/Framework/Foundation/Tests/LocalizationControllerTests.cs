using System.Threading;
using Framework.Foundation.Localization.Controller;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LocalizationControllerTests
    {
        [Test]
        public void Init_MarksEntityEnabled()
        {
            var controller = new LocalizationController();

            controller.InitPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(controller.Status.IsEnabled);
        }
    }
}
