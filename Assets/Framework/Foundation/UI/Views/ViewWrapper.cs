using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Views.ViewAnimation;

namespace Framework.Foundation.UI.Views
{
    internal sealed class ViewWrapper
    {
        public string ViewKey { get; }
        public ViewKind ViewKind { get; }
        public MonoView View { get; }
        public ViewState State => _state;

        private readonly IViewAnimator _animator;
        private readonly Action<ViewState> _stateChanged;
        private readonly Action<ViewEvent> _eventRaised;
        private ViewState _state = ViewState.Closed;

        public ViewWrapper(
            string viewKey,
            ViewKind viewKind,
            MonoView view,
            IViewAnimator animator,
            Action<ViewState> stateChanged = null,
            Action<ViewEvent> eventRaised = null)
        {
            ViewKey = viewKey;
            ViewKind = viewKind;
            View = view;
            _animator = animator;
            _stateChanged = stateChanged;
            _eventRaised = eventRaised;
        }

        public async UniTask Open(CancellationToken ct)
        {
            if (_state == ViewState.Open)
            {
                return;
            }

            RaiseEvent(ViewEvent.Open);
            SetState(ViewState.Open);
            await _animator.Show(ct);
            RaiseEvent(ViewEvent.Opened);
        }

        public void OpenImmediate()
        {
            if (_state == ViewState.Open)
            {
                return;
            }

            RaiseEvent(ViewEvent.Open);
            SetState(ViewState.Open);
            RaiseEvent(ViewEvent.Opened);
        }

        public async UniTask Suspend(CancellationToken ct)
        {
            SetState(ViewState.Suspended);
            await _animator.Hide(ct);
        }

        public void SuspendImmediate() => SetState(ViewState.Suspended);

        public async UniTask Close(CancellationToken ct)
        {
            if (_state == ViewState.Closed)
            {
                return;
            }

            RaiseEvent(ViewEvent.Close);
            await _animator.Hide(ct);
            SetState(ViewState.Closed);
            RaiseEvent(ViewEvent.Closed);
        }

        public void CloseImmediate()
        {
            if (_state == ViewState.Closed)
            {
                return;
            }

            RaiseEvent(ViewEvent.Close);
            SetState(ViewState.Closed);
            RaiseEvent(ViewEvent.Closed);
        }

        private void SetState(ViewState state)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            _stateChanged?.Invoke(state);
        }

        private void RaiseEvent(ViewEvent viewEvent) => _eventRaised?.Invoke(viewEvent);
    }
}
