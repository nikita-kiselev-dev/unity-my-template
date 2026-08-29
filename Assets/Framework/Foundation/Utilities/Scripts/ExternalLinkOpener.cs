using Framework.Foundation.Initialization;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.Utilities
{
    [AutoRegistration(Lifetime.Singleton)]
    public class ExternalLinkOpener : IExternalLinkOpener
    {
        public void OpenPrivacyPolicy()
        {
            Application.OpenURL(ExternalLinks.PrivacyPolicyWebSite);
        }
    }
}
