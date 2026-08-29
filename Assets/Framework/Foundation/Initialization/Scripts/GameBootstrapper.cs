using Framework.Foundation.Initialization.Signals;
using Framework.Foundation.Scenes;
using Framework.Foundation.Scenes.StateMachine;
using Framework.Foundation.Scenes.StateMachine.SceneStates;
using Framework.Foundation.Signals;
using R3;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.Initialization
{
    public class GameBootstrapper : MonoBehaviour
    {
        [Inject] private readonly ISceneStateMachine _sceneStateMachine;
        [Inject] private readonly ISignalBus _signalBus;

        [Inject]
        private void Init()
        {
            _signalBus.Subscribe<SceneStartedSignal>(EnterGame).AddTo(this);
        }

        private void EnterGame(SceneStartedSignal signal)
        {
            if (signal.SceneName == SceneConstants.Scenes.Bootstrap)
            {
                _sceneStateMachine.EnterState<StartSceneState>();
            }
        }
    }
}
