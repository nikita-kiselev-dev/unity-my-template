using Framework.Foundation.Logger;

namespace Framework.Foundation.Scenes.StateMachine.SceneStates
{
    public class MetaSceneState : ISceneState
    {
        private readonly ISceneService _sceneService;
        private readonly ILogChannel _logger;

        public MetaSceneState(ISceneService sceneService, ILogChannel logger)
        {
            _sceneService = sceneService;
            _logger = logger;
        }

        public void Enter()
        {
            _sceneService.LoadScene(SceneConstants.Scenes.Meta, OnLoaded);
            _logger.Log("Load scene started.");
        }

        public void Exit()
        {
        }

        private void OnLoaded()
        {
            _logger.Log("Load scene completed.");
        }
    }
}