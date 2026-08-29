using System;
using Framework.Foundation.Signals;

namespace Framework.Foundation.Initialization.Signals
{
    public class SceneStartFailedSignal : ISignal
    {
        public string SceneName { get; }
        public Exception Exception { get; }

        public SceneStartFailedSignal(string sceneName, Exception exception)
        {
            SceneName = sceneName;
            Exception = exception;
        }
    }
}
