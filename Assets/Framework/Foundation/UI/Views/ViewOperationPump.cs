using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Logger;

namespace Framework.Foundation.UI.Views
{
    internal sealed class ViewOperationPump
    {
        private readonly Queue<ViewOperation> _operations = new();
        private readonly Func<CancellationToken, UniTask> _waitFrame;
        private readonly IViewOperationExecutor _executor;
        private readonly ILogChannel _logger;
        private readonly CancellationToken _ct;

        private bool _started;
        private bool _pumping;

        // waitFrame остаётся делегатом: это единственная зависимость, которую тест подменяет
        // отдельно от исполнителя — чтобы удерживать окно коалесинга открытым.
        public ViewOperationPump(
            Func<CancellationToken, UniTask> waitFrame,
            IViewOperationExecutor executor,
            CancellationToken ct,
            ILogChannel logger)
        {
            _waitFrame = waitFrame;
            _executor = executor;
            _ct = ct;
            _logger = logger;
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            PumpAsync().Forget();
        }

        public void Enqueue(ViewOperation operation)
        {
            _operations.Enqueue(operation);
            if (_started)
            {
                PumpAsync().Forget();
            }
        }

        public void Clear()
        {
            _operations.Clear();
            _started = false;
            _pumping = false;
        }

        private async UniTaskVoid PumpAsync()
        {
            if (_pumping)
            {
                return;
            }

            _pumping = true;
            try
            {
                // Кадр ожидания собирает операции одного кадра в очередь до первого
                // Execute — иначе пачка Open из одного клика разъехалась бы на отдельные Show.
                await _waitFrame(_ct);

                while (_operations.Count > 0)
                {
                    try
                    {
                        await ExecuteNextAsync();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"ViewRouter operation failed: {e}");
                    }
                }
            }
            finally
            {
                _pumping = false;
            }
        }

        private async UniTask ExecuteNextAsync()
        {
            var operation = _operations.Dequeue();
            switch (operation.Kind)
            {
                case ViewOperationKind.Open when operation.Wrapper.ViewKind == ViewKind.Popup:
                    await _executor.OpenPopupBatch(DequeuePopupOpenBatch(operation.Wrapper), _ct);
                    break;
                case ViewOperationKind.Open:
                    await _executor.OpenWindow(operation.Wrapper, _ct);
                    break;
                case ViewOperationKind.Close:
                    await _executor.Close(operation.Wrapper, _ct);
                    break;
                case ViewOperationKind.CloseAll:
                    await _executor.CloseAll(_ct);
                    break;
            }
        }

        private List<ViewWrapper> DequeuePopupOpenBatch(ViewWrapper first)
        {
            var batch = new List<ViewWrapper> { first };
            while (_operations.Count > 0)
            {
                var next = _operations.Peek();
                if (next.Kind != ViewOperationKind.Open || next.Wrapper.ViewKind != ViewKind.Popup)
                {
                    break;
                }

                batch.Add(_operations.Dequeue().Wrapper);
            }

            return batch;
        }
    }
}
