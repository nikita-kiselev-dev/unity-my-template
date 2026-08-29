using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Asset
{
    // Дедупликация незавершённых загрузок: первый вызывающий начинает загрузку, остальные ждут её
    // результат. Обещание — UniTaskCompletionSource<T>, а не Preserve()-задача: Preserve запоминает
    // результат, но пока задача не завершилась, разрешает ровно одного ожидающего
    // (UniTaskCompletionSourceCore.OnCompleted), а join-путь нужен именно для нескольких сразу.
    // У UniTaskCompletionSource<T> для этого есть secondaryContinuationList.
    internal sealed class InflightLoads<TValue>
    {
        private readonly Dictionary<string, Entry> _entries = new();

        public IReadOnlyCollection<string> Keys => _entries.Keys;

        public bool IsInflight(string key) => _entries.ContainsKey(key);

        public void Begin(string key, Type assetType)
        {
            _entries[key] = new Entry(assetType, new UniTaskCompletionSource<TValue>());
        }

        // Тип сверяется до ожидания: ждать чужую загрузку, чтобы потом отвергнуть её результат,
        // смысла нет — ошибка в вызывающем коде видна сразу.
        public UniTask<TValue> Join(string key, Type requestedType, CancellationToken cancellationToken)
        {
            var entry = _entries[key];

            if (entry.AssetType != requestedType)
            {
                throw new InvalidOperationException(
                    $"Asset '{key}' is being loaded as {entry.AssetType.Name}, requested as {requestedType.Name}.");
            }

            return entry.Source.Task.AttachExternalCancellation(cancellationToken);
        }

        public void Complete(string key, TValue value)
        {
            if (_entries.Remove(key, out var entry))
            {
                entry.Source.TrySetResult(value);
            }
        }

        public void Fail(string key, Exception exception)
        {
            if (_entries.Remove(key, out var entry))
            {
                entry.Source.TrySetException(exception);
            }
        }

        private readonly struct Entry
        {
            public Entry(Type assetType, UniTaskCompletionSource<TValue> source)
            {
                AssetType = assetType;
                Source = source;
            }

            public Type AssetType { get; }
            public UniTaskCompletionSource<TValue> Source { get; }
        }
    }
}
