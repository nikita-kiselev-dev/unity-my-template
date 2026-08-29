using System;
using System.Collections.Generic;
using Framework.Foundation.Audio;
using Framework.Foundation.Initialization;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes.StateMachine.SceneStates;
using VContainer;

namespace Framework.Foundation.Scenes.StateMachine
{
    [AutoRegistration(Lifetime.Singleton)]
    public class SceneStateMachine : ISceneStateMachine
    {
        private readonly Dictionary<Type, ISceneState> _states;
        private ISceneState _activeState;

        // Состояния — не DI-типы: их создаёт машина, поэтому логгер она раздаёт сама.
        public SceneStateMachine(
            ISceneService sceneService,
            IAudioController audioController,
            ILogChannelFactory logChannelFactory)
        {
            _states = new Dictionary<Type, ISceneState>
            {
                {
                    typeof(StartSceneState),
                    new StartSceneState(sceneService, audioController,
                        logChannelFactory.Get(nameof(StartSceneState)))
                },
                {
                    typeof(CoreSceneState),
                    new CoreSceneState(sceneService, audioController,
                        logChannelFactory.Get(nameof(CoreSceneState)))
                },
                {
                    typeof(MetaSceneState),
                    new MetaSceneState(sceneService, logChannelFactory.Get(nameof(MetaSceneState)))
                }
            };
        }

        public void EnterState<TState>() where TState : ISceneState
        {
            _activeState?.Exit();
            _activeState = _states[typeof(TState)];
            _activeState.Enter();
        }
    }
}
