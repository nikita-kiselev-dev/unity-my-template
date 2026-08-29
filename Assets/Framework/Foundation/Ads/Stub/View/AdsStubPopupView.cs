using Framework.Foundation.Ads.Stub.ViewModel;
using Framework.Foundation.UI.Mvvm;
using Framework.Foundation.UI.Views;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Foundation.Ads.Stub.View
{
    public sealed class AdsStubPopupView : MonoView<AdsStubPopupViewModel>
    {
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private Button m_SuccessButton;
        [SerializeField] private Button m_FailButton;

        protected override void OnBind(AdsStubPopupViewModel viewModel)
        {
            viewModel.Title.Subscribe(title => m_TitleText.text = title).AddTo(this);
            viewModel.IsFailAvailable.Subscribe(m_FailButton.gameObject.SetActive).AddTo(this);

            m_SuccessButton.OnClickAsObservable().Subscribe(viewModel.Success.Execute).AddTo(this);
            m_FailButton.OnClickAsObservable().Subscribe(viewModel.Fail.Execute).AddTo(this);
        }
    }
}
