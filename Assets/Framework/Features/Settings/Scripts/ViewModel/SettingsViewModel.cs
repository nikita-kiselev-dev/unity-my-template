using Framework.Features.Settings.Model;
using Framework.Foundation.Audio;
using R3;

namespace Framework.Features.Settings.ViewModel
{
    public class SettingsViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        private readonly SettingsModel _model;

        public ReadOnlyReactiveProperty<float> SoundsVolume => _model.SoundsVolume;
        public ReadOnlyReactiveProperty<float> MusicVolume => _model.MusicVolume;

        public SettingsViewModel(SettingsModel model, IAudioController audioController)
        {
            _model = model;
            _model.AddTo(ref Subscriptions);

            // ReactiveProperty реплеит текущее значение при подписке —
            // эта же связка применяет сохранённую громкость на старте.
            _model.SoundsVolume.Subscribe(audioController.SetSoundsVolume).AddTo(ref Subscriptions);
            _model.MusicVolume.Subscribe(audioController.SetMusicVolume).AddTo(ref Subscriptions);
        }

        public void SetSoundsVolume(float volume) => _model.SetSoundsVolume(volume);
        public void SetMusicVolume(float volume) => _model.SetMusicVolume(volume);
    }
}
