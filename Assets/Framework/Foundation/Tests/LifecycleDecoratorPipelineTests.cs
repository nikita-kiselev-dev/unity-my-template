using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class LifecycleDecoratorPipelineTests
    {
        [Test]
        public void TryDecorate_AddsWrapper_WhenEntityDecoratable()
        {
            var controller = CreateController();
            var entity = new TestEntity();

            controller.TryDecorate(new LifecycleEntity[] { entity });

            Assert.AreEqual(1, entity.Wrappers.Count);
        }

        [Test]
        public void TryDecorate_SkipsEntity_WhenAlreadyDecorated()
        {
            var controller = CreateController();
            var entity = new TestEntity();
            var entities = new LifecycleEntity[] { entity };

            controller.TryDecorate(entities);
            controller.TryDecorate(entities);

            Assert.AreEqual(1, entity.Wrappers.Count);
        }

        private static LifecycleDecoratorPipeline CreateController()
        {
            return new LifecycleDecoratorPipeline(new ILifecycleDecorator[] { new WrappingDecorator() });
        }

        private sealed class WrappingDecorator : ILifecycleDecorator
        {
            public bool IsDecoratable(LifecycleEntity lifecycleEntity) => true;

            public LifecycleEntity Decorate(LifecycleEntity lifecycleEntity) => new WrapperEntity();
        }

        private sealed class TestEntity : LifecycleEntity
        {
        }

        private sealed class WrapperEntity : LifecycleEntity
        {
        }
    }
}
