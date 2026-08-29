#if !PLAYER_PREFS_SAVE_ENABLED && !FILE_SAVE_ENABLED
#error "Define either PLAYER_PREFS_SAVE_ENABLED or FILE_SAVE_ENABLED."
#endif

using VContainer;

namespace Framework.Foundation.Initialization.Registrators.Data
{
    public static partial class DataScopeRegistrator
    {
        public static void Configure(IContainerBuilder builder)
        {
            RegisterPlatform(builder);
        }

        static partial void RegisterPlatform(IContainerBuilder builder);
    }
}