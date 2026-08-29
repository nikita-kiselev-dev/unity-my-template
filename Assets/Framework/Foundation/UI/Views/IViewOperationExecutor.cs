using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.UI.Views
{
    // Что pump делает с операцией, когда до неё дошла очередь. Реализует ViewRouter: очередь
    // окон и стек popup-ов принадлежат ему, а pump знает только порядок и коалесинг.
    internal interface IViewOperationExecutor
    {
        UniTask OpenWindow(ViewWrapper window, CancellationToken ct);
        UniTask OpenPopupBatch(IReadOnlyList<ViewWrapper> popups, CancellationToken ct);
        UniTask Close(ViewWrapper view, CancellationToken ct);
        UniTask CloseAll(CancellationToken ct);
    }
}
