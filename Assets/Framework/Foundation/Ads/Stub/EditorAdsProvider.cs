#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Ads.Stub
{
    /// Провайдер редактора: вместо SDK — попап с кнопками Success/Fail.
    public sealed class EditorAdsProvider : IAdsProvider
    {
        private IAdsStubHost _host;

        public bool IsInited { get; private set; }

        public UniTask InitAsync(CancellationToken ct)
        {
            IsInited = true;
            return UniTask.CompletedTask;
        }

        public bool IsReady(AdFormat format) => _host != null;

        public void SetBannerVisible(bool visible)
        {
        }

        public void SetHost(IAdsStubHost host) => _host = host;

        // Сцена меняется, хост пересоздаётся: снимать ссылку имеет право только её владелец,
        // иначе умирающий scope затёр бы хост уже загрузившейся сцены.
        public void ClearHost(IAdsStubHost host)
        {
            if (ReferenceEquals(_host, host))
            {
                _host = null;
            }
        }

        public UniTask<AdResult> ShowAsync(AdFormat format, CancellationToken ct)
        {
            return _host == null ? UniTask.FromResult(AdResult.NotReady) : _host.ShowAsync(format, ct);
        }
    }
}
#endif
