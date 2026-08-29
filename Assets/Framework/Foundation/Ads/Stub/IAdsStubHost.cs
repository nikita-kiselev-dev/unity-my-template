#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Ads.Stub
{
    /// <summary>
    /// Тот, кто умеет показать попап-заглушку. Реализация — Scoped-сущность сцены
    /// (<c>AdsStubPopupHost</c>), поэтому Singleton-провайдер держит её как сменную ссылку:
    /// на сценах без хоста (Bootstrap) показывать нечем и показ отдаёт <see cref="AdResult.NotReady"/>.
    /// </summary>
    public interface IAdsStubHost
    {
        UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct);
    }
}
#endif
