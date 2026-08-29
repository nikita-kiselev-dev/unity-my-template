using Framework.Features.MainMenu.ViewModel;
using Framework.Foundation.UI.Mvvm;
using Framework.Foundation.UI.Views;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Features.MainMenu.View
{
    public sealed class MainMenuWindowView : MonoView<MainMenuViewModel>
    {
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private Button m_WebSiteButton;

        protected override void OnBind(MainMenuViewModel viewModel)
        {
            m_PlayButton.OnClickAsObservable().Subscribe(viewModel.Play.Execute).AddTo(this);
            m_SettingsButton.OnClickAsObservable().Subscribe(viewModel.OpenSettings.Execute).AddTo(this);
            m_WebSiteButton.OnClickAsObservable().Subscribe(viewModel.OpenWebSite.Execute).AddTo(this);
        }
    }
}
