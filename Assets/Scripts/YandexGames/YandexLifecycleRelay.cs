#if UNITY_WEBGL && PLUGIN_YG_2
using System;
using Framework.Foundation.Initialization;
using Framework.Foundation.Signals;
using Framework.Foundation.UnityLifecycle;
using VContainer;
using VContainer.Unity;
using YG;

namespace YandexGames
{
    /// <summary>
    /// Платформенный источник lifecycle-событий для web. Unity-коллбеков здесь нет:
    /// <c>OnApplicationQuit</c> на web не вызывается вовсе (закрытие вкладки), а уход со
    /// страницы приезжает из SDK в <c>YG2.onPauseGame</c>. Транслируем его в штатный
    /// <see cref="ApplicationPauseChangedSignal"/>, чтобы сработали и синхронный сейв
    /// (<c>ProgressSaver</c>), и ресинхронизация часов (<c>Clock</c>).
    /// Живёт в Assembly-CSharp: YG2 объявлен там, и asmdef-сборка на него сослаться не может.
    /// </summary>
    [AutoRegistration(Lifetime.Singleton)]
    public class YandexLifecycleRelay : IStartable, IDisposable
    {
        private readonly ISignalBus _signalBus;

        public YandexLifecycleRelay(ISignalBus signalBus)
        {
            _signalBus = signalBus;
        }

        void IStartable.Start()
        {
            YG2.onPauseGame += OnPauseGame;
        }

        // Событие YG2 статическое и переживает scope: без отписки relay мёртвой сцены
        // продолжит триггерить сигнал в убитую шину.
        void IDisposable.Dispose()
        {
            YG2.onPauseGame -= OnPauseGame;
        }

        private void OnPauseGame(bool isPaused)
        {
            _signalBus.Trigger(new ApplicationPauseChangedSignal(isPaused));
        }
    }
}
#endif
