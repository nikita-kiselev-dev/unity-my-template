using System;

namespace Framework.Foundation.Time
{
    /// <summary>
    /// Монотонное время процесса, не зависящее ни от системных часов, ни от <c>timeScale</c>
    /// и паузы игры. Основа для хода часов между синхронизациями.
    /// </summary>
    public interface IRealtimeSource
    {
        TimeSpan Elapsed { get; }
    }
}
