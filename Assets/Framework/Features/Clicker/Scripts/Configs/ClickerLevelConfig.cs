using System;
using System.Numerics;
using Newtonsoft.Json;

namespace Framework.Features.Clicker.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class ClickerLevelConfig
    {
        [JsonProperty("income_per_click")] private long _incomePerClick;
        [JsonProperty("upgrade_cost")] private long _upgradeCost;
        [JsonProperty("tier")] private ClickerTier _tier;

        public BigInteger IncomePerClick => _incomePerClick;
        public BigInteger UpgradeCost => _upgradeCost;
        public ClickerTier Tier => _tier;
    }
}