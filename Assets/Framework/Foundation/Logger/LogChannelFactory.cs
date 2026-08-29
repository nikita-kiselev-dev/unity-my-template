using System.Collections.Generic;
using Framework.Foundation.Initialization;
using VContainer;

namespace Framework.Foundation.Logger
{
    [AutoRegistration(Lifetime.Singleton)]
    public sealed class LogChannelFactory : ILogChannelFactory
    {
        private readonly Dictionary<(string name, LogCategory type), ILogChannel> _cache = new();

        public ILogChannel Get(string entityName, LogCategory entityType = LogCategory.System)
        {
            var key = (entityName, entityType);

            if (_cache.TryGetValue(key, out var logger))
            {
                return logger;
            }

            logger = new LogChannel(entityName, entityType);
            _cache[key] = logger;
            return logger;
        }
    }
}
