using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.Initialization;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.Audio
{
    [AutoRegistration(Lifetime.Singleton)]
    public class AudioClipLoader : IAudioClipLoader
    {
        [Inject] private readonly IAssetProvider _assetProvider;
        
        public async UniTask<AudioClip> LoadAudio(string audioClipName, bool persistent = false)
        {
            var audioClip = await _assetProvider.LoadAssetAsync<AudioClip>(audioClipName, persistent);
            return audioClip ? audioClip : throw new ArgumentNullException($"{GetType().Name}: can't load audio clip with name {audioClipName}!");
        }
    }
}