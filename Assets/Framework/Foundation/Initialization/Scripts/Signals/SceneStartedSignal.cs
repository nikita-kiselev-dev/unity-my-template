using Framework.Foundation.Signals;

namespace Framework.Foundation.Initialization.Signals
{
    public class SceneStartedSignal : ISignal
    {
        public string SceneName { get; }

        public SceneStartedSignal(string sceneName)
        {
            SceneName = sceneName;
        }
    }
}
