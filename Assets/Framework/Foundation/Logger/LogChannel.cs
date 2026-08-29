using UnityEngine;

namespace Framework.Foundation.Logger
{
    public class LogChannel : ILogChannel
    {
        private readonly string _entityName;
        private LogCategory _entityType;
        private bool _areLogsEnabled = true;

        public LogCategory EntityType => _entityType;
        public bool AreLogsEnabled => _areLogsEnabled;

        public LogChannel(string entityName, LogCategory entityType = LogCategory.System)
        {
            _entityName = entityName;
            _entityType = entityType;
        }

        public void SetEntityType(LogCategory entityType)
        {
            _entityType = entityType;
        }

        public void SetLogsStatus(bool areLogsEnabled)
        {
            _areLogsEnabled = areLogsEnabled;
        }

        [HideInCallstack]
        public void Log(string message)
        {
            if (!_areLogsEnabled)
            {
                return;
            }

            Debug.Log($"{Format(message)}\n");
        }

        // Ошибки логируются всегда: verbosity-флаг гасит только информационные логи,
        // иначе SetLogsStatus(false) молча проглотил бы реальные ошибки.
        [HideInCallstack]
        public void LogError(string message)
        {
            Debug.LogError($"{message.FormatAsErrorLog(_entityName)}\n");
        }

        private string Format(string message)
        {
            return _entityType switch
            {
                LogCategory.System => message.FormatAsSystemLog(_entityName),
                LogCategory.Feature => message.FormatAsFeatureLog(_entityName),
                _ => message
            };
        }
    }

    public sealed class LogChannel<T> : LogChannel
    {
        public LogChannel(LogCategory entityType = LogCategory.System)
            : base(typeof(T).Name, entityType)
        {
        }
    }
}
