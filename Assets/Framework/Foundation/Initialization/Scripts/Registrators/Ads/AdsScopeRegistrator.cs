using Framework.Foundation.Ads.Providers;
using Framework.Foundation.Initialization.Extensions;
using VContainer;
#if UNITY_EDITOR
using Framework.Foundation.Ads.Stub;
#endif

namespace Framework.Foundation.Initialization.Registrators.Ads
{
    /// <summary>
    /// Выбирает единственную активную реализацию <c>IAdsProvider</c>: платформенный адаптер под
    /// define → заглушка редактора → заглушка-пустышка. Регистратор, а не <c>[AutoRegistration]</c>
    /// на каждом провайдере: два типа под одним интерфейсом дали бы недетерминированный резолв.
    /// </summary>
    public static partial class AdsScopeRegistrator
    {
        public static void Configure(IContainerBuilder builder)
        {
            var registered = false;
            RegisterPlatform(builder, ref registered);

            if (registered)
            {
                return;
            }

#if UNITY_EDITOR
            builder.RegisterSingleton<EditorAdsProvider>();
#else
            builder.RegisterSingleton<NullAdsProvider>();
#endif
        }

        static partial void RegisterPlatform(IContainerBuilder builder, ref bool registered);
    }
}
