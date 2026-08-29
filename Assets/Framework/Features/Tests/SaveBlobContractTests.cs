using System;
using Framework.Features.Clicker.Data;
using Framework.Foundation.Ads;
using Framework.Foundation.Ads.Data;
using Framework.Features.DailyBonus.Data;
using Framework.Features.Settings.Data;
using MemoryPack;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    // Контракт сейвов: MemoryPack восстанавливает состояние в существующий инстанс —
    // так же, как это делает SaveEnvelope.Deserialize.
    public class SaveBlobContractTests
    {
        private static TData Roundtrip<TData>(TData source)
            where TData : Framework.Foundation.SaveLoad.SaveBlob, new()
        {
            var bytes = MemoryPackSerializer.Serialize(typeof(TData), source);
            var target = new TData();
            object refValue = target;
            MemoryPackSerializer.Deserialize(typeof(TData), bytes, ref refValue);

            Assert.AreSame(target, refValue, "MemoryPack должен заполнять существующий инстанс, а не создавать новый.");
            return (TData)refValue;
        }

        [Test]
        public void ClickerData_Roundtrip_PreservesState()
        {
            var source = new ClickerData();
            source.PrepareNewData();
            source.OnClick();
            source.OnClick();
            source.OnClick();
            source.Upgrade();

            var restored = Roundtrip(source);

            Assert.AreEqual(3, restored.ClickCount);
            Assert.AreEqual(1, restored.Level);
        }

        [Test]
        public void DailyBonusData_Roundtrip_PreservesState()
        {
            var source = new DailyBonusData();
            source.PrepareNewData();
            source.AddStreakDayData();
            source.AddStreakDayData();
            source.SetLastRewardDate(new DateTime(2026, 7, 10, 12, 0, 0));

            var restored = Roundtrip(source);

            Assert.AreEqual(3, restored.StreakDay);
            Assert.AreEqual(new DateTime(2026, 7, 10, 12, 0, 0), restored.LastRewardDate);
        }

        [Test]
        public void AdsData_Roundtrip_PreservesState()
        {
            var source = new AdsData();
            source.PrepareNewData();
            source.RegisterShown(AdFormat.Interstitial);
            source.RegisterShown(AdFormat.Rewarded);
            source.RegisterShown(AdFormat.Rewarded);

            var restored = Roundtrip(source);

            Assert.AreEqual(1, restored.InterstitialWatched);
            Assert.AreEqual(2, restored.RewardedWatched);
        }

        [Test]
        public void SettingsPopupData_Roundtrip_PreservesState()
        {
            var source = new SettingsData();
            source.PrepareNewData();
            source.SetSoundsVolumeData(0.25f);
            source.SetMusicVolumeData(0.5f);

            var restored = Roundtrip(source);

            Assert.AreEqual(0.25f, restored.SoundsVolume, 1e-6f);
            Assert.AreEqual(0.5f, restored.MusicVolume, 1e-6f);
        }
    }
}
