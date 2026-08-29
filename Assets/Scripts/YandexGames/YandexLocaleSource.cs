#if UNITY_WEBGL && PLUGIN_YG_2 && Localization_yg
using Framework.Foundation.Initialization;
using Framework.Foundation.Localization;
using Framework.Foundation.Utilities;
using VContainer;
using YG;

namespace YandexGames
{
    /// <summary>
    /// Язык аккаунта игрока из YG SDK. Ждать готовность SDK здесь не нужно: её держит
    /// <c>YandexSdkEntity</c> в фазе <c>Load</c>, а источник опрашивают в <c>Init</c>.
    /// </summary>
    [AutoRegistration(Lifetime.Singleton)]
    public class YandexLocaleSource : ILocaleSource
    {
        public Result<string> TryGetLocaleCode()
        {
            return YG2.isSDKEnabled
                ? Result<string>.Success(YG2.lang)
                : Result<string>.Failure();
        }
    }
}
#endif
