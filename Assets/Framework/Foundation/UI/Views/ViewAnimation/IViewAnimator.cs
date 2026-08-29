using Cysharp.Threading.Tasks;
using System.Threading;

namespace Framework.Foundation.UI.Views.ViewAnimation
{
    public interface IViewAnimator
    {
        UniTask Show(CancellationToken ct = default);
        UniTask Hide(CancellationToken ct = default);
    }
}
