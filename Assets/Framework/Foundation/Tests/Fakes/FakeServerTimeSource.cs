using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.LiveOps;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.Tests.Fakes
{
    public sealed class FakeServerTimeSource : IServerTimeSource
    {
        public Result<DateTime> NextResult { get; set; } = Result<DateTime>.Failure();
        public int FetchCount { get; private set; }

        public UniTask<Result<DateTime>> TryFetchUtc(CancellationToken ct)
        {
            FetchCount++;
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(NextResult);
        }
    }
}
