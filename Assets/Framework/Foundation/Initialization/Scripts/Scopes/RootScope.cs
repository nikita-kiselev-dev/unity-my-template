using System;
using Framework.Foundation.Audio;
using Framework.Foundation.Initialization.Extensions;
using Framework.Foundation.Initialization.Registrators.Ads;
using Framework.Foundation.Initialization.Registrators.Data;
using Framework.Foundation.Initialization.Registrators.LiveOps;
using R3;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.Initialization.Scopes
{
    public class RootScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            LiveOpsScopeRegistrator.Configure(builder);
            DataScopeRegistrator.Configure(builder);
            AdsScopeRegistrator.Configure(builder);

            builder.RegisterAutoTypes();
            builder.RegisterConfigs();
            RegisterTimeProvider(builder);

            RegisterSceneComponents(builder);
        }

        // В плеере R3.Unity к этому моменту уже подменил DefaultTimeProvider на UnityTimeProvider.Update;
        // логика берёт время только через инжектируемый TimeProvider, чтобы тесты подставляли фейковый.
        private static void RegisterTimeProvider(IContainerBuilder builder)
        {
            builder.RegisterInstance<TimeProvider>(ObservableSystem.DefaultTimeProvider);
        }

        private void RegisterSceneComponents(IContainerBuilder builder)
        {
            var audioController = FindAnyObjectByType<AudioController>();
            if (audioController == null)
            {
                // Молчаливый скип откладывал бы fail до resolve потребителя — падаем сразу и внятно.
                throw new InvalidOperationException($"{nameof(RootScope)} prefab must contain {nameof(AudioController)}.");
            }

            builder.RegisterComponent(audioController).AsImplementedInterfaces();
        }
    }
}
