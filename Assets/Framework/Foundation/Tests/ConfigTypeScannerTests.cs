using System;
using Framework.Foundation.Configs;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ConfigTypeScannerTests
    {
        [Test]
        public void Scan_ReturnsEntry_WhenTypeHasConfigAttribute()
        {
            var entries = ConfigTypeScanner.Scan(new[] { typeof(AnnotatedConfig) });

            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual(typeof(AnnotatedConfig), entries[0].ConfigType);
            Assert.AreEqual("annotated_config", entries[0].ConfigKey);
        }

        [Test]
        public void Scan_SkipsType_WhenConfigAttributeMissing()
        {
            var entries = ConfigTypeScanner.Scan(new[] { typeof(PlainConfig) });

            Assert.IsEmpty(entries);
        }

        [Test]
        public void Scan_SkipsType_WhenNotAnnotated()
        {
            var entries = ConfigTypeScanner.Scan(new[] { typeof(string), typeof(ConfigTypeScannerTests) });

            Assert.IsEmpty(entries);
        }

        [Test]
        public void Scan_SkipsAbstractType_WithConfigAttribute()
        {
            var entries = ConfigTypeScanner.Scan(new[] { typeof(AbstractAnnotatedConfig) });

            Assert.IsEmpty(entries);
        }

        [Test]
        public void Scan_Throws_WhenConfigAttributeOnNonConfigType()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => ConfigTypeScanner.Scan(new[] { typeof(AnnotatedNonConfig) }));

            Assert.That(exception.Message, Does.Contain(nameof(AnnotatedNonConfig)));
        }

        [ConfigKey("annotated_config")]
        private sealed class AnnotatedConfig : IConfig
        {
            public bool IsEnabled => true;
        }

        private sealed class PlainConfig : IConfig
        {
            public bool IsEnabled => true;
        }

        [ConfigKey("abstract_config")]
        private abstract class AbstractAnnotatedConfig : IConfig
        {
            public bool IsEnabled => true;
        }

        [ConfigKey("not_a_config")]
        private sealed class AnnotatedNonConfig
        {
        }
    }
}
