using Framework.Features.Settings;
using Framework.Features.Settings.Data;
using Framework.Features.Settings.Model;
using Framework.Features.Settings.ViewModel;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    public class SettingsViewModelTests
    {
        private const float Precision = 1e-6f;

        private static SettingsViewModel CreateViewModel(FakeAudioController audio)
        {
            var data = new SettingsData();
            data.PrepareNewData();
            return new SettingsViewModel(new SettingsModel(data), audio);
        }

        [Test]
        public void Constructor_AppliesSavedVolumesToAudio()
        {
            var audio = new FakeAudioController();

            CreateViewModel(audio);

            Assert.AreEqual(1, audio.SoundsVolumes.Count);
            Assert.AreEqual(SettingsConstants.Parameters.DefaultSoundsVolume, audio.SoundsVolumes[0], Precision);
            Assert.AreEqual(1, audio.MusicVolumes.Count);
        }

        [Test]
        public void SetSoundsVolume_ForwardsRoundedValueToAudio()
        {
            var audio = new FakeAudioController();
            var viewModel = CreateViewModel(audio);

            viewModel.SetSoundsVolume(0.333f);

            Assert.AreEqual(0.33f, audio.SoundsVolumes[^1], Precision);
            Assert.AreEqual(0.33f, viewModel.SoundsVolume.CurrentValue, Precision);
        }

        [Test]
        public void Dispose_StopsForwardingToAudio()
        {
            var audio = new FakeAudioController();
            var viewModel = CreateViewModel(audio);

            viewModel.Dispose();

            Assert.AreEqual(1, audio.SoundsVolumes.Count);
            Assert.AreEqual(1, audio.MusicVolumes.Count);
        }
    }
}
