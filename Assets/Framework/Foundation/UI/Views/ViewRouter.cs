using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using Framework.Foundation.Signals;
using Framework.Foundation.UI.Canvas;
using Framework.Foundation.UI.LoadingCurtain;
using Framework.Foundation.UI.Views.ViewAnimation;
using R3;
using VContainer;

namespace Framework.Foundation.UI.Views
{
    [AutoRegistration]
    [LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.ViewRouter)]
    [LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.ViewRouter)]
    public class ViewRouter : LifecycleEntity, IViewRouter, IViewOperationExecutor
    {
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly ICanvasProvider _canvasProvider;
        [Inject] private readonly ILogChannelFactory _logChannelFactory;

        private readonly Dictionary<string, ViewWrapper> _viewWrappers = new();
        private readonly BackgroundAnimator _backgroundAnimator = new();
        private readonly WindowQueue _windows = new();
        private readonly PopupStack _popups;
        private readonly CancellationTokenSource _cts = new();

        private ILogChannel _logger;
        private ViewOperationPump _pump;
        private DisposableBag _subscriptions;

        public ViewRouter()
        {
            _popups = new PopupStack(_backgroundAnimator);

            SetEnabled(true);
            SetActive();
        }

        // Единственный post-inject типа (второй [Inject]-метод VContainer вызвал бы в
        // неопределённом порядке). Всё, от чего зависит Register, собирается здесь, а не в
        // фазе Init: Register вызывает wrapper AutoViewEntity, и то, что он успевает раньше
        // Init хоста, — свойство барьера фаз, а не контракт ViewRouter.
        [Inject]
        private void InitRouter()
        {
            _logger = _logChannelFactory.Get(nameof(ViewRouter));
            _backgroundAnimator.SetCanvasProvider(_canvasProvider);
            _signalBus.Subscribe<PopupBackgroundClickedSignal>(CloseLast).AddTo(ref _subscriptions);
            _signalBus.Subscribe<LoadingCurtainHiddenSignal>(OnCurtainHidden).AddTo(ref _subscriptions);
            _pump = new ViewOperationPump(ct => UniTask.NextFrame(ct), this, _cts.Token, _logger);
        }

        public void Open(string viewKey)
        {
            if (TryGetWrapper(viewKey, out var wrapper))
            {
                _pump.Enqueue(ViewOperation.Open(wrapper));
            }
        }

        public void Close(string viewKey)
        {
            if (TryGetWrapper(viewKey, out var wrapper))
            {
                _pump.Enqueue(ViewOperation.Close(wrapper));
            }
        }

        public void CloseAll()
        {
            _pump.Enqueue(ViewOperation.CloseAll());
        }

        public void CloseLast()
        {
            var top = _popups.Top;
            if (top != null)
            {
                Close(top.ViewKey);
            }
        }

        public void Register(string viewKey, MonoView view, ViewKind viewKind, ViewRegistration options = default)
        {
            var animator = CreateAnimator(view, viewKind, options);
            var wrapper = new ViewWrapper(viewKey, viewKind, view, animator, view.NotifyState, view.NotifyEvent);
            _viewWrappers.Add(viewKey, wrapper);
            var enableOnStart = viewKind != ViewKind.Popup && options.EnableOnStart;
            view.gameObject.SetActive(enableOnStart);
            view.Setup(this, viewKey);

            if (enableOnStart)
            {
                _windows.InitializeActive(wrapper);
            }
        }

        protected override UniTask PostInit()
        {
            _pump.Start();
            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _pump.Clear();
            _viewWrappers.Clear();
            _windows.Clear();
            _popups.Clear();
            _subscriptions.Dispose();
            base.Dispose();
        }

        private void OnCurtainHidden()
        {
            _pump.Start();
        }

        UniTask IViewOperationExecutor.OpenWindow(ViewWrapper window, CancellationToken ct) =>
            _windows.Open(window, ct);

        UniTask IViewOperationExecutor.OpenPopupBatch(IReadOnlyList<ViewWrapper> popups, CancellationToken ct) =>
            _popups.OpenBatch(popups, ct);

        UniTask IViewOperationExecutor.Close(ViewWrapper view, CancellationToken ct) =>
            view.ViewKind == ViewKind.Popup ? _popups.Close(view, ct) : _windows.Close(view, ct);

        async UniTask IViewOperationExecutor.CloseAll(CancellationToken ct)
        {
            await _popups.CloseAll(ct);
            await _windows.CloseAll(ct);
        }

        private bool TryGetWrapper(string viewKey, out ViewWrapper wrapper)
        {
            if (_viewWrappers.TryGetValue(viewKey, out wrapper))
            {
                return true;
            }

            _logger.LogError($"{viewKey} - view is not registered.");
            return false;
        }

        private static IViewAnimator CreateAnimator(MonoView view, ViewKind viewKind, ViewRegistration options)
        {
            if (options.CustomAnimator != null)
            {
                return options.CustomAnimator;
            }

            return viewKind == ViewKind.Popup
                ? new PopupAnimator(view.gameObject)
                : new WindowAnimator(view.transform);
        }
    }
}
