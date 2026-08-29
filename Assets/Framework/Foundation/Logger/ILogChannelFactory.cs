namespace Framework.Foundation.Logger
{
    public interface ILogChannelFactory
    {
        ILogChannel Get(string entityName, LogCategory entityType = LogCategory.System);
    }
}
