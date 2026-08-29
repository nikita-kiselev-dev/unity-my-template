namespace Framework.Foundation.Initialization.Decorators
{
    public interface ILifecycleDecorator
    {
        public bool IsDecoratable(LifecycleEntity lifecycleEntity);
        LifecycleEntity Decorate(LifecycleEntity lifecycleEntity);
    }
}