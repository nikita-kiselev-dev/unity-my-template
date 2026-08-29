using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.UI.Effects;
using Framework.Foundation.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Foundation.UI.LoadingCurtain.View
{
    public class LoadingCurtainView : LoadingCurtainViewBase
    {
        [SerializeField] private CanvasGroup m_CanvasGroup;
        [SerializeField] private TMP_Text m_LoadingText;
        [SerializeField] private GradientColor m_GradientColor;
        [SerializeField] private Slider m_Slider;

        private readonly string[] _states = { "", ".", "..", "..." };
        private string _loadingLocalizedString;
        private int _dotCount;

        private CancellationTokenSource _cts;

        public CanvasGroup CanvasGroup => m_CanvasGroup;
        public GradientColor GradientColor => m_GradientColor;

        public void SetLoadingSliderTotalValue(int count)
        {
            m_Slider.maxValue = count;
        }

        public void SetLoadingSliderCurrentValue(int count)
        {
            m_Slider.value = count;
        }

        public override void SetLoadingText(string text)
        {
            _loadingLocalizedString = text;
        }

        private void OnEnable()
        {
            var loadingTextLoaded = !_loadingLocalizedString.IsNullOrEmpty();
            
            if (!loadingTextLoaded)
            {
                return;
            }
            
            _cts = new CancellationTokenSource();
            RunTextAnimation(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            ResetLoadingSlider();
        }

        private async UniTaskVoid RunTextAnimation(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                m_LoadingText.text = _loadingLocalizedString + _states[_dotCount];
                _dotCount = (_dotCount + 1) % 4;

                await UniTask.WaitForSeconds(
                    LoadingCurtainConstants.Parameters.AddDotAnimationDelay,
                    cancellationToken: token);
            }
        }

        private void ResetLoadingSlider()
        {
            m_Slider.maxValue = 0;
            m_Slider.value = 0;
        }
    }
}