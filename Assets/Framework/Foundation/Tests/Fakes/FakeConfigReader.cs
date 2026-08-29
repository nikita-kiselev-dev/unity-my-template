using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Configs;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeConfigReader : IConfigReader
    {
        private readonly Dictionary<Type, IConfig> _configs;

        public int ReadCount { get; private set; }

        /// Одноразовый сбой чтения: следующий Read бросает и сбрасывает поле.
        public Exception ThrowOnNextRead { get; set; }

        public FakeConfigReader(Dictionary<Type, IConfig> configs)
        {
            _configs = configs;
        }

        public void AddConfig(Type configType, IConfig config) => _configs[configType] = config;

        public UniTask<Result<IConfig>> Read(Type configType, string configName, CancellationToken cancellationToken = default)
        {
            ReadCount++;

            if (ThrowOnNextRead != null)
            {
                var exception = ThrowOnNextRead;
                ThrowOnNextRead = null;
                throw exception;
            }

            var hasValue = _configs.TryGetValue(configType, out var config);
            return UniTask.FromResult(new Result<IConfig>(config, hasValue));
        }
    }
}
