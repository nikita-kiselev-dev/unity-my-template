namespace Framework.Foundation.Initialization.Decorators
{
    public interface ILifecycleDecoratorPipeline
    {
        void TryDecorate(LifecycleEntity[] lifecycleEntities);
    }
}