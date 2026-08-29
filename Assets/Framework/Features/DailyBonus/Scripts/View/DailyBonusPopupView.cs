using Framework.Features.DailyBonus.ViewModel;
using Framework.Features.UI;
using Framework.Foundation.UI.Mvvm;
using Framework.Foundation.UI.Views;
using Framework.Foundation.Utilities;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Features.DailyBonus.View
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DailyBonusPopupView : MonoView<DailyBonusViewModel>
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private RewardRowLayout m_RewardRowLayout;

        public IRewardRowLayout RewardRowLayout => m_RewardRowLayout;

        protected override void OnBind(DailyBonusViewModel viewModel)
        {
            m_CloseButton.OnClickAsObservable().Subscribe(_ => Close()).AddTo(this);
        }
    }
}
