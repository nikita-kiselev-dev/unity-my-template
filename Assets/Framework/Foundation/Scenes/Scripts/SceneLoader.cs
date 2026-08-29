using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.Foundation.Scenes
{
    [AutoRegistration(Lifetime.Singleton)]
    [AutoLogger(nameof(SceneLoader))]
    public partial class SceneLoader : ISceneLoader, IDisposable
    {
        [Inject] private readonly ISignalBus _signalBus;

        private readonly Func<string> _getActiveSceneName = () => SceneManager.GetActiveScene().name;
        private readonly Func<string, UniTask> _loadSceneAsync = LoadAddressableScene;
        private readonly Func<float, CancellationToken, UniTask> _delay =
            (seconds, ct) => UniTask.WaitForSeconds(seconds, cancellationToken: ct);

        private SceneLoadRequest _request;

        // [Inject] на этом ctor обязателен: рядом есть internal-шов с параметрами, а VContainer
        // без явной пометки выбрал бы конструктор с наибольшим числом параметров (TypeAnalyzer).
        [Inject]
        public SceneLoader()
        {
        }

        // Тестовый шов: в проде поля и Logger заполняет VContainer.
        internal SceneLoader(
            ISignalBus signalBus,
            Func<string> getActiveSceneName,
            Func<string, UniTask> loadSceneAsync,
            Func<float, CancellationToken, UniTask> delay,
            ILogChannel logger)
        {
            _signalBus = signalBus;
            _getActiveSceneName = getActiveSceneName;
            _loadSceneAsync = loadSceneAsync;
            _delay = delay;
            Logger = logger;
        }

        public bool PrepareSceneLoad(string sceneName, Action onSceneLoadedCallback = null)
        {
            if (_request != null)
            {
                Logger.LogError($"Scene load '{sceneName}' rejected: '{_request.SceneName}' is already pending.");
                return false;
            }

            var activeSceneName = _getActiveSceneName();

            if (activeSceneName == sceneName)
            {
                onSceneLoadedCallback?.Invoke();
                return true;
            }

            _request = new SceneLoadRequest(sceneName, onSceneLoadedCallback);

            if (sceneName == SceneConstants.Scenes.Start)
            {
                LoadAsync().Forget();
                return true;
            }

            _signalBus.Trigger<SceneChangeRequestedSignal>();
            return true;
        }

        public async UniTask LoadAsync()
        {
            var request = _request;
            if (request == null || request.IsLoading)
            {
                return;
            }

            request.IsLoading = true;
            var ct = request.Cancellation.Token;
            var secondsToWait = request.SceneName == SceneConstants.Scenes.Start
                ? 0
                : SceneConstants.Parameters.LoadDelay;

            try
            {
                await _delay(secondsToWait, ct);
                await _loadSceneAsync(request.SceneName);

                if (ReferenceEquals(request, _request) && !ct.IsCancellationRequested)
                {
                    request.OnLoaded?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                // Без сигнала шторка ждала бы SceneStartedSignal новой сцены, который уже не придёт.
                Logger.LogError($"Failed to load scene '{request.SceneName}': {e}");
                _signalBus.Trigger(new SceneLoadFailedSignal(request.SceneName, e));
            }
            finally
            {
                if (ReferenceEquals(request, _request))
                {
                    request.Dispose();
                    _request = null;
                }
            }
        }

        void IDisposable.Dispose()
        {
            CancelCurrentRequest();
        }

        private void CancelCurrentRequest()
        {
            if (_request == null)
            {
                return;
            }

            _request.Dispose();
            _request = null;
        }

        private static async UniTask LoadAddressableScene(string sceneName)
        {
            await Addressables.LoadSceneAsync(sceneName).ToUniTask();
        }

        private sealed class SceneLoadRequest : IDisposable
        {
            public string SceneName { get; }
            public Action OnLoaded { get; }
            public CancellationTokenSource Cancellation { get; } = new();
            public bool IsLoading { get; set; }

            public SceneLoadRequest(string sceneName, Action onLoaded)
            {
                SceneName = sceneName;
                OnLoaded = onLoaded;
            }

            public void Dispose()
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
            }
        }
    }
}
