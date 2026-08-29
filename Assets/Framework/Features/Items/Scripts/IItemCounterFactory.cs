using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Features.Items
{
    public interface IItemCounterFactory
    {
        UniTask<Dictionary<string, IItemCounter>> CreateAll(CancellationToken cancellationToken = default);
    }
}