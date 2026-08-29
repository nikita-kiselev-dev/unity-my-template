using System;
using Framework.Foundation.UI.Views;

namespace Framework.Foundation.Initialization.Decorators.AutoView
{
    public readonly struct AutoViewBinding
    {
        public readonly string ViewKey;
        public readonly ViewKind ViewKind;
        public readonly Action<MonoView> Assign;

        public AutoViewBinding(string viewKey, ViewKind viewKind, Action<MonoView> assign)
        {
            ViewKey = viewKey;
            ViewKind = viewKind;
            Assign = assign;
        }
    }
}
