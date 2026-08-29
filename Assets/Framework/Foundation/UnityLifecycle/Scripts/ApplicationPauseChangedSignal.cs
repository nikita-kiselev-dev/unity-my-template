using Framework.Foundation.Signals;

namespace Framework.Foundation.UnityLifecycle
{
    public class ApplicationPauseChangedSignal : ISignal
    {
        public bool IsPaused { get; }

        public ApplicationPauseChangedSignal(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
