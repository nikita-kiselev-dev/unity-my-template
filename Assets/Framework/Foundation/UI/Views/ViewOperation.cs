namespace Framework.Foundation.UI.Views
{
    internal enum ViewOperationKind
    {
        Open,
        Close,
        CloseAll
    }

    internal readonly struct ViewOperation
    {
        public ViewOperationKind Kind { get; }
        public ViewWrapper Wrapper { get; }

        private ViewOperation(ViewOperationKind kind, ViewWrapper wrapper)
        {
            Kind = kind;
            Wrapper = wrapper;
        }

        public static ViewOperation Open(ViewWrapper wrapper) => new(ViewOperationKind.Open, wrapper);

        public static ViewOperation Close(ViewWrapper wrapper) => new(ViewOperationKind.Close, wrapper);

        public static ViewOperation CloseAll() => new(ViewOperationKind.CloseAll, null);
    }
}
