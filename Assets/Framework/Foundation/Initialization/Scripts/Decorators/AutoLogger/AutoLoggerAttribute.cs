using System;
using Framework.Foundation.Logger;

namespace Framework.Foundation.Initialization.Decorators.AutoLogger
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoLoggerAttribute : Attribute
    {
        public string LogName { get; }
        public LogCategory EntityType { get; }
        public bool StatusLogs { get; set; }

        public AutoLoggerAttribute(string logName, LogCategory entityType = LogCategory.System)
        {
            LogName = logName;
            EntityType = entityType;
        }
    }
}
