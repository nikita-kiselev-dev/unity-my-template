#if UNITY_WEBGL && PLUGIN_YG_2 && InterstitialAdv_yg && RewardedAdv_yg
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Ads;
using Framework.Foundation.Initialization;
using VContainer;
using YG;

namespace YandexGames
{
    /// <summary>
    /// Реклама Yandex Games. Кулдаун, счётчики, mute и сигналы держит <c>AdsController</c> —
    /// здесь только вызовы YG2 и превращение его событий в <see cref="AdResult"/>.
    /// Живёт в Assembly-CSharp: YG2 объявлен там, и asmdef-сборка на него сослаться не может,
    /// поэтому регистрация идёт через <c>[AutoRegistration]</c>, а не через
    /// <c>AdsScopeRegistrator</c> — он под теми же дефайнами только снимает заглушки.
    /// В редакторе класс компилируется, но не регистрируется: там работает попап-заглушка
    /// <c>EditorAdsProvider</c> с теми же исходами, а компиляция нужна, чтобы опечатка
    /// в адаптере всплывала сразу, а не только на WebGL-сборке.
    /// </summary>
#if !UNITY_EDITOR
    [AutoRegistration(Lifetime.Singleton)]
#endif
    public class YandexAdsProvider : IAdsProvider
    {
        // Потолок на весь показ, а не окно ожидания открытия: между вызовом showFullscreenAdv и
        // onOpen у Яндекса идёт загрузка креатива, и короткое окно превращало бы показавшуюся
        // рекламу в NotReady. Потолок нужен потому, что YG2 умеет проглотить показ молча
        // (SkipNextInterAdCall, ysdk == null в JS) — без него ad-сессия висела бы с мьютом вечно.
        // ponytail: одно число на оба формата; если rewarded-креативы окажутся длиннее, значение
        // переезжает в AdsConfig.
        private static readonly TimeSpan _showTimeout = TimeSpan.FromSeconds(90);

        public bool IsInited => YG2.isSDKEnabled;

        /// Готовность SDK держит YandexSdkEntity в фазе Load — ждать её здесь нечего.
        public UniTask InitAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool IsReady(AdFormat format)
        {
            if (!YG2.isSDKEnabled || YG2.nowAdsShow)
            {
                return false;
            }

            return format switch
            {
                AdFormat.Interstitial => YG2.isTimerAdvCompleted,
                AdFormat.Rewarded => true,
                _ => false
            };
        }

        public void SetBannerVisible(bool visible)
        {
        }

        public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct)
        {
            return format switch
            {
                AdFormat.Interstitial => ShowInterstitial(),
                AdFormat.Rewarded => ShowRewarded(),
                _ => UniTask.FromResult(AdResult.NotReady)
            };
        }

        private static async UniTask<AdResult> ShowInterstitial()
        {
            var finished = new UniTaskCompletionSource<AdResult>();

            // Закрытие = показ состоялся. Skipped у interstitial смысла не имеет: награды за него
            // нет, а различать «досмотрел» и «закрыл сам» нужно только ради награды. Флаг wasShown
            // не используется — Яндекс отдаёт в нём false и на фактически показанной рекламе,
            // и тогда показ не попадал бы ни в счётчик, ни в кулдаун.
            Action onClose = () => finished.TrySetResult(AdResult.Success);
            Action onError = () => finished.TrySetResult(AdResult.Failed);

            YG2.onCloseInterAdv += onClose;
            YG2.onErrorInterAdv += onError;

            try
            {
                YG2.InterstitialAdvShow();
                return await WaitOutcome(finished);
            }
            finally
            {
                YG2.onCloseInterAdv -= onClose;
                YG2.onErrorInterAdv -= onError;
            }
        }

        private static async UniTask<AdResult> ShowRewarded()
        {
            var rewarded = false;
            var finished = new UniTaskCompletionSource<AdResult>();

            Action<string> onReward = _ => rewarded = true;
            Action onClose = () => finished.TrySetResult(rewarded ? AdResult.Success : AdResult.Skipped);
            Action onError = () => finished.TrySetResult(AdResult.Failed);

            YG2.onRewardAdv += onReward;
            YG2.onCloseRewardedAdv += onClose;
            YG2.onErrorRewardedAdv += onError;

            try
            {
                YG2.RewardedAdvShow(string.Empty);
                return await WaitOutcome(finished);
            }
            finally
            {
                YG2.onRewardAdv -= onReward;
                YG2.onCloseRewardedAdv -= onClose;
                YG2.onErrorRewardedAdv -= onError;
            }
        }

        private static async UniTask<AdResult> WaitOutcome(UniTaskCompletionSource<AdResult> finished)
        {
            // Realtime: ожидание рекламы не должно зависеть от timeScale, который YG2 обнуляет
            // на своей паузе.
            var (isTimeout, result) = await finished.Task.TimeoutWithoutException(_showTimeout, DelayType.Realtime);

            return isTimeout ? AdResult.NotReady : result;
        }
    }
}
#endif
