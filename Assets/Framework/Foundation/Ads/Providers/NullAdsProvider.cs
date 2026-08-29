using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Ads.Providers
{
    /// Дефолт для платформ без рекламной сети: контроллер работает, показов нет.
    public sealed class NullAdsProvider : IAdsProvider
    {
        public bool IsInited => true;

        public UniTask InitAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool IsReady(AdFormat format) => false;

        public void SetBannerVisible(bool visible)
        {
        }

        public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct) =>
            UniTask.FromResult(AdResult.NotReady);
    }
}
