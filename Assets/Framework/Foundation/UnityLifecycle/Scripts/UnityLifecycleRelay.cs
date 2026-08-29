using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Signals;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.UnityLifecycle
{
    [AutoLogger(nameof(UnityLifecycleRelay))]
    public partial class UnityLifecycleRelay : MonoBehaviour
    {
        [Inject] private readonly ISignalBus _signalBus;

        // Awake/Start сигналов не публикуют: шина без replay, а подписаться раньше первого
        // Unity-коллбека релея некому — сигнал ушёл бы в пустоту.
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Logger.Log("Awake.");
        }

        private void Start()
        {
            Logger.Log("Start.");
        }

        private void OnApplicationQuit()
        {
            _signalBus.Trigger<ApplicationQuittingSignal>();
            Logger.Log("ApplicationQuit.");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(pauseStatus));
            Logger.Log($"ApplicationPause ({pauseStatus}).");
        }
    }
}
