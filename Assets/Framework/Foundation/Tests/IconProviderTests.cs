using System;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class IconProviderTests
    {
        private ReactiveSignalBus _signalBus;
        private FakeAssetProvider _assetProvider;
        private Asset.Icons.IconProvider _iconProvider;

        [SetUp]
        public void SetUp()
        {
            _signalBus = new ReactiveSignalBus();
            _assetProvider = new FakeAssetProvider();
            _iconProvider = new Asset.Icons.IconProvider(_signalBus, _assetProvider);
        }

        [TearDown]
        public void TearDown()
        {
            ((IDisposable)_iconProvider).Dispose();
            _signalBus.Dispose();
        }

        [Test]
        public void GetIcon_LoadsAsset_WithoutPersistentFlag()
        {
            LoadIcon("coin");

            CollectionAssert.AreEqual(new[] { "coin" }, _assetProvider.LoadedKeys);
            Assert.IsEmpty(_assetProvider.PersistentKeys);
        }

        [Test]
        public void CurtainShown_DropsCachedIcons()
        {
            LoadIcon("coin");

            _signalBus.Trigger<LoadingCurtainShownSignal>();
            ((IDisposable)_iconProvider).Dispose();

            // Шторка уже освободила ассеты: релизить в Dispose нечего, записей в кэше не осталось.
            Assert.IsEmpty(_assetProvider.ReleasedAssets);
        }

        [Test]
        public void Dispose_ReleasesCachedIcons_WhenCurtainDidNotShow()
        {
            LoadIcon("coin");

            ((IDisposable)_iconProvider).Dispose();

            CollectionAssert.AreEqual(new[] { "coin" }, _assetProvider.ReleasedAssets);
        }

        private void LoadIcon(string iconName)
        {
            _iconProvider.GetIcon(iconName).GetAwaiter().GetResult();
        }
    }
}
