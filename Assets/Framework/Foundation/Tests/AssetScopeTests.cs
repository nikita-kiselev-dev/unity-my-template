using System;
using Framework.Foundation.Asset;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Framework.Foundation.Tests
{
    public class AssetScopeTests
    {
        private FakeAssetProvider _provider;
        private IAssetScope _scope;

        [SetUp]
        public void SetUp()
        {
            _provider = new FakeAssetProvider();
            _scope = new AssetScope(_provider);
        }

        [Test]
        public void LoadAssetAsync_DelegatesToProvider_WhenCalledWithKey()
        {
            Load("key_a");

            Assert.AreEqual(new[] { "key_a" }, _provider.LoadedKeys);
        }

        [Test]
        public void Dispose_ReleasesCompletelyEveryLoadedKey_WhenMultipleKeysLoaded()
        {
            Load("key_a");
            Load("key_b");

            _scope.Dispose();

            CollectionAssert.AreEquivalent(new[] { "key_a", "key_b" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void Dispose_ReleasesKeyOnce_WhenSameKeyLoadedTwice()
        {
            Load("key_a");
            Load("key_a");

            _scope.Dispose();

            Assert.AreEqual(new[] { "key_a" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void Dispose_ReleasesInstantiatedKey_WhenInstantiateAsyncUsed()
        {
            _scope.InstantiateAsync<Object>("key_a").GetAwaiter().GetResult();

            _scope.Dispose();

            Assert.AreEqual(new[] { "key_a" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void Dispose_ReleasesRuntimeKey_WhenLoadedByAssetReference()
        {
            var reference = new AssetReference("guid_a");

            _scope.LoadAssetAsync<Object>(reference).GetAwaiter().GetResult();
            _scope.Dispose();

            Assert.AreEqual(new[] { "guid_a" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void Dispose_SkipsKey_WhenReleasedCompletelyManually()
        {
            Load("key_a");
            Load("key_b");

            _scope.ReleaseCompletely("key_a");
            _scope.Dispose();

            Assert.AreEqual(new[] { "key_a", "key_b" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void Dispose_ReleasesNothing_WhenCalledTwice()
        {
            Load("key_a");

            _scope.Dispose();
            _scope.Dispose();

            Assert.AreEqual(new[] { "key_a" }, _provider.ReleasedCompletely);
        }

        [Test]
        public void LoadAssetAsync_Throws_WhenScopeDisposed()
        {
            _scope.Dispose();

            Assert.Throws<ObjectDisposedException>(() => Load("key_a"));
        }

        [Test]
        public void LoadAssetAsync_PassesScopeAsOwner_WhenCalledWithKey()
        {
            Load("key_a");

            Assert.AreSame(_scope, _provider.LoadedByOwner[0].Owner);
        }

        [Test]
        public void InstantiateAsync_PassesScopeAsOwner_WhenCalledWithKey()
        {
            _scope.InstantiateAsync<Object>("key_a").GetAwaiter().GetResult();

            Assert.AreSame(_scope, _provider.InstantiatedByOwner[0].Owner);
        }

        // Два владельца одного ключа. Dispose первого снимает только его
        // владение — ключ второго провайдер не трогает.
        [Test]
        public void Dispose_ReleasesKeyUnderOwnOwnershipOnly_WhenSecondScopeHoldsSameKey()
        {
            var other = _provider.CreateScope();
            other.LoadAssetAsync<Object>("shared").GetAwaiter().GetResult();
            Load("shared");

            _scope.Dispose();

            Assert.AreEqual(1, _provider.ReleasedCompletelyByOwner.Count);
            Assert.AreEqual("shared", _provider.ReleasedCompletelyByOwner[0].Key);
            Assert.AreSame(_scope, _provider.ReleasedCompletelyByOwner[0].Owner);
        }

        [Test]
        public void ReleaseCompletely_PassesScopeAsOwner_WhenCalledManually()
        {
            Load("key_a");

            _scope.ReleaseCompletely("key_a");

            Assert.AreSame(_scope, _provider.ReleasedCompletelyByOwner[0].Owner);
        }

        [Test]
        public void CreateScope_TracksKeysIndependently_WhenNestedScopeUsed()
        {
            var nested = _scope.CreateScope();
            nested.LoadAssetAsync<Object>("nested_key").GetAwaiter().GetResult();
            Load("outer_key");

            _scope.Dispose();

            Assert.AreEqual(new[] { "outer_key" }, _provider.ReleasedCompletely);
        }

        private void Load(string key) => _scope.LoadAssetAsync<Object>(key).GetAwaiter().GetResult();
    }
}
