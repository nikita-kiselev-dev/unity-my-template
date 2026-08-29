using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace Framework.Foundation.Audio
{
    public class MusicPlayer : AudioSourcePlayer
    {
        private Sequence _switchSequence;

        public async UniTaskVoid Play(
            string audioClipName,
            bool isLooped = true,
            bool isPersistent = true)
        {
            var isNewAudioClipAlreadyPlaying = m_AudioSource.isPlaying && audioClipName == m_AudioSource.clip.name;

            if (isNewAudioClipAlreadyPlaying)
            {
                return;
            }

            var audioClip = await GetAudio(audioClipName, isPersistent);

            if (m_AudioSource.isPlaying)
            {
                Switch(audioClip, isLooped);
            }
            else
            {
                PlayMusic(audioClip, isLooped);
            }
        }

        private void PlayMusic(AudioClip audioClip, bool isLooped = true)
        {
            m_AudioSource.clip = audioClip;
            m_AudioSource.loop = isLooped;
            m_AudioSource.Play();
        }

        private void Switch(AudioClip audioClip, bool isLooped = true)
        {
            _switchSequence.Complete();
            _switchSequence = Sequence
                .Create()
                .Chain(Tween.AudioVolume(m_AudioSource, 0, AudioConstants.Parameters.MusicFadeOutDuration))
                .ChainCallback(target: this, target => target.PlayMusic(audioClip, isLooped))
                .Chain(Tween.AudioVolume(m_AudioSource, 1.0f, AudioConstants.Parameters.MusicFadeInDuration));
        }
    }
}