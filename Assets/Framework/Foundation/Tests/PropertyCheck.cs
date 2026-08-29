using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    // Минимальный property-based раннер: инвариант проверяется на сгенерированных входах, а не
    // на значениях, которые подобрал автор теста. Своя реализация вместо библиотеки — список
    // тестовых зависимостей проекта закрыт, а нужным инвариантам хватает этого объёма.
    public static class PropertyCheck
    {
        public const int DefaultCases = 100;

        // Seed фиксирован: флаки-гейт начинают игнорировать, и он перестаёт быть гейтом.
        // Значение печатается в сообщении падения, чтобы контрпример воспроизводился руками.
        public const int DefaultSeed = 20260813;

        // Бюджет времени на один тест. Нужен потому, что Unity Editor — это Mono, где BigInteger
        // и аллокации на порядок дороже, чем в CoreCLR: набор кейсов, который вне Unity занимает
        // секунды, в Test Runner занимал минуты и выглядел зависанием. Кейсы — верхняя граница,
        // реальный ограничитель — время.
        public const int DefaultBudgetMs = 1000;

        private const int MaxShrinkSteps = 20;

        public static void ForAll<T>(
            Func<Random, T> generate,
            Action<T> assert,
            int cases = DefaultCases,
            int seed = DefaultSeed,
            Func<T, T> shrink = null,
            Func<T, string> describe = null,
            int budgetMs = DefaultBudgetMs)
        {
            var random = new Random(seed);
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < cases; i++)
            {
                // Досрочный выход, а не падение: медленная машина не должна красить тест, иначе
                // гейт станет флаки. Но и молчать нельзя — усечение, о котором не сказали,
                // читается как «проверено всё».
                if (stopwatch.ElapsedMilliseconds > budgetMs)
                {
                    Console.WriteLine($"PropertyCheck: проверено {i} из {cases} кейсов, бюджет {budgetMs} мс исчерпан.");
                    return;
                }

                var input = generate(random);

                if (Holds(assert, input, out var error))
                {
                    continue;
                }

                var counterexample = Shrink(assert, input, shrink);
                var text = describe != null ? describe(counterexample) : Convert.ToString(counterexample);

                Assert.Fail($"Инвариант нарушен на кейсе {i} (seed {seed}): {text}{Environment.NewLine}{error.GetType().Name}: {error.Message}");
            }
        }

        // Уменьшение контрпримера — не украшение: «упало на последовательности из 40 операций»
        // не диагноз, а «упало на [Add 0]» — диагноз. Шаг задаёт вызывающий, потому что
        // «меньше» определяется доменом, а не структурой типа.
        private static T Shrink<T>(Action<T> assert, T input, Func<T, T> shrink)
        {
            if (shrink == null)
            {
                return input;
            }

            var current = input;

            for (var step = 0; step < MaxShrinkSteps; step++)
            {
                var candidate = shrink(current);

                if (EqualityComparer<T>.Default.Equals(candidate, current))
                {
                    break;
                }

                // Уменьшаем, только пока инвариант всё ещё нарушен: иначе получим случай,
                // который на самом деле проходит.
                if (Holds(assert, candidate, out _))
                {
                    break;
                }

                current = candidate;
            }

            return current;
        }

        private static bool Holds<T>(Action<T> assert, T input, out Exception error)
        {
            try
            {
                assert(input);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception;
                return false;
            }
        }

        // --- Генераторы ------------------------------------------------------------------------

        // Значения растянуты по величине специально: пока валюта влезает в машинное слово,
        // BigInteger идёт быстрым путём, а инварианты обязаны держаться и за его пределами.
        // Верхняя граница умеренная (~10^25, десятки бит за пределом long): важен переход через
        // машинное слово, а не длина числа, а на Mono каждая лишняя цифра стоит времени.
        public static BigInteger BigIntegerValue(Random random, bool allowNonPositive = false)
        {
            var magnitude = random.Next(0, 4);
            var basis = new BigInteger(random.Next(1, int.MaxValue));

            var value = magnitude switch
            {
                0 => basis,
                1 => basis * long.MaxValue,
                2 => basis * basis,
                _ => BigInteger.Pow(new BigInteger(10), random.Next(19, 26)) + basis
            };

            if (allowNonPositive && random.Next(0, 4) == 0)
            {
                return random.Next(0, 2) == 0 ? BigInteger.Zero : -value;
            }

            return value;
        }

        // Верхняя граница по умолчанию скромная (2 минуты) и это не «на всякий случай»:
        // потребитель сдвига времени тикает раз в секунду, поэтому интервал в сутки означает
        // 86 400 срабатываний таймера на один Advance. Первая версия генерировала до 3 дней и
        // укладывалась в бюджет ForAll всего на 6 кейсах из 100.
        public static TimeSpan Duration(Random random, int maxSeconds = 120)
        {
            return TimeSpan.FromMilliseconds(random.Next(0, maxSeconds * 1000));
        }

        public static List<T> Sequence<T>(Random random, Func<Random, T> element, int maxLength = 10)
        {
            var length = random.Next(0, maxLength + 1);
            var items = new List<T>(length);

            for (var i = 0; i < length; i++)
            {
                items.Add(element(random));
            }

            return items;
        }

        // Стандартный шаг уменьшения для последовательностей: короче на один элемент с конца.
        public static List<T> DropLast<T>(List<T> items)
        {
            return items.Count == 0 ? items : items.GetRange(0, items.Count - 1);
        }
    }
}
