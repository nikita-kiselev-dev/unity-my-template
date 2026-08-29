using System;
using System.Collections.Generic;
using Framework.Foundation.Scenes;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeSceneService : ISceneService
    {
        public List<string> LoadedScenes { get; } = new();

        public bool LoadScene(string sceneName, Action onLoaded = null)
        {
            LoadedScenes.Add(sceneName);
            onLoaded?.Invoke();
            return true;
        }
    }
}
