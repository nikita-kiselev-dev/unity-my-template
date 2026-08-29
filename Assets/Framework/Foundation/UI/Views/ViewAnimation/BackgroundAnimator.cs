using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Canvas;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Framework.Foundation.UI.Views.ViewAnimation
{
    public class BackgroundAnimator : IViewAnimator
    {
        private const float ShowViewBackgroundDuration = 0.2f;
        private const float HideViewBackgroundDuration = 0.2f;
        private const float ShowColorAlphaValue = 0.8f;

        private ICanvasProvider _canvasProvider;
        private CanvasGroup _popupBackground;
        private Sequence _sequence;

        // Канвас здесь не читается: провайдер создаёт его в фазе Load, а этот метод зовётся
        // на post-inject. Show/Hide всё равно перечитывают BackgroundCanvasGroup перед показом.
        public void SetCanvasProvider(ICanvasProvider canvasProvider)
        {
            _canvasProvider = canvasProvider;
        }

        public async UniTask Show(CancellationToken ct = default)
        {
            _sequence.Complete();
            _popupBackground = _canvasProvider.PopupCanvas.BackgroundCanvasGroup;
            _popupBackground.alpha = 0f;
            _popupBackground.gameObject.SetActive(true);

            var sequence = Sequence
                .Create()
                .Chain(Tween.Alpha(_popupBackground, ShowColorAlphaValue, ShowViewBackgroundDuration));
            _sequence = sequence;

            await UniTask.WaitWhile(() => sequence.isAlive, cancellationToken: ct);
        }

        public async UniTask Hide(CancellationToken ct = default)
        {
            _sequence.Complete();
            _popupBackground = _canvasProvider.PopupCanvas.BackgroundCanvasGroup;
            _popupBackground.alpha = ShowColorAlphaValue;
            _popupBackground.gameObject.SetActive(true);

            var sequence = Sequence
                .Create()
                .Chain(Tween.Alpha(_popupBackground, 0f, HideViewBackgroundDuration));
            _sequence = sequence;

            await UniTask.WaitWhile(() => sequence.isAlive, cancellationToken: ct);
            _popupBackground.gameObject.SetActive(false);
        }
    }
}
