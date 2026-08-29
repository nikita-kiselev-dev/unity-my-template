using Framework.Foundation.Ads.Configs;
using Newtonsoft.Json;

namespace Framework.Foundation.Tests
{
    // Конфиги заполняются Newtonsoft-ом из JSON (поля приватные) — в тестах строим их так же, как прод.
    internal static class FoundationTestConfigs
    {
        public static AdsConfig Ads(
            bool isEnabled = true,
            bool bannerEnabled = true,
            bool interstitialEnabled = true,
            bool rewardedEnabled = true,
            int interstitialCooldownSeconds = 60,
            int interstitialSessionStartCooldownSeconds = 0,
            bool rewardedResetsInterstitialCooldown = true)
        {
            return JsonConvert.DeserializeObject<AdsConfig>(
                $"{{\"is_enabled\":{Bool(isEnabled)}," +
                $"\"banner_enabled\":{Bool(bannerEnabled)}," +
                $"\"interstitial_enabled\":{Bool(interstitialEnabled)}," +
                $"\"rewarded_enabled\":{Bool(rewardedEnabled)}," +
                $"\"interstitial_cooldown_seconds\":{interstitialCooldownSeconds}," +
                $"\"interstitial_session_start_cooldown_seconds\":{interstitialSessionStartCooldownSeconds}," +
                $"\"rewarded_resets_interstitial_cooldown\":{Bool(rewardedResetsInterstitialCooldown)}}}");
        }

        private static string Bool(bool value) => value ? "true" : "false";
    }
}
