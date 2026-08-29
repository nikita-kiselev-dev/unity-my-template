using Framework.Foundation.Utilities;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeExternalLinkOpener : IExternalLinkOpener
    {
        public int PrivacyPolicyOpenCount { get; private set; }

        public void OpenPrivacyPolicy() => PrivacyPolicyOpenCount++;
    }
}
