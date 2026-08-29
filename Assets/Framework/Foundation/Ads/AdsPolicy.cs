using System;
using System.Collections.Generic;
using Framework.Foundation.Ads.Data;
using Framework.Foundation.Ads.Configs;

namespace Framework.Foundation.Ads
{
    /// <summary>
    /// Правила показа без Unity и без времени внутри: доступность формата, кулдаун и учёт
    /// просмотров. Время приходит параметром, поэтому тесты не зависят от часов.
    /// </summary>
    internal sealed class AdsPolicy
    {
        private readonly IAdsConfig _config;
        private readonly AdsData _data;
        private readonly DateTime _sessionStartUtc;
        private readonly HashSet<AdFormat> _runtimeDisabled = new();

        // Время последнего показа живёт только в рантайме: между сессиями его заменяет
        // session-start кулдаун, поэтому в сейве ему делать нечего.
        private DateTime _lastInterstitialUtc = DateTime.MinValue;

        /// Позже из двух дедлайнов: кулдаун после предыдущего показа и кулдаун от старта сессии.
        public DateTime InterstitialDeadlineUtc
        {
            get
            {
                var afterLastShow = _lastInterstitialUtc + _config.InterstitialCooldown;
                var afterSessionStart = _sessionStartUtc + _config.InterstitialSessionStartCooldown;
                return afterLastShow > afterSessionStart ? afterLastShow : afterSessionStart;
            }
        }

        public AdsPolicy(IAdsConfig config, AdsData data, DateTime sessionStartUtc)
        {
            _config = config;
            _data = data;
            _sessionStartUtc = sessionStartUtc;
        }

        public void SetFormatEnabled(AdFormat format, bool enabled)
        {
            if (enabled)
            {
                _runtimeDisabled.Remove(format);
                return;
            }

            _runtimeDisabled.Add(format);
        }

        // Рантайм-флаг может только запретить формат: конфиг сильнее.
        public bool IsFormatEnabled(AdFormat format)
        {
            return IsEnabledByConfig(format) && !_runtimeDisabled.Contains(format);
        }

        public bool IsAllowed(AdFormat format, DateTime utcNow)
        {
            return IsFormatEnabled(format) && GetCooldownLeft(format, utcNow) <= TimeSpan.Zero;
        }

        public TimeSpan GetCooldownLeft(AdFormat format, DateTime utcNow)
        {
            if (format != AdFormat.Interstitial)
            {
                return TimeSpan.Zero;
            }

            var left = InterstitialDeadlineUtc - utcNow;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        public void RegisterShown(AdFormat format, AdResult result, DateTime utcNow)
        {
            if (result != AdResult.Success)
            {
                return;
            }

            _data.RegisterShown(format);

            if (format == AdFormat.Interstitial ||
                (format == AdFormat.Rewarded && _config.RewardedResetsInterstitialCooldown))
            {
                _lastInterstitialUtc = utcNow;
            }
        }

        private bool IsEnabledByConfig(AdFormat format)
        {
            return format switch
            {
                AdFormat.Banner => _config.BannerEnabled,
                AdFormat.Interstitial => _config.InterstitialEnabled,
                AdFormat.Rewarded => _config.RewardedEnabled,
                _ => false
            };
        }
    }
}
