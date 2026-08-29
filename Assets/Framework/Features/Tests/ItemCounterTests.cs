using System.Collections.Generic;
using System.Numerics;
using Framework.Features.Items;
using Framework.Features.Items.Data;
using NUnit.Framework;
using R3;

namespace Framework.Features.Tests
{
    public class ItemCounterTests
    {
        private const string Key = "gold";

        private static ItemsData CreateData(int startValue)
        {
            var data = new ItemsData();
            data.PrepareNewData();
            data.AddNewItem(Key);

            if (startValue > 0)
            {
                data.AddItem(Key, startValue);
            }

            return data;
        }

        private static ItemCounter Create(ItemsData data)
        {
            return new ItemCounter(data, Key, "Gold", "Shiny", null);
        }

        [Test]
        public void Info_Value_IsSeededFromData()
        {
            var counter = Create(CreateData(7));

            Assert.AreEqual(new BigInteger(7), counter.Info.Value.CurrentValue);
        }

        [Test]
        public void Add_WritesToDataAndNotifiesSubscriber()
        {
            var data = CreateData(10);
            var counter = Create(data);
            var received = new List<BigInteger>();
            using var subscription = counter.Info.Value.Subscribe(value => received.Add(value));

            var result = counter.Add(5);

            Assert.IsTrue(result);
            Assert.AreEqual(new BigInteger(15), data.GetValue(Key).Value);
            Assert.AreEqual(new[] { new BigInteger(10), new BigInteger(15) }, received);
        }

        [Test]
        public void Remove_WritesToDataAndNotifiesSubscriber()
        {
            var data = CreateData(10);
            var counter = Create(data);
            var received = new List<BigInteger>();
            using var subscription = counter.Info.Value.Subscribe(value => received.Add(value));

            var result = counter.Remove(4);

            Assert.IsTrue(result);
            Assert.AreEqual(new BigInteger(6), data.GetValue(Key).Value);
            Assert.AreEqual(new[] { new BigInteger(10), new BigInteger(6) }, received);
        }

        [Test]
        public void Add_ReturnsFalse_AndDoesNotNotify_WhenCountIsNotPositive()
        {
            foreach (var count in new[] { 0, -1 })
            {
                var data = CreateData(10);
                var counter = Create(data);
                var received = new List<BigInteger>();
                using var subscription = counter.Info.Value.Subscribe(value => received.Add(value));

                var result = counter.Add(count);

                Assert.IsFalse(result);
                Assert.AreEqual(new BigInteger(10), data.GetValue(Key).Value);
                Assert.AreEqual(new[] { new BigInteger(10) }, received);
            }
        }

        [Test]
        public void Remove_ReturnsFalse_AndDoesNotNotify_WhenCountIsNotPositive()
        {
            foreach (var count in new[] { 0, -1 })
            {
                var data = CreateData(10);
                var counter = Create(data);
                var received = new List<BigInteger>();
                using var subscription = counter.Info.Value.Subscribe(value => received.Add(value));

                var result = counter.Remove(count);

                Assert.IsFalse(result);
                Assert.AreEqual(new BigInteger(10), data.GetValue(Key).Value);
                Assert.AreEqual(new[] { new BigInteger(10) }, received);
            }
        }

        [Test]
        public void Remove_ReturnsFalse_AndDoesNotNotify_WhenNotEnough()
        {
            var data = CreateData(3);
            var counter = Create(data);
            var received = new List<BigInteger>();
            using var subscription = counter.Info.Value.Subscribe(value => received.Add(value));

            var result = counter.Remove(5);

            Assert.IsFalse(result);
            Assert.AreEqual(new BigInteger(3), data.GetValue(Key).Value);
            Assert.AreEqual(new[] { new BigInteger(3) }, received);
        }
    }
}
