using System;
using System.Buffers.Binary;
using System.Numerics;
using Framework.Foundation.SaveLoad;
using Framework.Foundation.SaveLoad.Serialization;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class SaveEnvelopeTests
    {
        [OneTimeSetUp]
        public void RegisterFormatters()
        {
            SaveLoadBootstrap.Init();
        }

        private static SaveEnvelope CreateManager(params Framework.Foundation.SaveLoad.SaveBlob[] datas)
        {
            return new SaveEnvelope(datas, new FakeLogChannel());
        }

        private static SaveEnvelope CreateManager(FakeLogChannel logger, params Framework.Foundation.SaveLoad.SaveBlob[] datas)
        {
            return new SaveEnvelope(datas, logger);
        }

        private static AmountTestData CreateAmountData(int amount)
        {
            var data = new AmountTestData();
            data.PrepareNewData();
            data.Amount = amount;
            return data;
        }

        [Test]
        public void SerializeDeserialize_RestoresState_IntoSameInstance()
        {
            var data = CreateAmountData(50);
            var manager = CreateManager(data);

            var bytes = manager.Serialize();
            data.Amount += 25;
            manager.Deserialize(bytes);

            Assert.AreEqual(new BigInteger(50), data.Amount);
        }

        [Test]
        public void Deserialize_EmptyBytes_ResetsToNewData()
        {
            var data = CreateAmountData(50);
            var manager = CreateManager(data);

            manager.Deserialize(ReadOnlySpan<byte>.Empty);

            Assert.AreEqual(BigInteger.Zero, data.Amount);
        }

        [Test]
        public void Deserialize_SkipsUnknownTag()
        {
            var data = CreateAmountData(50);
            var manager = CreateManager(data);

            // count=1, tag=999 (РЅРµ Р·Р°СЂРµРіРёСЃС‚СЂРёСЂРѕРІР°РЅ), version=1, payload РёР· С‚СЂС‘С… РЅСѓР»РµРІС‹С… Р±Р°Р№С‚.
            var bytes = new byte[sizeof(int) + sizeof(ushort) + sizeof(ushort) + sizeof(int) + 3];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 999);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 3);

            Assert.DoesNotThrow(() => manager.Deserialize(bytes));
        }

        [Test]
        public void Deserialize_MigratesData_WhenPayloadVersionIsOlder()
        {
            var old = new VersionedTestDataV1 { Legacy = 42 };
            var current = new VersionedTestDataV2();

            var bytes = CreateManager(old).Serialize();
            CreateManager(current).Deserialize(bytes);

            Assert.AreEqual(1, current.MigratedFrom);
            Assert.AreEqual(42, current.Current);
        }

        [Test]
        public void Deserialize_SkipsMigration_WhenPayloadVersionIsCurrent()
        {
            var source = new VersionedTestDataV2 { Current = 7 };
            var target = new VersionedTestDataV2();

            var bytes = CreateManager(source).Serialize();
            CreateManager(target).Deserialize(bytes);

            Assert.AreEqual(0, target.MigratedFrom);
            Assert.AreEqual(7, target.Current);
        }

        [Test]
        public void Deserialize_Throws_WhenPayloadVersionIsFromFuture()
        {
            var future = new VersionedTestDataV2 { Current = 7 };
            var current = new VersionedTestDataV1();

            var bytes = CreateManager(future).Serialize();
            var manager = CreateManager(current);

            Assert.Throws<InvalidOperationException>(() => manager.Deserialize(bytes));
        }

        /// Сбой одного блоба не должен уносить чужой прогресс: конверт секционирован по длине.
        [Test]
        public void Deserialize_ResetsOnlyBrokenBlob_WhenSchemaShrank()
        {
            var bytes = CreateManager(new ShrunkTestDataWide { First = 1, Second = 2 }, CreateAmountData(50)).Serialize();

            var shrunk = new ShrunkTestDataNarrow();
            var items = new AmountTestData();
            var logger = new FakeLogChannel();

            Assert.DoesNotThrow(() => CreateManager(logger, shrunk, items).Deserialize(bytes));

            Assert.AreEqual(ShrunkTestData.ResetMarker, shrunk.First, "Сломанный блоб должен получить PrepareNewData.");
            Assert.AreEqual(new BigInteger(50), items.Amount, "Соседний блоб должен загрузиться.");
            Assert.AreEqual(1, logger.Errors.Count, "Потеря блоба обязана быть видна в логе.");
        }

        [Test]
        public void Deserialize_ResetsBlob_WithoutError_WhenVersionIsBelowMinReadable()
        {
            var bytes = CreateManager(new ShrunkTestDataWide { First = 1, Second = 2 }, CreateAmountData(50)).Serialize();

            var guarded = new ShrunkTestDataGuarded();
            var items = new AmountTestData();
            var logger = new FakeLogChannel();

            CreateManager(logger, guarded, items).Deserialize(bytes);

            Assert.AreEqual(ShrunkTestData.ResetMarker, guarded.First);
            Assert.AreEqual(new BigInteger(50), items.Amount);
            Assert.IsEmpty(logger.Errors, "Осознанный сброс схемы — не ошибка.");
        }

        [Test]
        public void Init_Throws_WhenMinReadableVersionIsAboveCurrentVersion()
        {
            Assert.Throws<InvalidOperationException>(
                () => _ = CreateManager(new InvalidVersionData()));
        }

        [Test]
        public void Init_Throws_WhenSaveTagMissing()
        {
            Assert.Throws<InvalidOperationException>(
                () => _ = CreateManager(new NoTagData()));
        }

        [Test]
        public void Init_Throws_OnDuplicateTag()
        {
            var first = CreateAmountData(1);
            var second = CreateAmountData(2);

            Assert.Throws<InvalidOperationException>(
                () => _ = CreateManager(first, second));
        }

        private sealed class NoTagData : Framework.Foundation.SaveLoad.SaveBlob
        {
            public override void PrepareNewData()
            {
            }
        }

        [SaveTag(60003)]
        private sealed class InvalidVersionData : Framework.Foundation.SaveLoad.SaveBlob
        {
            public override ushort CurrentVersion => 2;
            public override ushort MinReadableVersion => 3;

            public override void PrepareNewData()
            {
            }
        }
    }
}
