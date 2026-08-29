#if UNITY_WEBGL && PLUGIN_YG_2
using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using VContainer;
using YG;

namespace YandexGames
{
    /// <summary>
    /// Держит фазу <c>Load</c> Bootstrap-сцены до готовности YG SDK, чтобы все, кому нужны данные
    /// платформы, читали их синхронно в <c>Init</c>. Порядок в <c>BootstrapSceneInitOrder</c> роли
    /// не играет — он влияет только на <c>PostInit</c>; гарантию даёт барьер между фазами.
    /// Живёт в Assembly-CSharp: YG2 объявлен там, и asmdef-сборка на него сослаться не может.
    /// </summary>
    [AutoRegistration(Lifetime.Singleton)]
    [LifecycleOrder(SceneConstants.Scenes.Bootstrap, (int)BootstrapSceneInitOrder.First)]
    public class YandexSdkEntity : LifecycleEntity
    {
        private static readonly TimeSpan _initTimeout = TimeSpan.FromSeconds(10);

        [Inject] private readonly ILogChannelFactory _logChannelFactory;

        private ILogChannel _logger;

        protected override async UniTask Load()
        {
            _logger = _logChannelFactory.Get(nameof(YandexSdkEntity));

            if (YG2.isSDKEnabled)
            {
                return;
            }

            var initialized = new UniTaskCompletionSource();
            Action handler = () => initialized.TrySetResult();
            YG2.onGetSDKData += handler;

            // Realtime, а не DeltaTime: ожидание SDK не должно зависеть от timeScale.
            var timedOut = await initialized.Task.TimeoutWithoutException(_initTimeout, DelayType.Realtime);

            YG2.onGetSDKData -= handler;

            if (timedOut)
            {
                _logger.LogError($"YG SDK is not ready after {_initTimeout.TotalSeconds}s, continuing without platform data");
            }
        }

        protected override UniTask Init()
        {
            SetEnabled(YG2.isSDKEnabled);
            return UniTask.CompletedTask;
        }
    }
}
#endif
