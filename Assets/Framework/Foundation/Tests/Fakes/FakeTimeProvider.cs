using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Foundation.Tests.Fakes
{
    /// <summary>
    /// Детерминированный <see cref="TimeProvider"/> для тестов: время двигается только
    /// через <see cref="Advance"/>, таймеры срабатывают синхронно в хронологическом порядке.
    /// </summary>
    public sealed class FakeTimeProvider : TimeProvider
    {
        private readonly List<FakeTimer> _timers = new();
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan delta)
        {
            var target = _utcNow + delta;

            while (true)
            {
                var next = FindNextDueTimer(target);

                if (next == null)
                {
                    break;
                }

                _utcNow = next.DueAt.Value;
                next.Fire();
            }

            _utcNow = target;
        }

        private FakeTimer FindNextDueTimer(DateTimeOffset target)
        {
            FakeTimer next = null;

            foreach (var timer in _timers)
            {
                if (timer.DueAt.HasValue && timer.DueAt.Value <= target &&
                    (next == null || timer.DueAt.Value < next.DueAt.Value))
                {
                    next = timer;
                }
            }

            return next;
        }

        private void Remove(FakeTimer timer) => _timers.Remove(timer);

        private sealed class FakeTimer : ITimer
        {
            private readonly FakeTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object _state;
            private TimeSpan _period;

            public DateTimeOffset? DueAt { get; private set; }

            public FakeTimer(FakeTimeProvider owner, TimerCallback callback, object state)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
            }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner._utcNow + dueTime;
                return true;
            }

            // Перепланирование до вызова колбэка: колбэк может сам вызвать Change/Dispose.
            public void Fire()
            {
                DueAt = _period == Timeout.InfiniteTimeSpan || _period <= TimeSpan.Zero
                    ? null
                    : DueAt + _period;

                _callback(_state);
            }

            public void Dispose()
            {
                DueAt = null;
                _owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }
        }
    }
}
