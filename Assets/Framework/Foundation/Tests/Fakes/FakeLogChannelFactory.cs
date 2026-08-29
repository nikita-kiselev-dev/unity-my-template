using Framework.Foundation.Logger;

namespace Framework.Foundation.Tests.Fakes
{
    public sealed class FakeLogChannelFactory : ILogChannelFactory
    {
        public FakeLogChannel Logger { get; } = new();

        public ILogChannel Get(string entityName, LogCategory entityType = LogCategory.System) => Logger;
    }
}
