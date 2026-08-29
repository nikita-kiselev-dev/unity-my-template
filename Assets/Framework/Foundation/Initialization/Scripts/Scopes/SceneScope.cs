using Framework.Foundation.Initialization.Extensions;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.Initialization.Scopes
{
    public sealed class SceneScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<SceneStarter>();
            builder.RegisterEntryPointLogging();
        }
    }
}
