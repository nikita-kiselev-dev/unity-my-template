using Cysharp.Threading.Tasks;
using Framework.Foundation.Utilities;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.Audio
{
    public class AudioController : MonoBehaviour, IAudioController
    {
        [Inject] private readonly IAudioClipLoader _audioLoader;

        [SerializeField] private SoundPlayer m_SoundSource;
        [SerializeField] private MusicPlayer m_MusicSource;

        private bool _soundVolumeSet;
        private bool _musicVolumeSet;

        public EntityStatus Status { get; } = new(nameof(AudioController));

        bool IReadOnlyEntityStatus.IsEnabled => Status.IsEnabled;
        bool IReadOnlyEntityStatus.IsInited => Status.IsInited;
        bool IReadOnlyEntityStatus.IsActive => Status.IsActive;

        public void PlaySound(string audioClipName)
        {
            PlayAndForgetSound(audioClipName).Forget();
        }

        public void PlayMusic(string audioClipName)
        {
            PlayAndForgetMusic(audioClipName).Forget();
        }

        public void SetSoundsVolume(float volume)
        {
            m_SoundSource.SetVolume(volume);
            _soundVolumeSet = true;
        }

        public void SetMusicVolume(float volume)
        {
            m_MusicSource.SetVolume(volume);
            _musicVolumeSet = true;
        }

        public void SetMuted(bool isMuted)
        {
            AudioListener.pause = isMuted;
        }

        public UniTask<AudioClip> LoadAudio(string audioClipName, bool persistent = false)
        {
            return _audioLoader.LoadAudio(audioClipName, persistent);
        }

        private async UniTaskVoid PlayAndForgetSound(string audioClipName)
        {
            await UniTask.WaitUntil(() => _soundVolumeSet, cancellationToken: destroyCancellationToken);
            m_SoundSource.Play(audioClipName).Forget();
        }

        private async UniTaskVoid PlayAndForgetMusic(string audioClipName)
        {
            await UniTask.WaitUntil(() => _musicVolumeSet, cancellationToken: destroyCancellationToken);
            m_MusicSource.Play(audioClipName).Forget();
        }

        private void Awake()
        {
            if (Status.IsInited)
            {
                return;
            }

            DontDestroyOnLoad(this);

            m_SoundSource.Init(_audioLoader);
            m_MusicSource.Init(_audioLoader);

            Status
                .SetEnabled(true)
                .SetInited(true);
        }

        private void OnDestroy()
        {
            Status.Dispose();
        }
    }
}
