#if UNITY_WEBGL && !UNITY_EDITOR && PLUGIN_YG_2 && InterstitialAdv_yg && RewardedAdv_yg
using VContainer;

namespace Framework.Foundation.Initialization.Registrators.Ads
{
    public static partial class AdsScopeRegistrator
    {
        /// <summary>
        /// YandexAdsProvider живёт в Assembly-CSharp (там объявлен YG2) и регистрирует себя сам
        /// через [AutoRegistration]: сослаться на него отсюда нельзя. Регистратору остаётся снять
        /// заглушки — иначе под IAdsProvider оказалось бы два типа.
        /// </summary>
        static partial void RegisterPlatform(IContainerBuilder builder, ref bool registered)
        {
            registered = true;
        }
    }
}
#endif
