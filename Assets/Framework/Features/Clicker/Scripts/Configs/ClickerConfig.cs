using System;
using System.Collections.Generic;
using Framework.Foundation.Configs;
using Newtonsoft.Json;

namespace Framework.Features.Clicker.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigKey(ClickerConstants.Configs.Key)]
    public class ClickerConfig : IClickerConfig
    {
        [JsonProperty("is_enabled")] private bool _isEnabled;
        [JsonProperty("clicker_levels")] private ClickerLevelConfig[] _levels;
        
        public bool IsEnabled => _isEnabled;
        public IReadOnlyList<ClickerLevelConfig> Levels => _levels;
    }
}