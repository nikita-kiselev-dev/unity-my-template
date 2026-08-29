using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.Foundation.Audio
{
    public class AudioSourcePlayer : MonoBehaviour
    {
        [SerializeField] private protected AudioSource m_AudioSource;
        
        private readonly Dictionary<string, AudioClip> _audioList = new();
        private IAudioClipLoader _audioLoader;

        public void Init(IAudioClipLoader audioLoader)
        {
            _audioLoader = audioLoader;
        }
        
        public void Pause()
        {
            if (m_AudioSource.isPlaying)
            {
                m_AudioSource.Pause();
            }
        }

        public void Stop()
        {
            if (m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
            }
        }

        public void SetVolume(float volume)
        {
            m_AudioSource.volume = volume;
        }
        
        private protected async UniTask<AudioClip> GetAudio(string audioClipName, bool persistent = false)
        {
            if (_audioList.TryGetValue(audioClipName, out var audioClip) && audioClip)
            {
                return audioClip;
            }

            _audioList.Remove(audioClipName);
            audioClip = await _audioLoader.LoadAudio(audioClipName, persistent);

            // Не-persistent клип провайдер освободит по шторке: запись в кэше превратилась бы
            // в fake-null и молча перезагружала бы ассет на каждой сцене.
            if (persistent)
            {
                _audioList[audioClipName] = audioClip;
            }

            return audioClip;
        }
    }
}