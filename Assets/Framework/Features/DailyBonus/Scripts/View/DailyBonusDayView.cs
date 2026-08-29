using Framework.Features.DailyBonus.ViewModel;
using Framework.Foundation.UI.Mvvm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Features.DailyBonus.View
{
    public class DailyBonusDayView : BindableView<DailyBonusDayViewModel>
    {
        [SerializeField] private TextMeshProUGUI m_DayText;

        [SerializeField] private Image m_ItemIcon;
        [SerializeField] private TextMeshProUGUI m_ItemCount;

        protected override void OnBind(DailyBonusDayViewModel viewModel)
        {
            m_DayText.text = viewModel.DayText;
            m_ItemIcon.sprite = viewModel.ItemSprite;
            m_ItemCount.text = viewModel.ItemCountText;
        }
    }
}
