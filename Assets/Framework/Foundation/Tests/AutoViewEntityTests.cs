using System.Threading;
using Framework.Foundation.Initialization.Decorators.AutoView;
using Framework.Foundation.Tests.Fakes;
using Framework.Foundation.UI.Views;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class AutoViewEntityTests
    {
        private const string PopupKey = "popup";

        private FakeAssetProvider _assetProvider;
        private FakeViewFactory _viewFactory;
        private FakeViewRouter _viewRouter;

        [SetUp]
        public void SetUp()
        {
            _assetProvider = new FakeAssetProvider();
            _viewFactory = new FakeViewFactory();
            _viewRouter = new FakeViewRouter();
        }

        [Test]
        public void Load_LoadsAssetPerBinding()
        {
            var entity = CreateEntity(PopupKey);

            entity.LoadPhase(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(new[] { PopupKey }, _assetProvider.LoadedKeys.ToArray());
        }

        [Test]
        public void Init_CreatesAndRegistersView()
        {
            var entity = CreateEntity(PopupKey);

            RunPhases(entity);

            Assert.AreEqual(new[] { PopupKey }, _viewFactory.CreatedKeys.ToArray());
            Assert.AreEqual(new[] { PopupKey }, _viewRouter.RegisteredKeys.ToArray());
        }

        // Инстанс view обязан принадлежать тому же владельцу, что и ключ, —
        // иначе Dispose сущности освободит ключ, а окно останется висеть до выгрузки сцены.
        [Test]
        public void Init_CreatesViewThroughOwnScope_WhenViewCreated()
        {
            var entity = CreateEntity(PopupKey);

            RunPhases(entity);

            Assert.AreSame(_assetProvider.LoadedByOwner[0].Owner, _viewFactory.CreatedOwners[0]);
        }

        [Test]
        public void Dispose_ReleasesAssetsThroughOwnScope_WhenAssetsWereLoaded()
        {
            var entity = CreateEntity(PopupKey);

            RunPhases(entity);
            entity.Dispose();

            Assert.AreEqual(new[] { PopupKey }, _assetProvider.ReleasedCompletely.ToArray());
            Assert.IsEmpty(_assetProvider.ReleasedAssets);
        }

        // Гейт (LifecycleGate) пропускает все фазы выключенной сущности вместе с её обёртками:
        // scope в фазе Load не создан, релизить нечего.
        [Test]
        public void Dispose_ReleasesNothing_WhenPhasesWereSkipped()
        {
            var entity = CreateEntity(PopupKey);

            entity.Dispose();

            Assert.IsEmpty(_assetProvider.ReleasedCompletely);
            Assert.IsEmpty(_assetProvider.ReleasedAssets);
        }

        private AutoViewEntity CreateEntity(params string[] viewKeys)
        {
            var bindings = new AutoViewBinding[viewKeys.Length];
            for (var i = 0; i < viewKeys.Length; i++)
            {
                bindings[i] = new AutoViewBinding(viewKeys[i], ViewKind.Popup, _ => { });
            }

            return new AutoViewEntity(bindings, _viewFactory, _viewRouter, _assetProvider);
        }

        private static void RunPhases(AutoViewEntity entity)
        {
            entity.LoadPhase(CancellationToken.None).GetAwaiter().GetResult();
            entity.InitPhase(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
