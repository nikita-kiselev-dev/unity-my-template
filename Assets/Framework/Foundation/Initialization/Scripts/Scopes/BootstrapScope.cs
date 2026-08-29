using System;
using Framework.Foundation.Initialization.Extensions;
using Framework.Foundation.UI.LoadingCurtain.Controller;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.Initialization.Scopes
{
    public sealed class BootstrapScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<SceneStarter>();
            builder.RegisterEntryPointLogging();

            var gameBootstrapper = FindAnyObjectByType<GameBootstrapper>();
            if (gameBootstrapper == null)
            {
                throw new InvalidOperationException("Bootstrap scene must contain GameBootstrapper.");
            }

            builder.RegisterComponent(gameBootstrapper).AsSelf();

            var loadingCurtainController = FindAnyObjectByType<LoadingCurtainController>();
            if (loadingCurtainController == null)
            {
                throw new InvalidOperationException("Bootstrap scene must contain LoadingCurtainController.");
            }

            builder.RegisterComponent(loadingCurtainController).AsImplementedInterfaces();
        }
    }
}
