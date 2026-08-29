using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Effects;
using Framework.Foundation.UI.LoadingCurtain.View;
using Framework.Foundation.UI.Views.ViewAnimation;
using PrimeTween;
using System.Threading;
using UnityEngine;

namespace Framework.Foundation.UI.LoadingCurtain
{
    public class LoadingCurtainGradientColorAnimator : IViewAnimator
    {
        private readonly GameObject _gameObject;
        private readonly CanvasGroup _canvasGroup;

        private readonly Action _afterShowCallback;
        private readonly Action _afterHideCallback;

        private Sequence Sequence { get; set; }

        public bool IsAnimating => Sequence.isAlive;

        public LoadingCurtainGradientColorAnimator(
            LoadingCurtainView view,
            Action afterShowCallback,
            Action afterHideCallback)
        {
            _gameObject = view.gameObject;
            _canvasGroup = view.CanvasGroup;
            _afterShowCallback = afterShowCallback;
            _afterHideCallback = afterHideCallback;
        }

        public async UniTask Show(CancellationToken ct = default)
        {
            Sequence.Complete();

            _canvasGroup.alpha = 0f;
            _gameObject.SetActive(true);

            Sequence = Sequence
                .Create()
                .Chain(Tween.Alpha(_canvasGroup, 1f, LoadingCurtainConstants.Parameters.FadeInAnimationDuration));

            await UniTask.WaitWhile(() => Sequence.isAlive, cancellationToken: ct);
            _afterShowCallback?.Invoke();
        }

        public async UniTask Hide(CancellationToken ct = default)
        {
            Sequence.Complete();

            _canvasGroup.alpha = 1f;
            _gameObject.SetActive(true);

            Sequence = Sequence
                .Create()
                .Chain(Tween.Alpha(_canvasGroup, 0f, LoadingCurtainConstants.Parameters.FadeOutAnimationDuration));

            await UniTask.WaitWhile(() => Sequence.isAlive, cancellationToken: ct);
            _gameObject.SetActive(false);
            _afterHideCallback?.Invoke();
        }
    }
}
