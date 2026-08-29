using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Framework.Foundation.UI.Views
{
    internal sealed class WindowQueue
    {
        private readonly List<ViewWrapper> _pending = new();
        private ViewWrapper _current;

        public void InitializeActive(ViewWrapper window)
        {
            if (_current == null)
            {
                _current = window;
                window.OpenImmediate();
                return;
            }

            window.SuspendImmediate();
            window.View.gameObject.SetActive(false);
            _pending.Add(window);
        }

        public async UniTask Open(ViewWrapper window, CancellationToken ct)
        {
            if (window.State != ViewState.Closed)
            {
                return;
            }

            if (_current != null)
            {
                window.SuspendImmediate();
                _pending.Add(window);
                return;
            }

            _current = window;
            await window.Open(ct);
        }

        public async UniTask Close(ViewWrapper window, CancellationToken ct)
        {
            if (_current != window)
            {
                _pending.Remove(window);
                window.CloseImmediate();
                return;
            }

            _current = null;
            await window.Close(ct);

            if (_pending.Count == 0)
            {
                return;
            }

            var next = _pending[0];
            _pending.RemoveAt(0);
            _current = next;
            await next.Open(ct);
        }

        public async UniTask CloseAll(CancellationToken ct)
        {
            foreach (var pending in _pending)
            {
                pending.CloseImmediate();
            }
            _pending.Clear();

            if (_current == null)
            {
                return;
            }

            var window = _current;
            _current = null;
            await window.Close(ct);
        }

        public void Clear()
        {
            _pending.Clear();
            _current = null;
        }
    }
}
