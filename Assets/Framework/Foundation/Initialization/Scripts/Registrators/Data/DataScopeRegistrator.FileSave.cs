#if FILE_SAVE_ENABLED
using Framework.Foundation.Configs;
using Framework.Foundation.Initialization.Extensions;
using Framework.Foundation.SaveLoad;
using VContainer;

namespace Framework.Foundation.Initialization.Registrators.Data
{
    public static partial class DataScopeRegistrator
    {
        static partial void RegisterPlatform(IContainerBuilder builder)
        {
            builder.Register<SaveLoadService>(Lifetime.Singleton).AsLifecycleEntity();
            builder.Register<FileSaveStorage>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FileConfigStorage>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterSingleton<ConfigReader>();
        }
    }
}
#endif
