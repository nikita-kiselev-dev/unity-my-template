using System;
using System.Collections.Generic;
using Framework.Foundation.Configs;
using Newtonsoft.Json;

namespace Framework.Features.Items.Configs
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigKey(ItemsConstants.Configs.Currencies)]
    public class CurrenciesConfig : IConfig
    {
        [JsonProperty("is_enabled")] private bool _isEnabled;
        [JsonProperty("currencies")] private string[] _currencies;
        
        public bool IsEnabled => _isEnabled;
        public IReadOnlyList<string> Currencies => _currencies;
    }
}