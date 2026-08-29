using System;

namespace Framework.Foundation.Scenes
{
    public interface ISceneService
    {
        bool LoadScene(string sceneName, Action onLoaded = null);
    }
}
