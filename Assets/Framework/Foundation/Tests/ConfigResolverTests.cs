using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Configs;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ConfigResolverTests
    {
        private const string ConfigName = "test_config";
        private const string Malformed = "{not json";

        private FakeLogChannel _logger;

        [SetUp]
        public void SetUp()
        {
            _logger = new FakeLogChannel();
        }

        [Test]
        public void Read_ReturnsCachedValue_WhenServerValueAbsent()
        {
            var resolver = CreateResolver(new Dictionary<string, string> { [ConfigName] = Json(1) });

            var config = Read(resolver);

            Assert.AreEqual(1, config.Value);
        }

        [Test]
        public void Read_PrefersServerValue_WhenBothSourcesHaveConfig()
        {
            var serverValues = new Dictionary<string, string> { [ConfigName] = Json(2) };
            var resolver = CreateResolver(new Dictionary<string, string> { [ConfigName] = Json(1) });

            resolver.SetServerValues(serverValues);
            var config = Read(resolver);

            Assert.AreEqual(2, config.Value);
        }

        [Test]
        public void Read_FallsBackToCachedValue_WhenServerMissesConfig()
        {
            var serverValues = new Dictionary<string, string> { ["other_config"] = Json(9) };
            var resolver = CreateResolver(new Dictionary<string, string> { [ConfigName] = Json(1) });

            resolver.SetServerValues(serverValues);
            var config = Read(resolver);

            Assert.AreEqual(1, config.Value);
        }

        [Test]
        public void Read_FallsBackToCached_WhenServerValueIsMalformed()
        {
            var serverValues = new Dictionary<string, string> { [ConfigName] = Malformed };
            var resolver = CreateResolver(new Dictionary<string, string> { [ConfigName] = Json(1) });

            resolver.SetServerValues(serverValues);
            var config = Read(resolver);

            Assert.AreEqual(1, config.Value);
        }

        [Test]
        public void Read_FallsBackToDummy_WhenServerAndCacheAreMalformed()
        {
            var serverValues = new Dictionary<string, string> { [ConfigName] = Malformed };
            var resolver = CreateResolver(
                new Dictionary<string, string> { [ConfigName] = Malformed },
                dummyJson: Json(7));

            resolver.SetServerValues(serverValues);
            var config = Read(resolver);

            Assert.AreEqual(7, config.Value);
        }

        [Test]
        public void Read_Throws_WhenDummyIsMalformed()
        {
            var resolver = CreateResolver(null, dummyJson: Malformed);

            Assert.Throws<InvalidOperationException>(
                () => resolver.Read(typeof(TestConfig), ConfigName).GetAwaiter().GetResult());
        }

        [Test]
        public void Read_LogsError_ForEachFailedSource()
        {
            var serverValues = new Dictionary<string, string> { [ConfigName] = Malformed };
            var resolver = CreateResolver(
                new Dictionary<string, string> { [ConfigName] = Malformed },
                dummyJson: Json(7));

            resolver.SetServerValues(serverValues);
            Read(resolver);

            Assert.AreEqual(2, _logger.Errors.Count);
            Assert.IsTrue(_logger.Errors[0].Contains(ConfigName));
            Assert.IsTrue(_logger.Errors[1].Contains(ConfigName));
        }

        [Test]
        public void SetServerValues_TriggersSave_WhenServerDiffersFromCache()
        {
            var savesCount = 0;
            var serverValues = new Dictionary<string, string> { [ConfigName] = Json(2) };
            var resolver = CreateResolver(
                new Dictionary<string, string> { [ConfigName] = Json(1) },
                saveAction: () => savesCount++);

            resolver.SetServerValues(serverValues);

            Assert.AreEqual(1, savesCount);
        }

        [Test]
        public void SetServerValues_SkipsSave_WhenServerEqualsCache()
        {
            var savesCount = 0;
            var serverValues = new Dictionary<string, string> { [ConfigName] = Json(1) };
            var resolver = CreateResolver(
                new Dictionary<string, string> { [ConfigName] = Json(1) },
                saveAction: () => savesCount++);

            resolver.SetServerValues(serverValues);

            Assert.AreEqual(0, savesCount);
        }

        [Test]
        public void SetServerValues_TriggersSave_WhenCacheMissing()
        {
            var savesCount = 0;
            var serverValues = new Dictionary<string, string> { [ConfigName] = Json(1) };
            var resolver = CreateResolver(null, saveAction: () => savesCount++);

            resolver.SetServerValues(serverValues);

            Assert.AreEqual(1, savesCount);
        }

        private ConfigResolver CreateResolver(
            Dictionary<string, string> cachedValues,
            string dummyJson = null,
            Action saveAction = null)
        {
            return new ConfigResolver(
                (_, _) => UniTask.FromResult(dummyJson ?? Json(0)),
                cachedValues,
                _logger,
                saveAction ?? (() => { }));
        }

        private static TestConfig Read(ConfigResolver resolver) =>
            (TestConfig)resolver.Read(typeof(TestConfig), ConfigName).GetAwaiter().GetResult();

        private static string Json(int value) => $"{{\"IsEnabled\":true,\"Value\":{value}}}";

        private sealed class TestConfig : IConfig
        {
            public bool IsEnabled { get; set; }
            public int Value { get; set; }
        }
    }
}
