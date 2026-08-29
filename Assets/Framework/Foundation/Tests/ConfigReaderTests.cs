using System;
using Framework.Foundation.Configs;
using Framework.Foundation.LiveOps.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ConfigReaderTests
    {
        [Test]
        public void Init_QuarantinesCache_WhenJsonIsCorrupted()
        {
            var signalBus = new ReactiveSignalBus();
            var storage = new FakeConfigStorage { LoadedJson = "{invalid" };
            var logChannelFactory = new FakeLogChannelFactory();

            var manager = new ConfigReader(null, signalBus, new FakeRemoteConfigSource(), storage, logChannelFactory);

            Assert.AreEqual(1, storage.QuarantineCount);
            Assert.AreEqual(1, logChannelFactory.Logger.Errors.Count);
            manager.Dispose();
            signalBus.Dispose();
        }

        [Test]
        public void Read_ReturnsNoValue_WhenResolverExhaustsEverySource()
        {
            var signalBus = new ReactiveSignalBus();
            var logChannelFactory = new FakeLogChannelFactory();
            var resolver = new FakeConfigResolver
            {
                ReadThrows = new InvalidOperationException("every source is invalid")
            };
            var reader = new ConfigReader(
                null,
                signalBus,
                new FakeRemoteConfigSource(),
                new FakeConfigStorage(),
                logChannelFactory,
                resolver);

            var result = reader.Read(typeof(IConfig), "config").GetAwaiter().GetResult();

            Assert.IsFalse(result.HasValue);
            Assert.AreEqual(1, logChannelFactory.Logger.Errors.Count);
            reader.Dispose();
            signalBus.Dispose();
        }

        [Test]
        public void ServerLogin_UsesSingleValueSnapshot()
        {
            var signalBus = new ReactiveSignalBus();
            var storage = new FakeConfigStorage();
            var service = new FakeRemoteConfigSource();
            service.Values["config"] = "{}";
            var manager = new ConfigReader(null, signalBus, service, storage, new FakeLogChannelFactory());

            signalBus.Trigger<ServerLoginCompletedSignal>();

            Assert.AreEqual(1, service.GetValuesCount);
            Assert.IsNotNull(storage.SavedJson);
            manager.Dispose();
            signalBus.Dispose();
        }
    }
}
