using System.Collections.Generic;
using Framework.Foundation.UI.Views;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeViewRouter : IViewRouter
    {
        public List<string> RegisteredKeys { get; } = new();
        public List<string> OpenedKeys { get; } = new();
        public List<string> ClosedKeys { get; } = new();

        public void Open(string viewKey) => OpenedKeys.Add(viewKey);

        public void Close(string viewKey) => ClosedKeys.Add(viewKey);

        public void CloseAll()
        {
        }

        public void CloseLast()
        {
        }

        public void Register(string viewKey, MonoView view, ViewKind viewKind, ViewRegistration options = default)
        {
            RegisteredKeys.Add(viewKey);
        }
    }
}
