using System;
using Framework.Foundation.UI.Mvvm;
using R3;
using UnityEngine;

namespace Framework.Foundation.UI.Views
{
    public abstract class MonoView : MonoBehaviour
    {
        private readonly ViewStateNotifier _stateNotifier = new();

        private IViewRouter _viewRouter;
        private string _viewKey;

        public ReadOnlyReactiveProperty<ViewState> State => _stateNotifier.State;
        public Observable<Unit> OnOpen => _stateNotifier.OnOpen;
        public Observable<Unit> OnOpened => _stateNotifier.OnOpened;
        public Observable<Unit> OnClose => _stateNotifier.OnClose;
        public Observable<Unit> OnClosed => _stateNotifier.OnClosed;

        // Шорткаты уже привязаны к жизни view (AddTo(this)) — внешний AddTo не нужен.
        public IDisposable SubscribeOnOpen(Action action) => SubscribeUntilDestroy(OnOpen, action);
        public IDisposable SubscribeOnOpened(Action action) => SubscribeUntilDestroy(OnOpened, action);
        public IDisposable SubscribeOnClose(Action action) => SubscribeUntilDestroy(OnClose, action);
        public IDisposable SubscribeOnClosed(Action action) => SubscribeUntilDestroy(OnClosed, action);

        internal void Setup(IViewRouter viewRouter, string viewKey)
        {
            _viewRouter = viewRouter;
            _viewKey = viewKey;
        }

        internal void NotifyState(ViewState state) => _stateNotifier.SetState(state);

        internal void NotifyEvent(ViewEvent viewEvent) => _stateNotifier.RaiseEvent(viewEvent);

        public void Open() => _viewRouter.Open(_viewKey);
        public void Close() => _viewRouter.Close(_viewKey);

        protected virtual void OnDestroy() => _stateNotifier.Dispose();

        private IDisposable SubscribeUntilDestroy(Observable<Unit> source, Action action) =>
            source.Subscribe(action, static (_, subscribed) => subscribed()).AddTo(this);
    }

    public abstract class MonoView<TViewModel> : MonoView where TViewModel : ViewModel
    {
        protected TViewModel ViewModel { get; private set; }

        // Bind вызывается один раз за жизнь view: подписки в OnBind живут через .AddTo(this)
        // до Destroy, повторный Bind задублирует их.
        public void Bind(TViewModel viewModel)
        {
            ViewModel = viewModel;
            OnBind(viewModel);
        }

        protected abstract void OnBind(TViewModel viewModel);
    }
}
