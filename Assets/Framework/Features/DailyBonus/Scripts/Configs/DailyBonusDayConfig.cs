using System;
using Newtonsoft.Json;

namespace Framework.Features.DailyBonus.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class DailyBonusDayConfig
    {
        [JsonProperty("streak_day")] private int _streakDay;
        [JsonProperty("item_name")] private string _itemName;
        [JsonProperty("item_sprite")] private string _itemSprite;
        [JsonProperty("item_count")] private int _itemCount;

        public int StreakDay => _streakDay;
        public string ItemName => _itemName;
        public string ItemSprite => _itemSprite;
        public int ItemCount => _itemCount;
    }
}