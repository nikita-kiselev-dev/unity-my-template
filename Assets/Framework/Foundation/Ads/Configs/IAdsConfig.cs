using System;
using Framework.Foundation.Configs;

namespace Framework.Foundation.Ads.Configs
{
    public interface IAdsConfig : IConfig
    {
        bool BannerEnabled { get; }
        bool InterstitialEnabled { get; }
        bool RewardedEnabled { get; }

        /// Кулдаун между показами interstitial.
        TimeSpan InterstitialCooldown { get; }

        /// Отдельный кулдаун от старта сессии: первые секунды игры interstitial не показывается.
        TimeSpan InterstitialSessionStartCooldown { get; }

        /// Успешный rewarded перезапускает кулдаун interstitial: игрок, только что посмотревший
        /// рекламу добровольно, не должен сразу получить принудительную.
        bool RewardedResetsInterstitialCooldown { get; }
    }
}
