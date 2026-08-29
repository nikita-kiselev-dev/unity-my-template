using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Framework.Features.Items;
using Framework.Features.Items.Data;
using Framework.Foundation.SaveLoad.Serialization;
using MemoryPack;
using NUnit.Framework;
using R3;

namespace Framework.Features.Tests
{
    public class ItemsDataTests
    {
        private const string Key = "gold";

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            SaveLoadBootstrap.Init();
        }

        private static ItemsData Create()
        {
            var data = new ItemsData();
            data.PrepareNewData();
            return data;
        }

        [Test]
        public void AddNewItem_CreatesItemWithZeroValue()
        {
            var data = Create();

            data.AddNewItem(Key);

            Assert.IsTrue(data.GetValue(Key).HasValue);
            Assert.AreEqual(BigInteger.Zero, data.GetValue(Key).Value);
        }

        [Test]
        public void AddNewItem_DoesNotResetExistingValue()
        {
            var data = Create();
            data.AddNewItem(Key);
            data.AddItem(Key, 5);

            data.AddNewItem(Key);

            Assert.AreEqual(new BigInteger(5), data.GetValue(Key).Value);
        }

        [Test]
        public void AddItem_IncreasesValue()
        {
            var data = Create();
            data.AddNewItem(Key);

            var result = data.AddItem(Key, 42);

            Assert.IsTrue(result);
            Assert.AreEqual(new BigInteger(42), data.GetValue(Key).Value);
        }

        [Test]
        public void AddItem_ReturnsFalse_ForUnknownKey()
        {
            var data = Create();

            Assert.IsFalse(data.AddItem(Key, 1));
        }

        [Test]
        public void AddItem_ReturnsFalse_AndKeepsValue_WhenCountIsNotPositive()
        {
            foreach (var count in new[] { 0, -1 })
            {
                var data = Create();
                data.AddNewItem(Key);
                data.AddItem(Key, 5);

                var result = data.AddItem(Key, count);

                Assert.IsFalse(result);
                Assert.AreEqual(new BigInteger(5), data.GetValue(Key).Value);
            }
        }

        [Test]
        public void RemoveItem_DecreasesValue()
        {
            var data = Create();
            data.AddNewItem(Key);
            data.AddItem(Key, 10);

            var result = data.RemoveItem(Key, 4);

            Assert.IsTrue(result);
            Assert.AreEqual(new BigInteger(6), data.GetValue(Key).Value);
        }

        [Test]
        public void RemoveItem_AllowsRemovingToZero()
        {
            var data = Create();
            data.AddNewItem(Key);
            data.AddItem(Key, 10);

            var result = data.RemoveItem(Key, 10);

            Assert.IsTrue(result);
            Assert.AreEqual(BigInteger.Zero, data.GetValue(Key).Value);
        }

        [Test]
        public void RemoveItem_ReturnsFalse_AndKeepsValue_WhenNotEnough()
        {
            var data = Create();
            data.AddNewItem(Key);
            data.AddItem(Key, 3);

            var result = data.RemoveItem(Key, 5);

            Assert.IsFalse(result);
            Assert.AreEqual(new BigInteger(3), data.GetValue(Key).Value);
        }

        [Test]
        public void RemoveItem_ReturnsFalse_ForUnknownKey()
        {
            var data = Create();

            Assert.IsFalse(data.RemoveItem(Key, 1));
        }

        [Test]
        public void RemoveItem_ReturnsFalse_AndKeepsValue_WhenCountIsNotPositive()
        {
            foreach (var count in new[] { 0, -1 })
            {
                var data = Create();
                data.AddNewItem(Key);
                data.AddItem(Key, 5);

                var result = data.RemoveItem(Key, count);

                Assert.IsFalse(result);
                Assert.AreEqual(new BigInteger(5), data.GetValue(Key).Value);
            }
        }

        [Test]
        public void GetValue_ReturnsFailure_ForUnknownKey()
        {
            var data = Create();

            Assert.IsFalse(data.GetValue(Key).HasValue);
        }

        [Test]
        public void Items_StorePlainValues_WithoutReactiveWrappers()
        {
            var property = typeof(ItemsData).GetProperty(
                nameof(ItemsData.Items),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.AreEqual(typeof(Dictionary<string, BigInteger>), property.PropertyType);
        }

        [Test]
        public void ItemConfig_Value_ExposesReadOnlyReactiveProperty()
        {
            var property = typeof(ItemInfo).GetProperty(nameof(ItemInfo.Value));

            Assert.AreEqual(typeof(ReadOnlyReactiveProperty<BigInteger>), property.PropertyType);
        }

        [Test]
        public void MemoryPackRoundtrip_PreservesState_InExistingInstance()
        {
            var source = Create();
            source.AddNewItem(Key);
            source.AddItem(Key, 42);
            var bytes = MemoryPackSerializer.Serialize(source);
            var target = Create();
            object refValue = target;

            MemoryPackSerializer.Deserialize(typeof(ItemsData), bytes, ref refValue);

            Assert.AreSame(target, refValue);
            Assert.AreEqual(new BigInteger(42), target.GetValue(Key).Value);
        }
    }
}
