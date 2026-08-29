using Framework.Foundation.Signals;

namespace Framework.Foundation.Initialization.Signals
{
    public class SceneLoadingProgressSignal : ISignal
    {
        public SceneLoadingProgress Progress { get; }

        public SceneLoadingProgressSignal(SceneLoadingProgress progress)
        {
            Progress = progress;
        }
    }
}
