using System;
using Framework.Features.SaveLoad;
using Framework.Foundation.SaveLoad;
using MemoryPack;

namespace Framework.Features.Settings.Data
{
    [SaveTag(FeaturesSaveTags.SettingsData)]
    [MemoryPackable]
    public partial class SettingsData : Framework.Foundation.SaveLoad.SaveBlob
    {
        public float SoundsVolume { get; private set; }
        public float MusicVolume { get; private set; }

        public override void PrepareNewData()
        {
            SoundsVolume = SettingsConstants.Parameters.DefaultSoundsVolume;
            MusicVolume = SettingsConstants.Parameters.DefaultMusicVolume;
        }

        public void SetSoundsVolumeData(float value)
        {
            var roundedValue = Math.Round(value, 2);
            SoundsVolume = (float)roundedValue;
        }

        public void SetMusicVolumeData(float value)
        {
            var roundedValue = Math.Round(value, 2);
            MusicVolume = (float)roundedValue;
        }
    }
}
