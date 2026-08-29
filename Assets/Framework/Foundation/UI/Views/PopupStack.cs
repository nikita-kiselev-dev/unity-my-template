using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Views.ViewAnimation;
using System.Threading;

namespace Framework.Foundation.UI.Views
{
    internal sealed class PopupStack
    {
        private readonly List<ViewWrapper> _stack = new();
        private readonly IViewAnimator _background;

        public PopupStack(IViewAnimator background)
        {
            _background = background;
        }

        public ViewWrapper Top => _stack.Count > 0 ? _stack[^1] : null;

        // Одиночное открытие: используется тестами стека, прод идёт через OpenBatch из pump-а.
        public UniTask Open(ViewWrapper popup, CancellationToken ct) =>
            OpenBatch(new[] { popup }, ct);

        public async UniTask OpenBatch(IReadOnlyList<ViewWrapper> popups, CancellationToken ct)
        {
            var toOpen = new List<ViewWrapper>(popups.Count);
            for (var i = 0; i < popups.Count; i++)
            {
                var popup = popups[i];
                // Contains: фильтр по State идёт до мутации состояний, поэтому дубликат
                // в одной пачке (двойной клик за coalesce-окно) прошёл бы дважды.
                if (popup.State == ViewState.Closed && !toOpen.Contains(popup))
                {
                    toOpen.Add(popup);
                }
            }

            if (toOpen.Count == 0)
            {
                return;
            }

            if (_stack.Count == 0)
            {
                RunBackgroundAnimation(_background.Show(ct)).Forget();
            }
            else
            {
                await _stack[^1].Suspend(ct);
            }

            for (var i = 0; i < toOpen.Count - 1; i++)
            {
                var popup = toOpen[i];
                popup.SuspendImmediate();
                _stack.Add(popup);
            }

            var top = toOpen[^1];
            _stack.Add(top);
            await top.Open(ct);
        }

        public async UniTask Close(ViewWrapper popup, CancellationToken ct)
        {
            if (popup.State == ViewState.Closed)
            {
                return;
            }

            if (Top != popup)
            {
                _stack.Remove(popup);
                popup.CloseImmediate();
                return;
            }

            _stack.RemoveAt(_stack.Count - 1);

            if (_stack.Count == 0)
            {
                RunBackgroundAnimation(_background.Hide(ct)).Forget();
                await popup.Close(ct);
                return;
            }

            await popup.Close(ct);
            await _stack[^1].Open(ct);
        }

        public async UniTask CloseAll(CancellationToken ct)
        {
            var top = Top;

            for (var i = 0; i < _stack.Count - 1; i++)
            {
                _stack[i].CloseImmediate();
            }

            _stack.Clear();
            RunBackgroundAnimation(_background.Hide(ct)).Forget();

            if (top != null)
            {
                await top.Close(ct);
            }
        }

        public void Clear()
        {
            _stack.Clear();
        }

        private static async UniTaskVoid RunBackgroundAnimation(UniTask task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
