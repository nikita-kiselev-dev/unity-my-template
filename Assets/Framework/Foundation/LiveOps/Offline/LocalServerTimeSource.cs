using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.LiveOps.Offline
{
    public class LocalServerTimeSource : IServerTimeSource
    {
        public UniTask<Result<DateTime>> TryFetchUtc(CancellationToken ct)
        {
            return UniTask.FromResult(Result<DateTime>.Success(DateTime.UtcNow));
        }
    }
}
