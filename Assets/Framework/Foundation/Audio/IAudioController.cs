using Framework.Foundation.Utilities;

namespace Framework.Foundation.Audio
{
    public interface IAudioController : IEntityStatus
    {
        public void PlaySound(string audioClipName);
        public void SetSoundsVolume(float volume);
        public void PlayMusic(string audioClipName);
        public void SetMusicVolume(float volume);

        /// Глушит весь звук игры целиком — например, на время показа рекламы. Именно mute,
        /// а не выставление громкостей: геттеров громкости в контракте нет, восстанавливать нечего.
        public void SetMuted(bool isMuted);
    }
}
