using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.Initialization;
using Framework.Foundation.UI.Canvas;
using Framework.Foundation.UI.Views;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;

namespace Framework.Foundation.UI.Views
{
    [AutoRegistration]
    public class ViewFactory : IViewFactory
    {
        [Inject] private readonly ICanvasProvider _canvasProvider;
        [Inject] private readonly IReadOnlyList<IViewSetupStep> _setupSteps;

        // Фабрика ассетами не владеет: инстанс создаётся через scope вызывающего, он же его
        // и освободит. Своей ассет-зависимости у ViewFactory поэтому нет.
        public async UniTask<T> CreateView<T>(string viewKey, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default)
        {
            var parentTransform = GetParentTransform(viewKind);
            var operationHandler = await owner.InstantiateAsync<T>(viewKey, parentTransform, cancellationToken: cancellationToken);
            return SetupView(operationHandler, viewKind);
        }

        public async UniTask<T> CreateView<T>(AssetReference reference, ViewKind viewKind, IAssetScope owner, CancellationToken cancellationToken = default)
        {
            var parentTransform = GetParentTransform(viewKind);
            var operationHandler = await owner.InstantiateAsync<T>(reference, parentTransform, cancellationToken: cancellationToken);
            return SetupView(operationHandler, viewKind);
        }

        private T SetupView<T>(T operationHandler, ViewKind viewKind)
        {
            if (operationHandler is not MonoView view)
            {
                return operationHandler;
            }

            var startViewStatus = GetStartViewStatus(viewKind);
            view.gameObject.SetActive(startViewStatus);

            foreach (var step in _setupSteps)
            {
                step.Setup(view);
            }

            return operationHandler;
        }

        private Transform GetParentTransform(ViewKind viewKind)
        {
            return viewKind == ViewKind.Popup
                ? _canvasProvider.PopupCanvas.ViewParentTransform
                : _canvasProvider.WindowCanvas.ViewParentTransform;
        }

        private bool GetStartViewStatus(ViewKind viewKind)
        {
            return viewKind == ViewKind.Window;
        }
    }
}
