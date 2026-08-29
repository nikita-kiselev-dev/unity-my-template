using System.Collections.Generic;
using Framework.Foundation.Scenes;
using Framework.Foundation.Scenes.StateMachine;
using Framework.Foundation.Scenes.StateMachine.SceneStates;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class SceneStateMachineTests
    {
        private FakeSceneService _sceneService;
        private SceneStateMachine _stateMachine;

        [SetUp]
        public void Setup()
        {
            _sceneService = new FakeSceneService();
            _stateMachine = new SceneStateMachine(
                _sceneService, new FakeAudioController(), new FakeLogChannelFactory());
        }

        [Test]
        public void EnterState_LoadsStartScene_ForStartSceneState()
        {
            _stateMachine.EnterState<StartSceneState>();

            CollectionAssert.AreEqual(new[] { SceneConstants.Scenes.Start }, _sceneService.LoadedScenes);
        }

        [Test]
        public void EnterState_LoadsCoreScene_ForCoreSceneState()
        {
            _stateMachine.EnterState<CoreSceneState>();

            CollectionAssert.AreEqual(new[] { SceneConstants.Scenes.Core }, _sceneService.LoadedScenes);
        }

        [Test]
        public void EnterState_LoadsMetaScene_ForMetaSceneState()
        {
            _stateMachine.EnterState<MetaSceneState>();

            CollectionAssert.AreEqual(new[] { SceneConstants.Scenes.Meta }, _sceneService.LoadedScenes);
        }

        [Test]
        public void EnterState_LoadsNextScene_WhenSwitchingStates()
        {
            _stateMachine.EnterState<StartSceneState>();
            _stateMachine.EnterState<CoreSceneState>();

            CollectionAssert.AreEqual(
                new[] { SceneConstants.Scenes.Start, SceneConstants.Scenes.Core },
                _sceneService.LoadedScenes);
        }

        [Test]
        public void EnterState_Throws_ForUnregisteredState()
        {
            Assert.Throws<KeyNotFoundException>(() => _stateMachine.EnterState<UnregisteredState>());
        }

        private sealed class UnregisteredState : ISceneState
        {
            public void Enter()
            {
            }

            public void Exit()
            {
            }
        }
    }
}
