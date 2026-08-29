using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Threading;
using UnityEngine;
using Framework.Foundation.Utilities.Extensions;

namespace Framework.Foundation.UI.Views.ViewAnimation
{
    public class PopupAnimator : IViewAnimator
    {
        private readonly Transform _viewTransform;
        private readonly CanvasGroup _canvasGroup;

        private readonly Vector3 _defaultScale = new(
            ViewAnimationConstants.Popup.DefaultLocalScale,
            ViewAnimationConstants.Popup.DefaultLocalScale,
            ViewAnimationConstants.Popup.DefaultLocalScale);

        private Sequence _sequence;

        public PopupAnimator(GameObject gameObject)
        {
            _viewTransform = gameObject.transform;
            _canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
        }

        public async UniTask Show(CancellationToken ct = default)
        {
            _sequence.Complete();
            _viewTransform.localScale = _defaultScale;
            _canvasGroup.alpha = ViewAnimationConstants.Popup.DefaultCanvasGroupAlpha;
            _viewTransform.gameObject.SetActive(true);

            _sequence = Sequence
                .Create()
                .Group(Tween.Scale(_viewTransform, Vector3.one, ViewAnimationConstants.Popup.PopInDuration, Ease.OutBack))
                .Group(Tween.Alpha(_canvasGroup, 1f, ViewAnimationConstants.Popup.PopInAlphaDuration));

            await UniTask.WaitWhile(() => _sequence.isAlive, cancellationToken: ct);
        }

        public async UniTask Hide(CancellationToken ct = default)
        {
            _sequence.Complete();
            _canvasGroup.alpha = 1f;

            _sequence = Sequence
                .Create()
                .Group(Tween.Scale(_viewTransform, _defaultScale, ViewAnimationConstants.Popup.PopOutDuration))
                .Group(Tween.Alpha(_canvasGroup, 0.0f, ViewAnimationConstants.Popup.PopOutAlphaDuration));

            await UniTask.WaitWhile(() => _sequence.isAlive, cancellationToken: ct);
            _viewTransform.gameObject.SetActive(false);
        }
    }
}
