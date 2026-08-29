using System.Collections.Generic;
using Framework.Foundation.Audio;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeAudioController : IAudioController
    {
        public List<float> SoundsVolumes { get; } = new();
        public List<float> MusicVolumes { get; } = new();
        public List<bool> MuteCalls { get; } = new();

        public EntityStatus Status { get; } = new(nameof(FakeAudioController));

        public bool IsEnabled => Status.IsEnabled;
        public bool IsInited => Status.IsInited;
        public bool IsActive => Status.IsActive;

        public void PlaySound(string audioClipName)
        {
        }

        public void SetSoundsVolume(float volume) => SoundsVolumes.Add(volume);

        public void PlayMusic(string audioClipName)
        {
        }

        public void SetMusicVolume(float volume) => MusicVolumes.Add(volume);

        public void SetMuted(bool isMuted) => MuteCalls.Add(isMuted);
    }
}
