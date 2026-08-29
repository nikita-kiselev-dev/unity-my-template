using Framework.Foundation.SaveLoad;
using MemoryPack;

namespace Framework.Foundation.Ads.Data
{
    /// В сейве живут только счётчики просмотров: время последнего показа нужно кулдауну
    /// внутри сессии, а на новой сессии interstitial всё равно ждёт session-start кулдаун.
    [SaveTag(FoundationSaveTags.AdsData)]
    [MemoryPackable]
    public partial class AdsData : global::Framework.Foundation.SaveLoad.SaveBlob
    {
        public int InterstitialWatched { get; private set; }
        public int RewardedWatched { get; private set; }

        // v1 хранил ещё и даты показов: схема сузилась, старый payload не читается —
        // рубеж поднят, чтобы такой сейв сбрасывал только рекламу.
        public override ushort CurrentVersion => 2;
        public override ushort MinReadableVersion => 2;

        public override void PrepareNewData()
        {
            InterstitialWatched = 0;
            RewardedWatched = 0;
        }

        public void RegisterShown(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Interstitial:
                    InterstitialWatched++;
                    break;
                case AdFormat.Rewarded:
                    RewardedWatched++;
                    break;
            }
        }
    }
}
