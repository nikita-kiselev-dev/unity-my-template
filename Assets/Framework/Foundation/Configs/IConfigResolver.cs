using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Configs
{
    public interface IConfigResolver
    {
        UniTask<IConfig> Read(Type configType, string configName, CancellationToken cancellationToken = default);
        void SetServerValues(IReadOnlyDictionary<string, string> config);
    }
}
