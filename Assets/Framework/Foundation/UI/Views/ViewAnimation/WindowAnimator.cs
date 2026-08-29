using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Framework.Foundation.UI.Views.ViewAnimation
{
    public class WindowAnimator : IViewAnimator
    {
        private readonly Transform _viewTransform;

        public WindowAnimator(Transform viewTransform)
        {
            _viewTransform = viewTransform;
        }

        public UniTask Show(CancellationToken ct = default)
        {
            _viewTransform.gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask Hide(CancellationToken ct = default)
        {
            _viewTransform.gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }
    }
}
