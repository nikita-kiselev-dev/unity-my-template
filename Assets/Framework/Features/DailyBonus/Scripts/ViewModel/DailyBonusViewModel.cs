using System.Collections.Generic;
using R3;

namespace Framework.Features.DailyBonus.ViewModel
{
    public class DailyBonusViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        public IReadOnlyList<DailyBonusDayViewModel> Days { get; }

        public DailyBonusViewModel(IReadOnlyList<DailyBonusDayViewModel> days)
        {
            Days = days;

            foreach (var day in days)
            {
                day.AddTo(ref Subscriptions);
            }
        }
    }
}
