using System.Linq;
using Framework.Foundation.Asset;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class AssetOwnershipTests
    {
        private AssetOwnership _ownership;
        private object _first;
        private object _second;

        [SetUp]
        public void SetUp()
        {
            _ownership = new AssetOwnership();
            _first = new object();
            _second = new object();
        }

        [Test]
        public void Release_ReportsUnowned_WhenOnlyOwnerReleasedKey()
        {
            _ownership.Acquire("key_a", _first, persistent: false);

            Assert.IsTrue(_ownership.Release("key_a", _first));
            Assert.IsFalse(_ownership.IsOwned("key_a"));
        }

        // Ради этого тикет и заведён: релиз одного владельца не освобождает ключ,
        // пока его держит второй.
        [Test]
        public void Release_KeepsKeyOwned_WhenAnotherOwnerHoldsIt()
        {
            _ownership.Acquire("key_a", _first, persistent: false);
            _ownership.Acquire("key_a", _second, persistent: false);

            Assert.IsFalse(_ownership.Release("key_a", _first));
            Assert.IsTrue(_ownership.IsOwned("key_a"));

            Assert.IsTrue(_ownership.Release("key_a", _second));
            Assert.IsFalse(_ownership.IsOwned("key_a"));
        }

        [Test]
        public void Release_ReportsUnowned_WhenSameOwnerAcquiredKeyTwice()
        {
            _ownership.Acquire("key_a", _first, persistent: false);
            _ownership.Acquire("key_a", _first, persistent: false);

            Assert.IsTrue(_ownership.Release("key_a", _first));
        }

        [Test]
        public void Release_KeepsKeyOwned_WhenCalledByForeignOwner()
        {
            _ownership.Acquire("key_a", _first, persistent: false);

            Assert.IsFalse(_ownership.Release("key_a", _second));
            Assert.IsTrue(_ownership.IsOwned("key_a"));
        }

        // Ключ мог остаться в кэше без владельцев: последний отпустил его при живых инстансах.
        // Такой ключ обязан считаться свободным, иначе его больше никто не освободит.
        [Test]
        public void Release_ReportsUnowned_WhenKeyHasNoOwners()
        {
            Assert.IsTrue(_ownership.Release("key_a", _first));
        }

        [Test]
        public void IsOwned_ReturnsFalse_WhenKeyNeverHadOwners()
        {
            Assert.IsFalse(_ownership.IsOwned("key_a"));
        }

        [Test]
        public void IsPersistent_ReturnsTrue_WhenAnyOwnerClaimedKey()
        {
            _ownership.Acquire("key_a", _first, persistent: true);
            _ownership.Acquire("key_a", _second, persistent: false);

            Assert.IsTrue(_ownership.IsPersistent("key_a"));
        }

        [Test]
        public void IsPersistent_ReturnsFalse_WhenClaimingOwnerLeft()
        {
            _ownership.Acquire("key_a", _first, persistent: true);
            _ownership.Acquire("key_a", _second, persistent: false);

            _ownership.Release("key_a", _first);

            Assert.IsFalse(_ownership.IsPersistent("key_a"));
            Assert.IsTrue(_ownership.IsOwned("key_a"));
        }

        [Test]
        public void IsPersistent_ReturnsTrue_WhenSameOwnerReacquiredWithoutFlag()
        {
            _ownership.Acquire("key_a", _first, persistent: true);
            _ownership.Acquire("key_a", _first, persistent: false);

            Assert.IsTrue(_ownership.IsPersistent("key_a"));
        }

        [Test]
        public void IsPersistent_KeepsClaim_WhenForeignOwnerReleasedKey()
        {
            _ownership.Acquire("key_a", _first, persistent: true);

            _ownership.Release("key_a", _second);

            Assert.IsTrue(_ownership.IsPersistent("key_a"));
        }

        [Test]
        public void Keys_DropsKey_WhenLastOwnerReleasedIt()
        {
            _ownership.Acquire("key_a", _first, persistent: false);
            _ownership.Acquire("key_b", _first, persistent: false);

            _ownership.Release("key_a", _first);

            CollectionAssert.AreEqual(new[] { "key_b" }, _ownership.Keys.ToArray());
        }

        [Test]
        public void PersistentKeys_ListsClaimedKeysOnly_WhenKeysMixed()
        {
            _ownership.Acquire("key_a", _first, persistent: true);
            _ownership.Acquire("key_b", _first, persistent: false);

            CollectionAssert.AreEqual(new[] { "key_a" }, _ownership.PersistentKeys.ToArray());
        }

        [Test]
        public void Clear_ForgetsAllOwnersAndClaims_WhenCalled()
        {
            _ownership.Acquire("key_a", _first, persistent: true);

            _ownership.Clear();

            CollectionAssert.IsEmpty(_ownership.Keys);
            CollectionAssert.IsEmpty(_ownership.PersistentKeys);
            Assert.IsFalse(_ownership.IsPersistent("key_a"));
        }
    }
}
