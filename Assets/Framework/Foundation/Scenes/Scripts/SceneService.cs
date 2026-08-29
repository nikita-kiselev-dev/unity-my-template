using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using R3;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.Foundation.Scenes
{
    [AutoRegistration(Lifetime.Singleton)]
    public class SceneService : ISceneService, IDisposable
    {
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly ISceneLoader _sceneLoader;

        private DisposableBag _subscriptions;

        public bool LoadScene(string sceneName, Action onLoaded = null)
        {
            return _sceneLoader.PrepareSceneLoad(sceneName, onLoaded);
        }

        void IDisposable.Dispose()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            _subscriptions.Dispose();
        }

        [Inject]
        private void Init()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            _signalBus.Subscribe<LoadingCurtainShownSignal>(LoadScene).AddTo(ref _subscriptions);
        }

        private void LoadScene()
        {
            _sceneLoader.LoadAsync().Forget();
        }

        private void OnSceneChanged(Scene previousScene, Scene currentScene)
        {
            if (currentScene.name != SceneConstants.Scenes.Bootstrap && currentScene.name != SceneConstants.Scenes.Start)
            {
                _signalBus.Trigger<SceneChangedSignal>();
            }
        }
    }
}
