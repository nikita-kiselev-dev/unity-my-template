namespace Framework.Foundation.Scenes.StateMachine
{
    public interface ISceneStateMachine
    {
        void EnterState<TState>() where TState : ISceneState;
    }
}
