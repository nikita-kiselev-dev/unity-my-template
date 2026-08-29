using Framework.Foundation.Initialization.Extensions;
using Framework.Foundation.LiveOps.Offline;
using VContainer;

namespace Framework.Foundation.Initialization.Registrators.LiveOps
{
    public static partial class LiveOpsScopeRegistrator
    {
        public static void Configure(IContainerBuilder builder)
        {
            RegisterOfflineDefaults(builder);
            RegisterPlatform(builder);
        }

        private static void RegisterOfflineDefaults(IContainerBuilder builder)
        {
            builder.RegisterSingleton<EmptyRemoteConfigSource>();
            builder.RegisterSingleton<LocalServerTimeSource>();
            builder.RegisterSingleton<OfflineServerConnectionService>();
        }

        static partial void RegisterPlatform(IContainerBuilder builder);
    }
}