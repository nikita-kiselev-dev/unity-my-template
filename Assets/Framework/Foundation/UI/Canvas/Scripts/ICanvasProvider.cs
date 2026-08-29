using Framework.Foundation.Utilities;

namespace Framework.Foundation.UI.Canvas
{
    public interface ICanvasProvider : IEntityStatus
    {
        public IWindowCanvas WindowCanvas { get; }
        public IPopupCanvas PopupCanvas { get; }
    }
}
