using Framework.Foundation.UI.Views;

namespace Framework.Foundation.UI.Views
{
    /// <summary>
    /// Post-instantiate step applied by <see cref="ViewFactory"/> to every created view.
    /// Feature modules register their own steps so the factory stays feature-agnostic.
    /// </summary>
    public interface IViewSetupStep
    {
        void Setup(MonoView view);
    }
}
