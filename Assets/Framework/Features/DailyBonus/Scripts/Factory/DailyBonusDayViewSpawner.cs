using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Features.DailyBonus.View;
using Framework.Features.DailyBonus.ViewModel;
using Framework.Foundation.Asset;

namespace Framework.Features.DailyBonus.Factory
{
    public class DailyBonusDayViewSpawner
    {
        private readonly IAssetScope _assets;

        public DailyBonusDayViewSpawner(IAssetScope assets)
        {
            _assets = assets;
        }

        public async UniTask CreateDayViews(IReadOnlyList<DailyBonusDayViewModel> days)
        {
            foreach (var day in days)
            {
                var view = await _assets.InstantiateAsync<DailyBonusDayView>(day.PrefabKey, day.Parent, setActive: true);
                view.Bind(day);
            }
        }
    }
}
