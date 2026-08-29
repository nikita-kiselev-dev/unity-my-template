using System;
using Framework.Foundation.Configs;
using Framework.Foundation.Logger;
using Framework.Foundation.SaveLoad;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.Initialization.Extensions
{
    public static class VContainerBuilderExtensions
    {
        private static readonly Type _dataType = typeof(SaveBlob);
        private static AutoTypeScanResult _scanCache;

        public static void RegisterSingleton<T>(this IContainerBuilder containerBuilder)
        {
            containerBuilder.Register<T>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        }

        /// <summary>
        /// Lifetime.Scoped в root scope означает «инстанс на сценовый scope, умирает со сценой».
        /// </summary>
        public static void RegisterScoped<T>(this IContainerBuilder containerBuilder)
        {
            containerBuilder.Register<T>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();
        }

        public static void AsLifecycleEntity(this RegistrationBuilder registrationBuilder)
        {
            registrationBuilder.As<LifecycleEntity>().AsImplementedInterfaces();
        }

        /// <summary>
        /// Без handler-а исключение из entry point (SceneStarter) уходит в дефолтный лог VContainer
        /// мимо ILogChannel. Регистрировать в каждом scope с RegisterEntryPoint.
        /// </summary>
        public static void RegisterEntryPointLogging(this IContainerBuilder builder)
        {
            // Handler ставится на этапе регистрации: резолвить ILogChannelFactory здесь нечем,
            // контейнер ещё не собран.
            var logger = new LogChannel<SceneStarter>();
            builder.RegisterEntryPointExceptionHandler(exception => logger.LogError(exception.ToString()));
        }

        /// <summary>
        /// Регистрирует типы с [AutoRegistration] и все конкретные наследники SaveBlob.
        /// </summary>
        public static void RegisterAutoTypes(this IContainerBuilder builder)
        {
            builder.RegisterAutoTypes(EnsureScanned());
        }

        internal static void RegisterAutoTypes(this IContainerBuilder builder, AutoTypeScanResult scan)
        {
            foreach (var entry in scan.AutoTypes)
            {
                switch (entry.Kind)
                {
                    case AutoTypeKind.LifecycleEntity:
                        builder.Register(entry.Type, entry.Lifetime).AsLifecycleEntity();
                        break;
                    case AutoTypeKind.SaveBlob:
                        builder.Register(entry.Type, Lifetime.Singleton).As(_dataType).AsSelf();
                        break;
                    default:
                        builder.Register(entry.Type, entry.Lifetime).AsSelf().AsImplementedInterfaces();
                        break;
                }
            }
        }

        /// <summary>
        /// Регистрирует конкретные IConfig с [ConfigKeyAttribute] как обычные зависимости контейнера.
        /// Инстансы отдаёт IConfigProvider, поэтому его WarmUp обязан завершиться до резолва потребителей.
        /// </summary>
        public static void RegisterConfigs(this IContainerBuilder builder)
        {
            builder.RegisterConfigs(EnsureScanned());
        }

        internal static void RegisterConfigs(this IContainerBuilder builder, AutoTypeScanResult scan)
        {
            var entries = scan.Configs;
            builder.Register<IConfigProvider>(
                resolver => new ConfigProvider(resolver.Resolve<IConfigReader>(), entries),
                Lifetime.Singleton);

            foreach (var entry in entries)
            {
                builder.Register(new ConfigRegistrationBuilder(entry.ConfigType)).AsSelf().AsImplementedInterfaces();
            }
        }

        private static AutoTypeScanResult EnsureScanned()
        {
            return _scanCache ??= AutoTypeScanner.Scan(AppDomain.CurrentDomain.GetAssemblies());
        }
    }
}
