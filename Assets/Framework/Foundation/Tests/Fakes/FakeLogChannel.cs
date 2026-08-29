using System.Collections.Generic;
using Framework.Foundation.Logger;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeLogChannel : ILogChannel
    {
        public List<string> Messages { get; } = new();
        public List<string> Errors { get; } = new();

        public LogCategory EntityType => LogCategory.System;
        public bool AreLogsEnabled { get; private set; } = true;

        public void SetLogsStatus(bool areLogsEnabled) => AreLogsEnabled = areLogsEnabled;

        // Фейк пишет сообщение независимо от флага: тест проверяет, что вызывающий код
        // не дошёл до Log, а не что фейк его проглотил.
        public void Log(string message) => Messages.Add(message);

        public void LogError(string message) => Errors.Add(message);
    }
}
