using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.Foundation.Audio
{
    public interface IAudioClipLoader
    {
        public UniTask<AudioClip> LoadAudio(string audioClipName, bool persistent = false);
    }
}