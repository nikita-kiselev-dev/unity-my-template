using System;
using System.Numerics;
using Framework.Features.Items;
using Framework.Features.Items.Data;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    /// Регресс-бенч: метрика — байты GC на операцию, а не время
    /// (время шумит, аллокации детерминированы). Пороги подобраны по замеру и держат
    /// нынешнее поведение `BigInteger`: маленькое значение живёт в машинном слове,
    /// большое — в массиве, который и аллоцируется на каждой операции.
    public class ItemCounterAllocationTests
    {
        private const string Key = "gold";
        private const int Iterations = 100_000;
        private const int WarmupIterations = 1_000;

        // 10^40 — за пределами long, то есть BigInteger держит значение в uint[].
        private static readonly BigInteger LargeValue = BigInteger.Pow(10, 40);

        [Test]
        public void Add_AllocatesNothing_WhenValueFitsMachineWord()
        {
            var counter = Create(BigInteger.Zero);

            var bytesPerCall = Measure(() => counter.Add(BigInteger.One));

            Report("Add (значение в машинном слове)", bytesPerCall);
            Assert.That(bytesPerCall, Is.LessThanOrEqualTo(1d));
        }

        [Test]
        public void Add_AllocatesPerCall_WhenValueExceedsMachineWord()
        {
            var counter = Create(LargeValue);

            var bytesPerCall = Measure(() => counter.Add(BigInteger.One));

            Report("Add (значение в uint[])", bytesPerCall);
            Assert.That(bytesPerCall, Is.LessThanOrEqualTo(256d));
        }

        [Test]
        public void ToString_AllocatesPerCall_WhenValueExceedsMachineWord()
        {
            var value = LargeValue;

            var bytesPerCall = Measure(() => value.ToString());

            Report("ToString (значение в uint[])", bytesPerCall);
            Assert.That(bytesPerCall, Is.LessThanOrEqualTo(512d));
        }

        private static double Measure(Action operation)
        {
            for (var i = 0; i < WarmupIterations; i++)
            {
                operation();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < Iterations; i++)
            {
                operation();
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            return (after - before) / (double)Iterations;
        }

        // Console, а не TestContext: тест гоняется и кастомным раннером Tools/fast-tests.ps1,
        // где NUnit-контекст не поднят.
        private static void Report(string label, double bytesPerCall) =>
            Console.WriteLine($"[alloc] {label}: {bytesPerCall:F1} B/op");

        private static ItemCounter Create(BigInteger startValue)
        {
            var data = new ItemsData();
            data.PrepareNewData();
            data.AddNewItem(Key);

            if (startValue > BigInteger.Zero)
            {
                data.AddItem(Key, startValue);
            }

            return new ItemCounter(data, Key, "Gold", "Shiny", null);
        }
    }
}
