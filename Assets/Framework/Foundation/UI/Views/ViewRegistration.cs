using Framework.Foundation.UI.Views.ViewAnimation;

namespace Framework.Foundation.UI.Views
{
    public readonly struct ViewRegistration
    {
        public bool EnableOnStart { get; }
        public IViewAnimator CustomAnimator { get; }

        public ViewRegistration(
            bool enableOnStart = false,
            IViewAnimator customAnimator = null)
        {
            EnableOnStart = enableOnStart;
            CustomAnimator = customAnimator;
        }
    }
}
