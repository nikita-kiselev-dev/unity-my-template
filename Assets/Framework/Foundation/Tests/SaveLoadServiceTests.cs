using System.Threading;
using Framework.Foundation.SaveLoad;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class SaveLoadServiceTests
    {
        private FakeSaveEnvelope _saveEnvelope;
        private FakeSaveStorage _storage;
        private FakeLogChannel _logger;

        [SetUp]
        public void Setup()
        {
            _saveEnvelope = new FakeSaveEnvelope();
            _storage = new FakeSaveStorage();
            _logger = new FakeLogChannel();
        }

        [Test]
        public void Load_PreparesNewData_WhenStorageEmpty()
        {
            CreateLoadedService();

            Assert.AreEqual(1, _saveEnvelope.PrepareNewDataCount);
            Assert.AreEqual(0, _saveEnvelope.DeserializedBytes.Count);
        }

        [Test]
        public void Load_DeserializesBytes_WhenStorageHasSave()
        {
            _storage.ReadResult = SaveReadResult.Success(new byte[] { 7, 8, 9 });

            CreateLoadedService();

            CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, _saveEnvelope.DeserializedBytes[0]);
        }

        [Test]
        public void Load_QuarantinesSaveAndStartsFresh_WhenDeserializationFails()
        {
            _storage.ReadResult = SaveReadResult.Success(new byte[] { 7 });
            _saveEnvelope.DeserializeThrows = true;

            CreateLoadedService();

            Assert.AreEqual(1, _storage.QuarantineCount);
            Assert.AreEqual(1, _saveEnvelope.PrepareNewDataCount);
            Assert.AreEqual(1, _logger.Errors.Count);
        }

        [Test]
        public void Load_QuarantinesSaveAndStartsFresh_WhenStorageReportsCorruption()
        {
            _storage.ReadResult = SaveReadResult.Corrupted();

            CreateLoadedService();

            Assert.AreEqual(1, _storage.QuarantineCount);
            Assert.AreEqual(1, _saveEnvelope.PrepareNewDataCount);
            Assert.AreEqual(1, _logger.Errors.Count);
        }

        [Test]
        public void Decode_ReturnsCorrupted_WhenPlayerPrefsBase64IsInvalid()
        {
            var result = PlayerPrefsSaveStorage.Decode("not-base64");

            Assert.AreEqual(SaveReadStatus.Corrupted, result.Status);
        }

        [Test]
        public void Init_MarksEntityEnabled()
        {
            var service = CreateLoadedService();

            service.InitPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(service.Status.IsEnabled);
        }

        [Test]
        public void SaveData_DoesNothing_BeforeLoad()
        {
            var service = new SaveLoadService(_saveEnvelope, _storage, _logger);

            service.SaveData();

            Assert.AreEqual(0, _storage.Writes.Count);
        }

        [Test]
        public void SaveData_WritesSerializedBytes()
        {
            _saveEnvelope.SerializedBytes = new byte[] { 4, 5, 6 };
            var service = CreateLoadedService();

            service.SaveData();

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, _storage.Writes[0]);
        }

        [Test]
        public void SaveDataImmediate_WritesSerializedBytesSynchronously()
        {
            _saveEnvelope.SerializedBytes = new byte[] { 4, 5, 6 };
            var service = CreateLoadedService();

            service.SaveDataImmediate();

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, _storage.ImmediateWrites[0]);
            Assert.AreEqual(0, _storage.Writes.Count);
        }

        [Test]
        public void SaveData_CoalescesRepeatedCalls_WhileWriteInFlight()
        {
            _storage.CompleteWritesImmediately = false;
            var service = CreateLoadedService();

            service.SaveData();
            service.SaveData();
            service.SaveData();

            Assert.AreEqual(1, _storage.Writes.Count);

            // Завершение первой записи запускает ровно один отложенный пересейв.
            _storage.CompleteWrite();
            Assert.AreEqual(2, _storage.Writes.Count);

            _storage.CompleteWrite();
            Assert.AreEqual(2, _storage.Writes.Count);
        }

        [Test]
        public void SaveData_StartsNewWrite_AfterQueueDrained()
        {
            _storage.CompleteWritesImmediately = false;
            var service = CreateLoadedService();

            service.SaveData();
            _storage.CompleteWrite();

            service.SaveData();

            Assert.AreEqual(2, _storage.Writes.Count);
        }

        private SaveLoadService CreateLoadedService()
        {
            var service = new SaveLoadService(_saveEnvelope, _storage, _logger);
            service.LoadPhase(CancellationToken.None).GetAwaiter().GetResult();
            return service;
        }
    }
}
