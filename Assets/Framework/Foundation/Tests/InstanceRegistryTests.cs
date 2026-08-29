using System.Linq;
using Framework.Foundation.Asset;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class InstanceRegistryTests
    {
        private static readonly object DefaultOwner = new();

        private InstanceRegistry<FakeInstance> _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new InstanceRegistry<FakeInstance>(instance => instance.IsAlive);
        }

        [Test]
        public void TryUntrack_ReturnsOwningKey_WhenInstanceTracked()
        {
            var instance = Track("key_a");

            Assert.IsTrue(_registry.TryUntrack(instance).TryGet(out var key));
            Assert.AreEqual("key_a", key);
        }

        [Test]
        public void TryUntrack_ReturnsFailure_WhenInstanceUnknown()
        {
            Assert.IsFalse(_registry.TryUntrack(new FakeInstance()).HasValue);
        }

        [Test]
        public void TryUntrack_ReturnsFailure_WhenInstanceNull()
        {
            Assert.IsFalse(_registry.TryUntrack(null).HasValue);
        }

        // Инстанс, уничтоженный мимо провайдера, обязан уходить
        // из учёта, иначе записи копятся до конца процесса.
        [Test]
        public void TryUntrack_ReturnsKeyAndForgetsInstance_WhenInstanceDead()
        {
            var instance = Track("key_a");
            instance.IsAlive = false;

            Assert.IsTrue(_registry.TryUntrack(instance).TryGet(out var key));
            Assert.AreEqual("key_a", key);
            Assert.IsFalse(_registry.TryUntrack(instance).HasValue);
            CollectionAssert.IsEmpty(_registry.Keys);
        }

        [Test]
        public void TryUntrack_KeepsKey_WhenOtherInstancesRemain()
        {
            var first = Track("key_a");
            Track("key_a");

            _registry.TryUntrack(first);

            CollectionAssert.AreEqual(new[] { "key_a" }, _registry.Keys);
        }

        [Test]
        public void TryUntrack_UsesReferenceIdentity_WhenInstancesCompareEqual()
        {
            var first = Track("key_a");
            var second = Track("key_b");

            Assert.AreEqual("key_a", _registry.TryUntrack(first).GetValueOrDefault());
            Assert.AreEqual("key_b", _registry.TryUntrack(second).GetValueOrDefault());
        }

        [Test]
        public void Track_MovesInstanceToNewKey_WhenTrackedTwice()
        {
            var instance = Track("key_a");

            _registry.Track(instance, "key_b", DefaultOwner);

            Assert.AreEqual("key_b", _registry.TryUntrack(instance).GetValueOrDefault());
            CollectionAssert.IsEmpty(_registry.Keys);
        }

        [Test]
        public void HasAlive_ReturnsTrueOnlyForKeysWithLiveInstances_WhenSomeDied()
        {
            Track("key_a");
            Track("key_b").IsAlive = false;

            Assert.IsTrue(_registry.HasAlive("key_a"));
            Assert.IsFalse(_registry.HasAlive("key_b"));
            Assert.IsFalse(_registry.HasAlive("key_c"));
            CollectionAssert.AreEqual(new[] { "key_a" }, _registry.Keys);
        }

        [Test]
        public void HasAlive_ForgetsDeadInstances_WhenCalled()
        {
            var alive = Track("key_a");
            var dead = Track("key_a");
            dead.IsAlive = false;

            _registry.HasAlive("key_a");

            Assert.IsFalse(_registry.TryUntrack(dead).HasValue);
            Assert.IsTrue(_registry.TryUntrack(alive).HasValue);
        }

        [Test]
        public void CountAlive_CountsOnlyLiveInstances_WhenSomeDied()
        {
            Track("key_a");
            Track("key_a").IsAlive = false;

            Assert.AreEqual(1, _registry.CountAlive("key_a"));
            Assert.AreEqual(0, _registry.CountAlive("key_b"));
        }

        // Снапшот для дебаг-оверлея обязан быть чистым чтением: мёртвых считает, но не удаляет.
        [Test]
        public void CountAlive_KeepsDeadInstances_WhenAllDied()
        {
            var instance = Track("key_a");
            instance.IsAlive = false;

            Assert.AreEqual(0, _registry.CountAlive("key_a"));
            CollectionAssert.AreEqual(new[] { "key_a" }, _registry.Keys);
            Assert.IsTrue(_registry.TryUntrack(instance).HasValue);
        }

        [Test]
        public void TryTakeAll_ReturnsEveryTrackedInstance_WhenKeyHasLiveAndDead()
        {
            var alive = Track("key_a");
            var dead = Track("key_a");
            dead.IsAlive = false;

            Assert.IsTrue(_registry.TryTakeAll("key_a").TryGet(out var taken));
            CollectionAssert.AreEquivalent(new[] { alive, dead }, taken.ToArray());
        }

        [Test]
        public void TryTakeAll_ForgetsKeyAndInstances_WhenTaken()
        {
            var instance = Track("key_a");

            _registry.TryTakeAll("key_a");

            Assert.IsFalse(_registry.TryTakeAll("key_a").HasValue);
            Assert.IsFalse(_registry.TryUntrack(instance).HasValue);
            CollectionAssert.IsEmpty(_registry.Keys);
        }

        [Test]
        public void TryTakeAll_ReturnsFailure_WhenKeyUnknown()
        {
            Assert.IsFalse(_registry.TryTakeAll("key_a").HasValue);
        }

        // Dispose одного владельца обязан уничтожить только его инстансы,
        // иначе чужой GameObject умирает вместе с чужим релизом.
        [Test]
        public void TryTakeAll_ReturnsInstancesOfCallingOwnerOnly_WhenKeyShared()
        {
            var owner = new object();
            var foreignOwner = new object();
            var own = Track("key_a", owner);
            var foreign = Track("key_a", foreignOwner);

            Assert.IsTrue(_registry.TryTakeAll("key_a", owner).TryGet(out var taken));
            CollectionAssert.AreEqual(new[] { own }, taken.ToArray());
            Assert.AreEqual("key_a", _registry.TryUntrack(foreign).GetValueOrDefault());
        }

        [Test]
        public void TryTakeAll_KeepsForeignInstancesTracked_WhenOwnerTookItsOwn()
        {
            var owner = new object();
            Track("key_a", owner);
            Track("key_a", new object());

            _registry.TryTakeAll("key_a", owner);

            CollectionAssert.AreEqual(new[] { "key_a" }, _registry.Keys.ToArray());
        }

        [Test]
        public void TryTakeAll_ForgetsKeyAndInstance_WhenOwnerTookLastOne()
        {
            var owner = new object();
            var instance = Track("key_a", owner);

            _registry.TryTakeAll("key_a", owner);

            CollectionAssert.IsEmpty(_registry.Keys);
            Assert.IsFalse(_registry.TryUntrack(instance).HasValue);
        }

        [Test]
        public void TryTakeAll_ReturnsFailure_WhenOwnerHasNothingForKey()
        {
            Track("key_a", new object());

            Assert.IsFalse(_registry.TryTakeAll("key_a", new object()).HasValue);
        }

        [Test]
        public void Keys_ListsTrackedKeysOnly_WhenInstancesTracked()
        {
            Track("key_a");
            Track("key_b");

            CollectionAssert.AreEquivalent(new[] { "key_a", "key_b" }, _registry.Keys.ToArray());
        }

        [Test]
        public void Clear_ForgetsEverything_WhenCalled()
        {
            var instance = Track("key_a");

            _registry.Clear();

            CollectionAssert.IsEmpty(_registry.Keys);
            Assert.IsFalse(_registry.TryUntrack(instance).HasValue);
        }

        private FakeInstance Track(string key) => Track(key, DefaultOwner);

        private FakeInstance Track(string key, object owner)
        {
            var instance = new FakeInstance();
            _registry.Track(instance, key, owner);
            return instance;
        }

        // Equals/GetHashCode намеренно сломаны: реестр обязан различать инстансы по ссылке,
        // как уничтоженные UnityEngine.Object, у которых равенство отвязано от живости.
        private sealed class FakeInstance
        {
            public bool IsAlive { get; set; } = true;

            public override bool Equals(object other) => other is FakeInstance;

            public override int GetHashCode() => 0;
        }
    }
}
