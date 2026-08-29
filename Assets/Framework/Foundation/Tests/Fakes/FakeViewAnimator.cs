using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Views.ViewAnimation;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeViewAnimator : IViewAnimator
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }

        public UniTask Show(CancellationToken ct = default)
        {
            ShowCount++;
            return UniTask.CompletedTask;
        }

        public UniTask Hide(CancellationToken ct = default)
        {
            HideCount++;
            return UniTask.CompletedTask;
        }
    }
}
