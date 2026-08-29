using System;
using System.Diagnostics;
using Framework.Foundation.Initialization;
using VContainer;

namespace Framework.Foundation.Time
{
    /// <summary>
    /// Не <c>UnityEngine.Time</c> и не инжектируемый <c>TimeProvider</c>: в плеере это
    /// <c>UnityTimeProvider.Update</c> с <c>TimeKind.Time</c>, чей <c>GetTimestamp()</c> отдаёт
    /// <c>Time.timeAsDouble</c> — зависит от <c>timeScale</c> и стоит на паузе игры.
    /// </summary>
    [AutoRegistration(Lifetime.Singleton)]
    public sealed class StopwatchRealtimeSource : IRealtimeSource
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public TimeSpan Elapsed => _stopwatch.Elapsed;
    }
}
