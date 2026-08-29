using System.Collections.Generic;
using Framework.Features.Settings;
using Framework.Features.Settings.Data;
using Framework.Features.Settings.Model;
using NUnit.Framework;
using R3;

namespace Framework.Features.Tests
{
    public class SettingsModelTests
    {
        private const float Precision = 1e-6f;

        private static SettingsModel CreateModel(out SettingsData data)
        {
            data = new SettingsData();
            data.PrepareNewData();
            return new SettingsModel(data);
        }

        [Test]
        public void Constructor_ExposesDefaultVolumes()
        {
            var model = CreateModel(out _);

            Assert.AreEqual(SettingsConstants.Parameters.DefaultSoundsVolume, model.SoundsVolume.CurrentValue, Precision);
            Assert.AreEqual(SettingsConstants.Parameters.DefaultMusicVolume, model.MusicVolume.CurrentValue, Precision);
        }

        [Test]
        public void SetSoundsVolume_RoundsToTwoDigits()
        {
            var model = CreateModel(out var data);

            model.SetSoundsVolume(0.12345f);

            Assert.AreEqual(0.12f, model.SoundsVolume.CurrentValue, Precision);
            Assert.AreEqual(0.12f, data.SoundsVolume, Precision);
        }

        [Test]
        public void SetMusicVolume_EmitsRoundedValueToStream()
        {
            var model = CreateModel(out _);
            var received = new List<float>();
            using var subscription = model.MusicVolume.Subscribe(received.Add);

            model.SetMusicVolume(0.456f);

            Assert.AreEqual(0.46f, received[^1], Precision);
        }
    }
}
