using System;
using System.Collections.Generic;
using Framework.Foundation.Configs;
using Newtonsoft.Json;

namespace Framework.Features.DailyBonus.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigKey(DailyBonusConstants.Configs.Key)]
    public class DailyBonusConfig : IDailyBonusConfig
    {
        [JsonProperty("is_enabled")] private bool _isEnabled;
        [JsonProperty("streak_days")] private DailyBonusDayConfig[] _days;

        public bool IsEnabled => _isEnabled;
        public IReadOnlyList<DailyBonusDayConfig> Days => _days;
    }
}
