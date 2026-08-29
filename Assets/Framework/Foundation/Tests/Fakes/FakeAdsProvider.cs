using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Ads;

namespace Framework.Foundation.Tests.Fakes
{
    public sealed class FakeAdsProvider : IAdsProvider
    {
        private UniTaskCompletionSource<AdResult> _pending;

        public HashSet<AdFormat> ReadyFormats { get; } = new()
        {
            AdFormat.Banner,
            AdFormat.Interstitial,
            AdFormat.Rewarded
        };

        public List<AdFormat> ShowCalls { get; } = new();
        public int InitCount { get; private set; }
        public bool IsInited { get; private set; }
        public bool? BannerVisible { get; private set; }

        public AdResult NextResult { get; set; } = AdResult.Success;
        public Exception NextException { get; set; }
        public bool ManualCompletion { get; set; }

        public UniTask InitAsync(CancellationToken ct)
        {
            InitCount++;
            IsInited = true;
            return UniTask.CompletedTask;
        }

        public bool IsReady(AdFormat format) => ReadyFormats.Contains(format);

        public void SetBannerVisible(bool visible) => BannerVisible = visible;

        public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct)
        {
            ShowCalls.Add(format);

            if (NextException != null)
            {
                throw NextException;
            }

            if (!ManualCompletion)
            {
                return UniTask.FromResult(NextResult);
            }

            _pending = new UniTaskCompletionSource<AdResult>();
            return _pending.Task;
        }

        public void CompletePending(AdResult result) => _pending.TrySetResult(result);
    }
}
