using Framework.Features.Clicker.ViewModel;
using Framework.Features.Items.View;
using Framework.Foundation.UI.Mvvm;
using Framework.Foundation.UI.Views;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Features.Clicker.View
{
    [CurrencyViewHost]
    public class ClickerWindowView : MonoView<ClickerViewModel>
    {
        [SerializeField] private Button m_UpgradeButton;
        [SerializeField] private ClickAreaView m_ClickArea;

        protected override void OnBind(ClickerViewModel viewModel)
        {
            m_ClickArea.Clicked.Subscribe(viewModel.Click.Execute).AddTo(this);
            m_UpgradeButton.OnClickAsObservable().Subscribe(viewModel.Upgrade.Execute).AddTo(this);
            viewModel.CanUpgrade.SubscribeToInteractable(m_UpgradeButton).AddTo(this);
        }
    }
}
