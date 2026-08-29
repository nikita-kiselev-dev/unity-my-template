using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.LiveOps
{
    /// <summary>
    /// Единственное асинхронное место во времени: источник для разовой синхронизации часов.
    /// Недоступный сервер — валидный сценарий, поэтому <see cref="Result{T}"/>, а не исключение.
    /// Прикладной код время берёт из <c>IClock</c>, не отсюда.
    /// </summary>
    public interface IServerTimeSource
    {
        UniTask<Result<DateTime>> TryFetchUtc(CancellationToken ct);
    }
}
