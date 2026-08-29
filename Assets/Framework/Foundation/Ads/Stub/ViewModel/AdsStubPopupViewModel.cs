using Cysharp.Threading.Tasks;
using R3;

namespace Framework.Foundation.Ads.Stub.ViewModel
{
    /// <summary>
    /// VM попапа-заглушки: одна ad-сессия — один <see cref="Prepare"/> и ровно один исход.
    /// Живёт вне <c>#if UNITY_EDITOR</c>, потому что от неё зависит <c>AdsStubPopupView</c>:
    /// компонент префаба нельзя вырезать из билда, иначе prefab потеряет скрипт.
    /// </summary>
    public sealed class AdsStubPopupViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        private readonly ReactiveProperty<string> _title = new(string.Empty);
        private readonly ReactiveProperty<bool> _isFailAvailable = new();

        private UniTaskCompletionSource<AdResult> _completion;

        public ReadOnlyReactiveProperty<string> Title => _title;
        public ReadOnlyReactiveProperty<bool> IsFailAvailable => _isFailAvailable;

        public ReactiveCommand Success { get; } = new();
        public ReactiveCommand Fail { get; } = new();

        public AdsStubPopupViewModel()
        {
            Success.AddTo(ref Subscriptions);
            Fail.AddTo(ref Subscriptions);

            Success.Subscribe(_ => Complete(AdResult.Success)).AddTo(ref Subscriptions);
            Fail.Subscribe(_ => Complete(AdResult.Failed)).AddTo(ref Subscriptions);
        }

        public UniTask<AdResult> Prepare(AdFormat format)
        {
            _completion = new UniTaskCompletionSource<AdResult>();
            _title.Value = format.ToString();

            // Отказ показа игрок может выбрать только у rewarded: в остальных форматах
            // «не досмотрел» выражается закрытием попапа.
            _isFailAvailable.Value = format == AdFormat.Rewarded;

            return _completion.Task;
        }

        public void Complete(AdResult result)
        {
            _completion?.TrySetResult(result);
        }

        public override void Dispose()
        {
            _title.Dispose();
            _isFailAvailable.Dispose();
            base.Dispose();
        }
    }
}
