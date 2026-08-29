using Framework.Foundation.Signals;

namespace Framework.Foundation.Ads.Signals
{
    public class AdFinishedSignal : ISignal
    {
        public AdFormat Format { get; }
        public AdResult Result { get; }

        public AdFinishedSignal(AdFormat format, AdResult result)
        {
            Format = format;
            Result = result;
        }
    }
}
