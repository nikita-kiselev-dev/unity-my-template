using System;
using R3;

namespace Framework.Foundation.UI.Views
{
    internal sealed class ViewStateNotifier : IDisposable
    {
        private readonly ReactiveProperty<ViewState> _state = new(ViewState.Closed);
        private readonly Subject<ViewEvent> _events = new();

        public ReadOnlyReactiveProperty<ViewState> State => _state;
        public Observable<Unit> OnOpen => Filtered(ViewEvent.Open);
        public Observable<Unit> OnOpened => Filtered(ViewEvent.Opened);
        public Observable<Unit> OnClose => Filtered(ViewEvent.Close);
        public Observable<Unit> OnClosed => Filtered(ViewEvent.Closed);

        public void SetState(ViewState state) => _state.Value = state;

        public void RaiseEvent(ViewEvent viewEvent) => _events.OnNext(viewEvent);

        public void Dispose()
        {
            _state.Dispose();
            _events.Dispose();
        }

        private Observable<Unit> Filtered(ViewEvent viewEvent) =>
            _events.Where(viewEvent, static (raised, expected) => raised == expected).AsUnitObservable();
    }
}
