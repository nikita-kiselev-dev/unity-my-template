using System;
using System.Collections.Generic;
using Framework.Foundation.Initialization;
using R3;
using VContainer;

namespace Framework.Foundation.Signals
{
    /// <summary>
    /// R3-based <see cref="ISignalBus"/>. Not thread-safe — call only from the Unity main thread.
    /// A stream is keyed by the signal type alone, so any subscriber to <c>T</c> receives every
    /// <c>T</c> triggered — the payload travels inside the signal instance.
    /// Triggers without active subscribers are silently dropped (no replay semantics).
    /// Handler exceptions are isolated by R3 and routed to <see cref="ObservableSystem"/>'s
    /// unhandled-exception handler (<c>Debug.LogException</c> in Unity).
    /// </summary>
    [AutoRegistration(Lifetime.Singleton)]
    public class ReactiveSignalBus : ISignalBus, IDisposable
    {
        private readonly Dictionary<Type, IStreamHandle> _streams = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : ISignal
        {
            return GetOrCreateStream<T>().Subscribe(handler);
        }

        public IDisposable Subscribe<T>(Action handler) where T : ISignal
        {
            return GetOrCreateStream<T>().Subscribe(_ => handler());
        }

        public void Trigger<T>(T signal) where T : ISignal
        {
            if (_streams.TryGetValue(typeof(T), out var handle))
            {
                ((StreamHandle<T>)handle).Subject.OnNext(signal);
            }
        }

        public void Trigger<T>() where T : ISignal, new()
        {
            if (_streams.TryGetValue(typeof(T), out var handle))
            {
                ((StreamHandle<T>)handle).Subject.OnNext(new T());
            }
        }

        public void Dispose()
        {
            foreach (var handle in _streams.Values)
            {
                handle.CompleteAndDispose();
            }

            _streams.Clear();
        }

        private Subject<T> GetOrCreateStream<T>() where T : ISignal
        {
            if (_streams.TryGetValue(typeof(T), out var existing))
            {
                return ((StreamHandle<T>)existing).Subject;
            }

            var handle = new StreamHandle<T>();
            _streams[typeof(T)] = handle;
            return handle.Subject;
        }

        private interface IStreamHandle
        {
            void CompleteAndDispose();
        }

        private sealed class StreamHandle<T> : IStreamHandle
        {
            public Subject<T> Subject { get; } = new();

            public void CompleteAndDispose()
            {
                Subject.OnCompleted();
                Subject.Dispose();
            }
        }
    }
}
