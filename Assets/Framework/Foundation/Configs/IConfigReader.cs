using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.Configs
{
    public interface IConfigReader
    {
        UniTask<Result<IConfig>> Read(Type configType, string configName, CancellationToken cancellationToken = default);
    }
}