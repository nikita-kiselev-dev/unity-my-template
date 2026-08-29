using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Audio
{
    public class SoundPlayer : AudioSourcePlayer
    {
        public async UniTaskVoid Play(string audioClipName)
        {
            var audioClip = await GetAudio(audioClipName);
            m_AudioSource.PlayOneShot(audioClip);
        }
    }
}