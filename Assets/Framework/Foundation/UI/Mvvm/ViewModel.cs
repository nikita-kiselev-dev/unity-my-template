using System;
using R3;

namespace Framework.Foundation.UI.Mvvm
{
    public abstract class ViewModel : IDisposable
    {
        // Struct-bag: подписки и disposable-ресурсы VM цепляются через .AddTo(ref Subscriptions).
        protected DisposableBag Subscriptions;

        public virtual void Dispose() => Subscriptions.Dispose();
    }
}
