using Framework.Features.MainMenu.ViewModel;
using Framework.Features.Tests.Fakes;
using Framework.Foundation.Scenes.StateMachine.SceneStates;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;
using R3;

namespace Framework.Features.Tests
{
    public class MainMenuViewModelTests
    {
        private FakeSceneStateMachine _sceneStateMachine;
        private FakeSettingsCore _settingsCore;
        private FakeExternalLinkOpener _externalSources;

        [SetUp]
        public void SetUp()
        {
            _sceneStateMachine = new FakeSceneStateMachine();
            _settingsCore = new FakeSettingsCore();
            _externalSources = new FakeExternalLinkOpener();
        }

        private MainMenuViewModel CreateViewModel(bool isOnboardingCompleted)
        {
            return new MainMenuViewModel(_sceneStateMachine, _settingsCore, _externalSources, isOnboardingCompleted);
        }

        [Test]
        public void Play_EntersMetaScene_WhenOnboardingCompleted()
        {
            var viewModel = CreateViewModel(isOnboardingCompleted: true);

            viewModel.Play.Execute(Unit.Default);

            Assert.AreEqual(new[] { typeof(MetaSceneState) }, _sceneStateMachine.EnteredStates.ToArray());
        }

        [Test]
        public void Play_EntersCoreScene_WhenOnboardingNotCompleted()
        {
            var viewModel = CreateViewModel(isOnboardingCompleted: false);

            viewModel.Play.Execute(Unit.Default);

            Assert.AreEqual(new[] { typeof(CoreSceneState) }, _sceneStateMachine.EnteredStates.ToArray());
        }

        [Test]
        public void Play_TransitionsOnlyOnce_OnRepeatedClicks()
        {
            var viewModel = CreateViewModel(isOnboardingCompleted: true);

            viewModel.Play.Execute(Unit.Default);
            viewModel.Play.Execute(Unit.Default);

            Assert.AreEqual(1, _sceneStateMachine.EnteredStates.Count);
        }

        [Test]
        public void OpenSettings_OpensPopup()
        {
            var viewModel = CreateViewModel(isOnboardingCompleted: true);

            viewModel.OpenSettings.Execute(Unit.Default);

            Assert.AreEqual(1, _settingsCore.OpenPopupCount);
        }

        [Test]
        public void OpenWebSite_OpensPrivacyPolicy()
        {
            var viewModel = CreateViewModel(isOnboardingCompleted: true);

            viewModel.OpenWebSite.Execute(Unit.Default);

            Assert.AreEqual(1, _externalSources.PrivacyPolicyOpenCount);
        }
    }
}
