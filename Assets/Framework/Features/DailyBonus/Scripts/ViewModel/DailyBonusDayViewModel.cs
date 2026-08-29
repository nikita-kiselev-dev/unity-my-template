using UnityEngine;

namespace Framework.Features.DailyBonus.ViewModel
{
    public class DailyBonusDayViewModel : Framework.Foundation.UI.Mvvm.ViewModel
    {
        public string PrefabKey { get; }
        public Transform Parent { get; }
        public string DayText { get; }
        public string ItemCountText { get; }
        public Sprite ItemSprite { get; }

        public DailyBonusDayViewModel(
            string prefabKey,
            Transform parent,
            string dayText,
            string itemCountText,
            Sprite itemSprite)
        {
            PrefabKey = prefabKey;
            Parent = parent;
            DayText = dayText;
            ItemCountText = itemCountText;
            ItemSprite = itemSprite;
        }
    }
}
