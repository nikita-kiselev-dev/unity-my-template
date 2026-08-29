using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Framework.Foundation.Time
{
    /// <summary>
    /// Единая точка доступа к времени. Читается синхронно: серверное время синхронизируется
    /// один раз до готовности игры, дальше часы идут по монотонному <see cref="IRealtimeSource"/>.
    /// </summary>
    public interface IClock
    {
        /// <summary>Серверное UTC. Механики, где игрок не должен читить: награды, ивенты, кулдауны.</summary>
        DateTime ServerUtcNow { get; }

        /// <summary>Серверное время в таймзоне игрока: механики со сбросом в местную полночь.</summary>
        DateTime ServerLocalNow { get; }

        ClockTrust Trust { get; }

        /// <summary>Серверное UTC, тикающее раз в секунду. Для UI.</summary>
        ReadOnlyReactiveProperty<DateTime> ServerNow { get; }

        /// <summary>Остаток до дедлайна. Завершается ровно на <c>TimeSpan.Zero</c>.</summary>
        Observable<TimeSpan> Countdown(DateTime deadlineUtc);

        /// <summary>Идемпотентно, реально синхронизирует один раз за процесс.</summary>
        UniTask WarmUp(CancellationToken ct);
    }
}
