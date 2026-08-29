using VContainer;

namespace Framework.Foundation.Initialization.Registrators.LiveOps
{
    public static partial class LiveOpsScopeRegistrator
    {
        static partial void RegisterPlatform(IContainerBuilder builder)
        {
#if GAMEPUSH_ENABLED
            // Register GamePush-specific LiveOps services here.
#endif
        }
    }
}
