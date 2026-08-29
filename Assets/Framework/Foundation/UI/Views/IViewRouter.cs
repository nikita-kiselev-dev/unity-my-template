namespace Framework.Foundation.UI.Views
{
    public interface IViewRouter
    {
        void Open(string viewKey);
        void Close(string viewKey);
        void CloseAll();
        void CloseLast();
        void Register(string viewKey, MonoView view, ViewKind viewKind, ViewRegistration options = default);
    }
}
