using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Framework.Foundation.Ads
{
    /// <summary>
    /// Прикладной фасад рекламы: фича спрашивает готовность, зовёт показ и получает исход,
    /// не зная о конкретной сети. Все методы безопасны при выключенной рекламе — они
    /// возвращают <see cref="AdResult.NotReady"/>, а не бросают.
    /// </summary>
    public interface IAdsController
    {
        /// Рантайм-переключатель формата. Включить формат, выключенный конфигом, нельзя.
        void SetFormatEnabled(AdFormat format, bool enabled);

        /// «Готово к показу прямо сейчас»: конфиг, рантайм-флаг, готовность сети, кулдаун
        /// и отсутствие активного показа. Пересчитывается по тику <c>IClock.ServerNow</c>.
        ReadOnlyReactiveProperty<bool> IsReady(AdFormat format);

        UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct = default);

        /// Обёртка над <see cref="ShowAsync"/> для вызова из синхронного кода.
        void Show(AdFormat format, Action onSuccess = null, Action<AdResult> onFail = null);

        /// Остаток кулдауна interstitial на момент вызова. Для UI-таймера.
        Observable<TimeSpan> InterstitialCooldown();

        /// Снапшот остатка кулдауна interstitial: позднейший из «после показа» и «после старта
        /// сессии». Для проверок и дебага, где подписка на тикающий стрим избыточна.
        TimeSpan InterstitialCooldownLeft { get; }

        ReadOnlyReactiveProperty<bool> IsAdPlaying { get; }

        int InterstitialWatched { get; }
        int RewardedWatched { get; }
    }
}
