using System;
using Framework.Foundation.Signals;

namespace Framework.Foundation.Scenes.Signals
{
    /// Провал самой загрузки сцены: фазы новой сцены не начнутся, поэтому SceneStartedSignal
    /// не придёт и шторку нужно снимать по этому сигналу.
    public class SceneLoadFailedSignal : ISignal
    {
        public string SceneName { get; }
        public Exception Exception { get; }

        public SceneLoadFailedSignal(string sceneName, Exception exception)
        {
            SceneName = sceneName;
            Exception = exception;
        }
    }
}
