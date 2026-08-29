using System;
using Framework.Foundation.Configs;
using Newtonsoft.Json;

namespace Framework.Foundation.Ads.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigKey(AdsConstants.Configs.Key)]
    public class AdsConfig : IAdsConfig
    {
        [JsonProperty("is_enabled")] private bool _isEnabled;
        [JsonProperty("banner_enabled")] private bool _bannerEnabled;
        [JsonProperty("interstitial_enabled")] private bool _interstitialEnabled;
        [JsonProperty("rewarded_enabled")] private bool _rewardedEnabled;
        [JsonProperty("interstitial_cooldown_seconds")] private int _interstitialCooldownSeconds;
        [JsonProperty("interstitial_session_start_cooldown_seconds")] private int _interstitialSessionStartCooldownSeconds;
        [JsonProperty("rewarded_resets_interstitial_cooldown")] private bool _rewardedResetsInterstitialCooldown;

        public bool IsEnabled => _isEnabled;
        public bool BannerEnabled => _bannerEnabled;
        public bool InterstitialEnabled => _interstitialEnabled;
        public bool RewardedEnabled => _rewardedEnabled;
        public TimeSpan InterstitialCooldown => TimeSpan.FromSeconds(_interstitialCooldownSeconds);
        public TimeSpan InterstitialSessionStartCooldown => TimeSpan.FromSeconds(_interstitialSessionStartCooldownSeconds);
        public bool RewardedResetsInterstitialCooldown => _rewardedResetsInterstitialCooldown;
    }
}
