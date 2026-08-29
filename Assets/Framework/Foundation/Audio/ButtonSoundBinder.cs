using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Scenes;
using Framework.Foundation.UI.Views;
using Framework.Foundation.Utilities.Extensions;
using UnityEngine.UI;
using VContainer;

namespace Framework.Foundation.Audio
{
    /// <summary>
    /// Вешает звук нажатия на каждую кнопку внутри view, созданного <see cref="ViewFactory"/>.
    /// Одновременно LifecycleEntity: не-persistent клип освобождается по шторке, поэтому прогрев
    /// нужен на каждой сцене, а не один раз за сессию.
    /// </summary>
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.Last)]
    [LifecycleOrder(SceneConstants.Scenes.Core, (int)CoreSceneInitOrder.Last)]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.Last)]
    public class ButtonSoundBinder : LifecycleEntity, IViewSetupStep
    {
        [Inject] private readonly IAudioClipLoader _audioLoader;
        [Inject] private readonly IAudioController _audioController;

        protected override async UniTask Load()
        {
            await _audioLoader.LoadAudio(SoundKeys.ClickSound0);
        }

        protected override UniTask Init()
        {
            SetEnabled(true);
            SetActive();
            return UniTask.CompletedTask;
        }

        public void Setup(MonoView view)
        {
            foreach (var button in view.GetComponentsInChildren<Button>(includeInactive: true))
            {
                button.AddListenerClean(PlayButtonSound);
            }
        }

        private void PlayButtonSound()
        {
            _audioController.PlaySound(SoundKeys.ClickSound0);
        }
    }
}
