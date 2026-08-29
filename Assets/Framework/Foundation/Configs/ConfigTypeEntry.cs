using System;

namespace Framework.Foundation.Configs
{
    public readonly struct ConfigTypeEntry
    {
        public readonly Type ConfigType;
        public readonly string ConfigKey;

        public ConfigTypeEntry(Type configType, string configKey)
        {
            ConfigType = configType;
            ConfigKey = configKey;
        }
    }
}
