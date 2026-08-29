using System;
using Framework.Foundation.Ads;
using Framework.Foundation.Ads.Data;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class AdsPolicyTests
    {
        private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        private AdsData _data;

        [SetUp]
        public void Setup()
        {
            _data = new AdsData();
            _data.PrepareNewData();
        }

        private AdsPolicy CreatePolicy(
            bool interstitialEnabled = true,
            bool rewardedEnabled = true,
            bool bannerEnabled = true,
            int cooldownSeconds = 60,
            int sessionStartCooldownSeconds = 0,
            bool rewardedResetsInterstitialCooldown = true)
        {
            var config = FoundationTestConfigs.Ads(
                bannerEnabled: bannerEnabled,
                interstitialEnabled: interstitialEnabled,
                rewardedEnabled: rewardedEnabled,
                interstitialCooldownSeconds: cooldownSeconds,
                interstitialSessionStartCooldownSeconds: sessionStartCooldownSeconds,
                rewardedResetsInterstitialCooldown: rewardedResetsInterstitialCooldown);

            return new AdsPolicy(config, _data, Now);
        }

        [Test]
        public void IsAllowed_ReturnsTrue_WhenInterstitialWasNeverShown()
        {
            var policy = CreatePolicy();

            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now));
        }

        [Test]
        public void IsAllowed_ReturnsFalse_WhenInterstitialCooldownIsActive()
        {
            var policy = CreatePolicy(cooldownSeconds: 60);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(59)));
        }

        [Test]
        public void IsAllowed_ReturnsTrue_WhenInterstitialCooldownExpired()
        {
            var policy = CreatePolicy(cooldownSeconds: 60);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(60)));
        }

        [Test]
        public void IsAllowed_IgnoresCooldown_ForRewarded()
        {
            var policy = CreatePolicy(cooldownSeconds: 60);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Rewarded, Now));
        }

        [Test]
        public void GetCooldownLeft_ReturnsRemainingTime_WhileInterstitialCooldownIsActive()
        {
            var policy = CreatePolicy(cooldownSeconds: 60);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);

            Assert.AreEqual(
                TimeSpan.FromSeconds(20),
                policy.GetCooldownLeft(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(40)));
        }

        [Test]
        public void GetCooldownLeft_ReturnsZero_WhenCooldownExpired()
        {
            var policy = CreatePolicy(cooldownSeconds: 60);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);

            Assert.AreEqual(
                TimeSpan.Zero,
                policy.GetCooldownLeft(AdFormat.Interstitial, Now + TimeSpan.FromHours(1)));
        }

        [Test]
        public void RegisterShown_RestartsInterstitialCooldown_WhenRewardedResetIsEnabled()
        {
            var policy = CreatePolicy(cooldownSeconds: 60, rewardedResetsInterstitialCooldown: true);

            policy.RegisterShown(AdFormat.Rewarded, AdResult.Success, Now);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(30)));
            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(60)));
        }

        [Test]
        public void RegisterShown_KeepsInterstitialCooldown_WhenRewardedResetIsDisabled()
        {
            var policy = CreatePolicy(cooldownSeconds: 60, rewardedResetsInterstitialCooldown: false);

            policy.RegisterShown(AdFormat.Rewarded, AdResult.Success, Now);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now));
        }

        [Test]
        public void RegisterShown_DoesNotRestartInterstitialCooldown_WhenRewardedFailed()
        {
            var policy = CreatePolicy(cooldownSeconds: 60, rewardedResetsInterstitialCooldown: true);

            policy.RegisterShown(AdFormat.Rewarded, AdResult.Failed, Now);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now));
        }

        [Test]
        public void IsAllowed_ReturnsFalse_WhenFormatIsDisabledByConfig()
        {
            var policy = CreatePolicy(interstitialEnabled: false);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Interstitial, Now));
            Assert.IsTrue(policy.IsAllowed(AdFormat.Rewarded, Now));
        }

        [Test]
        public void IsAllowed_ReturnsFalse_WhenFormatIsDisabledAtRuntime()
        {
            var policy = CreatePolicy();

            policy.SetFormatEnabled(AdFormat.Rewarded, false);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Rewarded, Now));
        }

        [Test]
        public void SetFormatEnabled_DoesNotOverrideConfig_WhenFormatIsDisabledByConfig()
        {
            var policy = CreatePolicy(bannerEnabled: false);

            policy.SetFormatEnabled(AdFormat.Banner, true);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Banner, Now));
        }

        [Test]
        public void RegisterShown_IncrementsCounters_OnSuccessOnly()
        {
            var policy = CreatePolicy(cooldownSeconds: 0);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now);
            policy.RegisterShown(AdFormat.Interstitial, AdResult.Failed, Now);
            policy.RegisterShown(AdFormat.Interstitial, AdResult.Skipped, Now);
            policy.RegisterShown(AdFormat.Rewarded, AdResult.Success, Now);
            policy.RegisterShown(AdFormat.Rewarded, AdResult.NotReady, Now);

            Assert.AreEqual(1, _data.InterstitialWatched);
            Assert.AreEqual(1, _data.RewardedWatched);
        }

        [Test]
        public void RegisterShown_DoesNotCountBanner()
        {
            var policy = CreatePolicy();

            policy.RegisterShown(AdFormat.Banner, AdResult.Success, Now);

            Assert.AreEqual(0, _data.InterstitialWatched);
            Assert.AreEqual(0, _data.RewardedWatched);
        }

        [Test]
        public void IsAllowed_ReturnsFalse_WhileSessionStartCooldownIsActive()
        {
            var policy = CreatePolicy(sessionStartCooldownSeconds: 30);

            Assert.IsFalse(policy.IsAllowed(AdFormat.Interstitial, Now));
            Assert.IsFalse(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(29)));
        }

        [Test]
        public void IsAllowed_ReturnsTrue_WhenSessionStartCooldownExpired()
        {
            var policy = CreatePolicy(sessionStartCooldownSeconds: 30);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void IsAllowed_IgnoresSessionStartCooldown_ForOtherFormats()
        {
            var policy = CreatePolicy(sessionStartCooldownSeconds: 30);

            Assert.IsTrue(policy.IsAllowed(AdFormat.Rewarded, Now));
            Assert.IsTrue(policy.IsAllowed(AdFormat.Banner, Now));
        }

        [Test]
        public void GetCooldownLeft_UsesLaterDeadline_WhenShownInsideSessionStartWindow()
        {
            var policy = CreatePolicy(cooldownSeconds: 60, sessionStartCooldownSeconds: 300);

            // Показ внутри session-start окна не должен укоротить его: дедлайн — позднейший из двух.
            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now + TimeSpan.FromSeconds(10));

            Assert.AreEqual(
                TimeSpan.FromSeconds(290),
                policy.GetCooldownLeft(AdFormat.Interstitial, Now + TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void GetCooldownLeft_UsesShowCooldown_WhenItEndsLater()
        {
            var policy = CreatePolicy(cooldownSeconds: 60, sessionStartCooldownSeconds: 30);

            policy.RegisterShown(AdFormat.Interstitial, AdResult.Success, Now + TimeSpan.FromMinutes(5));

            Assert.AreEqual(
                TimeSpan.FromSeconds(60),
                policy.GetCooldownLeft(AdFormat.Interstitial, Now + TimeSpan.FromMinutes(5)));
        }
    }
}
