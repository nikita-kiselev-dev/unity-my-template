using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Scenes;
using Framework.Foundation.Signals;
using Framework.Foundation.UI.Views;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.UI.Canvas
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.CanvasProvider)]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.CanvasProvider)]
    public class CanvasProvider : LifecycleEntity, ICanvasProvider
    {
        [Inject] private readonly IAssetProvider _assetProvider;
        [Inject] private readonly ISignalBus _signalBus;
        
        public IWindowCanvas WindowCanvas { get; private set; }
        public IPopupCanvas PopupCanvas { get; private set; }
        
        protected override async UniTask Load()
        {
            await UniTask.WhenAll(
                _assetProvider.LoadAssetAsync<GameObject>(CanvasConstants.Canvases.Window, cancellationToken: CancellationToken),
                _assetProvider.LoadAssetAsync<GameObject>(CanvasConstants.Canvases.Popup, cancellationToken: CancellationToken));

            await CreateView();
            WindowCanvas.Init();
            PopupCanvas.Init(PopupBackgroundClickAction);
        }

        protected override UniTask Init()
        {
            SetEnabled(true);
            SetActive();
            return UniTask.CompletedTask;
        }

        protected override void Unload()
        {
            base.Unload();
            _assetProvider.ReleaseCompletely(CanvasConstants.Canvases.Window);
            _assetProvider.ReleaseCompletely(CanvasConstants.Canvases.Popup);
        }

        private async UniTask CreateView()
        {
            WindowCanvas = await _assetProvider.InstantiateAsync<IWindowCanvas>(CanvasConstants.Canvases.Window, setActive: true, cancellationToken: CancellationToken);
            PopupCanvas = await _assetProvider.InstantiateAsync<IPopupCanvas>(CanvasConstants.Canvases.Popup, setActive: true, cancellationToken: CancellationToken);
        }
        
        private void PopupBackgroundClickAction()
        {
            _signalBus.Trigger<PopupBackgroundClickedSignal>();
        }
    }
}