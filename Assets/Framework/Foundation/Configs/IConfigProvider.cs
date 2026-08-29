using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Configs
{
    public interface IConfigProvider
    {
        UniTask WarmUp(CancellationToken cancellationToken = default);
        IConfig Get(Type configType);
    }
}
