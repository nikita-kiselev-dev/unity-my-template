using System;
using System.Collections.Generic;
using Framework.Foundation.Scenes.StateMachine;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeSceneStateMachine : ISceneStateMachine
    {
        public List<Type> EnteredStates { get; } = new();

        public void EnterState<TState>() where TState : ISceneState => EnteredStates.Add(typeof(TState));
    }
}
