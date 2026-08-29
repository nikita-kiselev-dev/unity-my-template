using System;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Scenes
{
    public interface ISceneLoader
    {
        bool PrepareSceneLoad(string sceneName, Action onSceneLoadedCallback = null);
        UniTask LoadAsync();
    }
}
