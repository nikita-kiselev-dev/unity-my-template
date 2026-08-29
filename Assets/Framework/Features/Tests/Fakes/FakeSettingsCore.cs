using Framework.Features.Settings;
using Framework.Foundation.Utilities;

namespace Framework.Features.Tests.Fakes
{
    public class FakeSettingsCore : ISettingsCore
    {
        public int OpenPopupCount { get; private set; }

        public EntityStatus Status { get; } = new(nameof(FakeSettingsCore));

        public bool IsEnabled => Status.IsEnabled;
        public bool IsInited => Status.IsInited;
        public bool IsActive => Status.IsActive;

        public void OpenPopup() => OpenPopupCount++;
    }
}
