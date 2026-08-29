using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Ads
{
    /// <summary>
    /// Контракт рекламной сети. Реализация живёт в <c>Assets/Framework/Integrations/</c> и
    /// подключается через <c>AdsScopeRegistrator.RegisterPlatform</c>; активная сеть всегда одна.
    /// Кулдаун, счётчики и mute — забота <see cref="IAdsController"/>, провайдер про них не знает.
    /// </summary>
    public interface IAdsProvider
    {
        bool IsInited { get; }

        UniTask InitAsync(CancellationToken ct);

        bool IsReady(AdFormat format);

        void SetBannerVisible(bool visible);

        UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct);
    }
}
