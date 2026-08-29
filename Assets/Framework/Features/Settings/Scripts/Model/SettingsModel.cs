using System;
using Framework.Features.Settings.Data;
using R3;

namespace Framework.Features.Settings.Model
{
    public class SettingsModel : IDisposable
    {
        private readonly SettingsData _data;
        private readonly ReactiveProperty<float> _soundsVolume;
        private readonly ReactiveProperty<float> _musicVolume;

        public ReadOnlyReactiveProperty<float> SoundsVolume => _soundsVolume;
        public ReadOnlyReactiveProperty<float> MusicVolume => _musicVolume;

        public SettingsModel(SettingsData data)
        {
            _data = data;
            _soundsVolume = new ReactiveProperty<float>(data.SoundsVolume);
            _musicVolume = new ReactiveProperty<float>(data.MusicVolume);
        }

        public void SetSoundsVolume(float volume)
        {
            _data.SetSoundsVolumeData(volume);
            // Data округляет значение — в стрим уходит то, что реально сохранится.
            _soundsVolume.Value = _data.SoundsVolume;
        }

        public void SetMusicVolume(float volume)
        {
            _data.SetMusicVolumeData(volume);
            _musicVolume.Value = _data.MusicVolume;
        }

        public void Dispose()
        {
            _soundsVolume.Dispose();
            _musicVolume.Dispose();
        }
    }
}
