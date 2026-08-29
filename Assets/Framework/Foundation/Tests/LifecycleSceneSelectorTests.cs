using Framework.Foundation.Initialization;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LifecycleSceneSelectorTests
    {
        [Test]
        public void SelectForScene_ReturnsMatchingEntities_InInitOrder()
        {
            var otherScene = new OtherSceneEntity();
            var last = new LastEntity();
            var first = new FirstEntity();

            var result = LifecycleSceneSelector.SelectForScene(
                new LifecycleEntity[] { otherScene, last, first },
                "TestScene");

            Assert.AreEqual(2, result.Length);
            Assert.AreSame(first, result[0]);
            Assert.AreSame(last, result[1]);
        }

        [LifecycleOrderAttribute("TestScene", 20)]
        private sealed class LastEntity : LifecycleEntity
        {
        }

        [LifecycleOrderAttribute("TestScene", 10)]
        private sealed class FirstEntity : LifecycleEntity
        {
        }

        [Test]
        public void SelectForScene_OrdersByTypeName_WhenInitOrderEqual()
        {
            var second = new TieBravoEntity();
            var first = new TieAlphaEntity();

            var result = LifecycleSceneSelector.SelectForScene(
                new LifecycleEntity[] { second, first },
                "TieScene");

            Assert.AreSame(first, result[0]);
            Assert.AreSame(second, result[1]);
        }

        [LifecycleOrderAttribute("OtherScene", 0)]
        private sealed class OtherSceneEntity : LifecycleEntity
        {
        }

        [LifecycleOrderAttribute("TieScene", 5)]
        private sealed class TieAlphaEntity : LifecycleEntity
        {
        }

        [LifecycleOrderAttribute("TieScene", 5)]
        private sealed class TieBravoEntity : LifecycleEntity
        {
        }
    }
}
