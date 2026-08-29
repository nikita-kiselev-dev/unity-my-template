using System;
using System.Collections.Generic;
using System.Threading;
using Framework.Foundation.Configs;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ConfigProviderTests
    {
        [Test]
        public void Get_ReturnsLoadedConfig_AfterWarmUp()
        {
            var first = new first();
            var second = new second();
            var provider = CreateProvider(first, second);

            WarmUp(provider);

            Assert.AreSame(first, provider.Get(typeof(first)));
            Assert.AreSame(second, provider.Get(typeof(second)));
        }

        [Test]
        public void Get_Throws_BeforeWarmUp()
        {
            var provider = CreateProvider(new first());

            var exception = Assert.Throws<InvalidOperationException>(() => provider.Get(typeof(first)));

            Assert.That(exception.Message, Does.Contain(nameof(first)));
        }

        [Test]
        public void Get_Throws_WhenTypeIsNotRegistered()
        {
            var provider = CreateProvider(new first());
            WarmUp(provider);

            Assert.Throws<InvalidOperationException>(() => provider.Get(typeof(second)));
        }

        [Test]
        public void WarmUp_ReadsEachConfigOnce_WhenCalledTwice()
        {
            var reader = new FakeConfigReader(new Dictionary<Type, IConfig> { [typeof(first)] = new first() });
            var provider = new ConfigProvider(reader, new[] { new ConfigTypeEntry(typeof(first), "first") });

            WarmUp(provider);
            WarmUp(provider);

            Assert.AreEqual(1, reader.ReadCount);
        }

        [Test]
        public void WarmUp_Throws_WhenConfigIsMissing()
        {
            var reader = new FakeConfigReader(new Dictionary<Type, IConfig>());
            var provider = new ConfigProvider(reader, new[] { new ConfigTypeEntry(typeof(first), "first") });

            var exception = Assert.Throws<InvalidOperationException>(() => WarmUp(provider));

            Assert.That(exception.Message, Does.Contain("first"));
        }

        [Test]
        public void WarmUp_LoadsConfigs_WhenRetriedAfterFailure()
        {
            var reader = new FakeConfigReader(new Dictionary<Type, IConfig>());
            var provider = new ConfigProvider(reader, new[] { new ConfigTypeEntry(typeof(first), "first") });
            Assert.Throws<InvalidOperationException>(() => WarmUp(provider));

            var config = new first();
            reader.AddConfig(typeof(first), config);
            WarmUp(provider);

            Assert.AreSame(config, provider.Get(typeof(first)));
            Assert.AreEqual(2, reader.ReadCount);
        }

        [Test]
        public void WarmUp_LoadsConfigs_WhenRetriedAfterCancellation()
        {
            var config = new first();
            var reader = new FakeConfigReader(new Dictionary<Type, IConfig> { [typeof(first)] = config })
            {
                ThrowOnNextRead = new OperationCanceledException()
            };
            var provider = new ConfigProvider(reader, new[] { new ConfigTypeEntry(typeof(first), "first") });
            Assert.Throws<OperationCanceledException>(() => WarmUp(provider));

            WarmUp(provider);

            Assert.AreSame(config, provider.Get(typeof(first)));
        }

        private static ConfigProvider CreateProvider(params IConfig[] configs)
        {
            var map = new Dictionary<Type, IConfig>();
            var entries = new List<ConfigTypeEntry>();

            foreach (var config in configs)
            {
                var type = config.GetType();
                map[type] = config;
                entries.Add(new ConfigTypeEntry(type, type.Name.ToLowerInvariant()));
            }

            return new ConfigProvider(new FakeConfigReader(map), entries);
        }

        private static void WarmUp(ConfigProvider provider)
        {
            provider.WarmUp(CancellationToken.None).GetAwaiter().GetResult();
        }

        private sealed class first : IConfig
        {
            public bool IsEnabled => true;
        }

        private sealed class second : IConfig
        {
            public bool IsEnabled => true;
        }
    }
}
