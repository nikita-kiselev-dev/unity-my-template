namespace Framework.Foundation.Logger
{
    public interface ILogChannel
    {
        LogCategory EntityType { get; }

        /// <summary>
        /// Guard для хот-пассов: форматирование сообщения стоит дороже самого Log, а при
        /// выключенных логах результат всё равно выбрасывается.
        /// </summary>
        bool AreLogsEnabled { get; }

        void SetLogsStatus(bool areLogsEnabled);
        void Log(string message);
        void LogError(string message);
    }
}