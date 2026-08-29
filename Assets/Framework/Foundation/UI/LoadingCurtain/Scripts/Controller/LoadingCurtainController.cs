using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization.Signals;
using Framework.Foundation.Localization;
using Framework.Foundation.Localization.Extensions;
using Framework.Foundation.Scenes;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.UI.LoadingCurtain.View;
using Framework.Foundation.Utilities;
using R3;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.UI.LoadingCurtain.Controller
{
    public class LoadingCurtainController : MonoBehaviour, ILoadingCurtainController
    {
        private const string _loadingKey = "loading";

        [SerializeField] private LoadingCurtainView m_View;

        [Inject] private readonly ISignalBus _signalBus;

        private LoadingCurtainGradientColorAnimator _animator;

        public EntityStatus Status { get; } = new(nameof(LoadingCurtainController));

        bool IReadOnlyEntityStatus.IsEnabled => Status.IsEnabled;
        bool IReadOnlyEntityStatus.IsInited => Status.IsInited;
        bool IReadOnlyEntityStatus.IsActive => Status.IsActive;

        [Inject]
        private void Subscribe()
        {
            _signalBus.Subscribe<SceneChangeRequestedSignal>(Show).AddTo(this);
            _signalBus.Subscribe<SceneStartedSignal>(Hide).AddTo(this);
            _signalBus.Subscribe<SceneLoadFailedSignal>(HideAfterLoadFailure).AddTo(this);
            _signalBus.Subscribe<SceneStartFailedSignal>(HideAfterStartFailure).AddTo(this);
            _signalBus.Subscribe<SceneLoadingProgressSignal>(UpdateLoadingProgress).AddTo(this);
        }

        private void Awake()
        {
            if (Status.IsInited)
            {
                return;
            }

            _animator = new LoadingCurtainGradientColorAnimator(m_View, AfterShow, AfterHide);
            ConfigureView().Forget();

            Status
                .SetEnabled(true)
                .SetInited(true);
        }

        private void Show()
        {
            ShowAsync().Forget();
        }

        private async UniTaskVoid ShowAsync()
        {
            await UniTask.WaitUntil(() => !_animator.IsAnimating, cancellationToken: destroyCancellationToken);

            if (!m_View.gameObject.activeSelf)
            {
                _animator.Show(destroyCancellationToken).Forget();
            }
        }

        private void Hide(SceneStartedSignal signal)
        {
            // После Bootstrap шторка остаётся: сразу за ним идёт загрузка Start.
            if (signal.SceneName == SceneConstants.Scenes.Bootstrap)
            {
                return;
            }

            HideAsync().Forget();
        }

        // Сцена не загрузилась, игрок остался на предыдущей: шторку снимаем, иначе UI залочен навсегда.
        private void HideAfterLoadFailure(SceneLoadFailedSignal signal)
        {
            HideAsync().Forget();
        }

        // Сцена загрузилась, но фаза упала: SceneStartedSignal уже не придёт, и без этого
        // подписчика шторка висит поверх сцены навсегда.
        private void HideAfterStartFailure(SceneStartFailedSignal signal)
        {
            HideAsync().Forget();
        }

        private async UniTaskVoid HideAsync()
        {
            await UniTask.WaitUntil(() => !_animator.IsAnimating, cancellationToken: destroyCancellationToken);

            if (m_View.gameObject.activeSelf)
            {
                _animator.Hide(destroyCancellationToken).Forget();
            }
        }

        private void AfterShow()
        {
            _signalBus.Trigger<LoadingCurtainShownSignal>();
        }

        private void AfterHide()
        {
            _signalBus.Trigger<LoadingCurtainHiddenSignal>();
        }

        private void UpdateLoadingProgress(SceneLoadingProgressSignal signal)
        {
            m_View.SetLoadingSliderTotalValue(signal.Progress.Total);
            m_View.SetLoadingSliderCurrentValue(signal.Progress.Completed);
        }

        private async UniTaskVoid ConfigureView()
        {
            var loadingLocalizedString = await _loadingKey
                .Localize(LocalizationConstants.Tables.General)
                .AttachExternalCancellation(destroyCancellationToken);

            m_View.SetLoadingText(loadingLocalizedString);
        }
    }
}
