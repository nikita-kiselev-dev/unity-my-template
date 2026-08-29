using Framework.Foundation.Signals;

namespace Framework.Foundation.Ads.Signals
{
    public class AdStartedSignal : ISignal
    {
        public AdFormat Format { get; }

        public AdStartedSignal(AdFormat format)
        {
            Format = format;
        }
    }
}
