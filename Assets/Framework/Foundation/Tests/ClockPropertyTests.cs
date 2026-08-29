using System;
using System.Collections.Generic;
using System.Threading;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.Time;
using Framework.Foundation.Utilities;
using NUnit.Framework;
using R3;

namespace Framework.Foundation.Tests
{
    // Инварианты часов на сгенерированных последовательностях сдвигов времени. Монотонность —
    // именно то свойство, которое примерами не покажешь: она обязана держаться на любом наборе
    // интервалов, а ломается на конкретном.
    public class ClockPropertyTests
    {
        private static readonly DateTime ServerUtc = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        private sealed class Fixture : IDisposable
        {
            public readonly ReactiveSignalBus SignalBus = new();
            public readonly FakeRealtimeSource Realtime = new();
            public readonly FakeTimeProvider TimeProvider = new();
            public readonly Clock Clock;

            public Fixture()
            {
                var serverTime = new FakeServerTimeSource { NextResult = Result<DateTime>.Success(ServerUtc) };
                Clock = new Clock(serverTime, Realtime, TimeProvider, SignalBus, new FakeLogChannelFactory());
                Clock.WarmUp(CancellationToken.None).GetAwaiter().GetResult();
            }

            public void Advance(TimeSpan delta)
            {
                Realtime.Advance(delta);
                TimeProvider.Advance(delta);
            }

            public void Dispose()
            {
                Clock.Dispose();
                SignalBus.Dispose();
            }
        }

        // Clock создаётся один раз на тест, а не на кейс: инстанс тянет за собой R3-подписку и
        // сигнал-бус, и в Mono сотни таких инстансов стоили минуты в Test Runner. Инварианту это
        // ничего не стоит — он про непрерывную серию сдвигов, а не про свежие часы.
        [Test]
        public void ServerUtcNow_NeverGoesBackwards_ForAnyAdvanceSequence()
        {
            using var fixture = new Fixture();
            var previous = fixture.Clock.ServerUtcNow;

            PropertyCheck.ForAll(
                random => PropertyCheck.Sequence(random, r => PropertyCheck.Duration(r, maxSeconds: 30)),
                deltas =>
                {
                    foreach (var delta in deltas)
                    {
                        fixture.Advance(delta);
                        var current = fixture.Clock.ServerUtcNow;

                        Assert.GreaterOrEqual(current, previous);
                        previous = current;
                    }
                },
                // shrink здесь нет намеренно: часы общие на тест, и повторный прогон
                // уменьшенного входа сдвинул бы их ещё раз — «уменьшенный» контрпример врал бы.
                describe: deltas => $"{deltas.Count} deltas: {string.Join(", ", deltas)}");
        }

        [Test]
        public void ServerUtcNow_EqualsAnchorPlusTotalElapsed_ForAnyAdvanceSequence()
        {
            using var fixture = new Fixture();
            var total = TimeSpan.Zero;

            PropertyCheck.ForAll(
                random => PropertyCheck.Sequence(random, r => PropertyCheck.Duration(r, maxSeconds: 30)),
                deltas =>
                {
                    foreach (var delta in deltas)
                    {
                        fixture.Advance(delta);
                        total += delta;
                    }

                    // Ход часов задаёт монотонный источник, а не системное время: сумма сдвигов
                    // обязана давать ровно то же значение, что и накопленный ход.
                    Assert.AreEqual(ServerUtc + total, fixture.Clock.ServerUtcNow);
                },
                // shrink здесь нет намеренно: часы общие на тест, и повторный прогон
                // уменьшенного входа сдвинул бы их ещё раз — «уменьшенный» контрпример врал бы.
                describe: deltas => $"{deltas.Count} deltas: {string.Join(", ", deltas)}");
        }

        [Test]
        public void Countdown_EndsExactlyAtZero_ForAnyDeadline()
        {
            PropertyCheck.ForAll(
                random => TimeSpan.FromSeconds(random.Next(1, 10)),
                remaining =>
                {
                    using var fixture = new Fixture();
                    var observed = new List<TimeSpan>();

                    using var subscription = fixture.Clock
                        .Countdown(fixture.Clock.ServerUtcNow + remaining)
                        .Subscribe(observed.Add);

                    // Тиков с запасом: дойти до нуля обязан любой дедлайн из диапазона.
                    for (var i = 0; i < 12; i++)
                    {
                        fixture.Advance(TimeSpan.FromSeconds(1));
                    }

                    Assert.IsNotEmpty(observed);
                    Assert.AreEqual(TimeSpan.Zero, observed[observed.Count - 1]);

                    for (var i = 1; i < observed.Count; i++)
                    {
                        Assert.LessOrEqual(observed[i], observed[i - 1]);
                    }
                },
                cases: 8,
                describe: remaining => $"remaining {remaining}");
        }
    }
}
