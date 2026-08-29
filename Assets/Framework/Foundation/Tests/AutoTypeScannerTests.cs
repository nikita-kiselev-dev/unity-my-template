using System;
using System.Linq;
using System.Reflection;
using Framework.Foundation.Ads;
using Framework.Foundation.Ads.Data;
using Framework.Foundation.Ads.Configs;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators;
using Framework.Foundation.Signals;
using NUnit.Framework;
using VContainer;

namespace Framework.Foundation.Tests
{
    public class AutoTypeScannerTests
    {
        private static readonly Assembly _core = typeof(LifecycleEntity).Assembly;
        private static readonly Assembly _tests = typeof(AutoTypeScannerTests).Assembly;

        [Test]
        public void Scan_ClassifiesLifecycleEntity_WhenTypeInheritsLifecycleEntity()
        {
            var entry = FindAutoType(typeof(AdsController));

            Assert.AreEqual(AutoTypeKind.LifecycleEntity, entry.Kind);
            Assert.AreEqual(Lifetime.Singleton, entry.Lifetime);
        }

        [Test]
        public void Scan_ClassifiesService_WhenTypeIsNotLifecycleEntity()
        {
            var entry = FindAutoType(typeof(ReactiveSignalBus));

            Assert.AreEqual(AutoTypeKind.Service, entry.Kind);
            Assert.AreEqual(Lifetime.Singleton, entry.Lifetime);
        }

        [Test]
        public void Scan_UsesDefaultLifetime_WhenAttributeHasNoArgument()
        {
            var entry = FindAutoType(typeof(LifecycleDecoratorPipeline));

            Assert.AreEqual(Lifetime.Scoped, entry.Lifetime);
        }

        [Test]
        public void Scan_ClassifiesData_WithoutAttribute()
        {
            var entry = FindAutoType(typeof(AdsData));

            Assert.AreEqual(AutoTypeKind.SaveBlob, entry.Kind);
            Assert.AreEqual(Lifetime.Singleton, entry.Lifetime);
        }

        [Test]
        public void Scan_ReturnsConfigEntry_ForAnnotatedType()
        {
            var result = AutoTypeScanner.Scan(new[] { _core });

            var entry = result.Configs.Single(config => config.ConfigType == typeof(AdsConfig));

            Assert.AreEqual(AdsConstants.Configs.Key, entry.ConfigKey);
            Assert.IsFalse(result.AutoTypes.Any(auto => auto.Type == typeof(AdsConfig)));
        }

        [Test]
        public void Scan_SkipsAbstractTypes()
        {
            var result = AutoTypeScanner.Scan(new[] { _core });

            Assert.IsFalse(result.AutoTypes.Any(entry => entry.Type.IsAbstract));
            Assert.IsFalse(result.Configs.Any(entry => entry.ConfigType.IsAbstract));
        }

        [Test]
        public void Scan_ReturnsNoDuplicates()
        {
            var result = AutoTypeScanner.Scan(new[] { _core, _core });

            var types = result.AutoTypes.Select(entry => entry.Type).ToArray();
            var configTypes = result.Configs.Select(entry => entry.ConfigType).ToArray();

            Assert.AreEqual(types.Distinct().Count(), types.Length);
            Assert.AreEqual(configTypes.Distinct().Count(), configTypes.Length);
        }

        [Test]
        public void Scan_SkipsTestAssembly_WhenItReferencesNUnit()
        {
            Assert.IsTrue(_tests.GetTypes().Any(type => typeof(SaveLoad.SaveBlob).IsAssignableFrom(type) && !type.IsAbstract),
                "Тестовая сборка обязана содержать хотя бы один SaveBlob-тип, иначе фильтр нечего проверять.");

            var result = AutoTypeScanner.Scan(new[] { _tests });

            Assert.IsEmpty(result.AutoTypes);
            Assert.IsEmpty(result.Configs);
        }

        [Test]
        public void Scan_SkipsAssembly_WhenItDoesNotReferenceCore()
        {
            var result = AutoTypeScanner.Scan(new[] { typeof(object).Assembly });

            Assert.IsEmpty(result.AutoTypes);
            Assert.IsEmpty(result.Configs);
        }

        private static AutoTypeEntry FindAutoType(Type type)
        {
            var result = AutoTypeScanner.Scan(new[] { _core });

            return result.AutoTypes.Single(entry => entry.Type == type);
        }
    }
}
