using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Configs;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeConfigResolver : IConfigResolver
    {
        public Exception ReadThrows { get; set; }
        public IConfig ReadResult { get; set; }

        public UniTask<IConfig> Read(Type configType, string configName, CancellationToken cancellationToken = default)
        {
            if (ReadThrows != null)
            {
                throw ReadThrows;
            }

            return UniTask.FromResult(ReadResult);
        }

        public void SetServerValues(IReadOnlyDictionary<string, string> config)
        {
        }
    }
}
