using Framework.Foundation.Audio;
using Framework.Foundation.Logger;

namespace Framework.Foundation.Scenes.StateMachine.SceneStates
{
    public class StartSceneState : ISceneState
    {
        private readonly ISceneService _sceneService;
        private readonly IAudioController _audioController;
        private readonly ILogChannel _logger;

        public StartSceneState(ISceneService sceneService, IAudioController audioController, ILogChannel logger)
        {
            _sceneService = sceneService;
            _audioController = audioController;
            _logger = logger;
        }

        public void Enter()
        {
            _sceneService.LoadScene(SceneConstants.Scenes.Start, OnLoaded);
            _logger.Log("Load scene started.");
        }

        public void Exit()
        {
        }

        private void OnLoaded()
        {
            _logger.Log("Load scene completed.");
            _audioController.PlayMusic(MusicKeys.StartSceneMusic);
        }
    }
}
