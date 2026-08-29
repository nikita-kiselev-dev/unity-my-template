using System;

namespace Framework.Foundation.Signals
{
    public interface ISignalBus
    {
        IDisposable Subscribe<T>(Action<T> handler) where T : ISignal;
        IDisposable Subscribe<T>(Action handler) where T : ISignal;
        void Trigger<T>(T signal) where T : ISignal;
        void Trigger<T>() where T : ISignal, new();
    }
}
