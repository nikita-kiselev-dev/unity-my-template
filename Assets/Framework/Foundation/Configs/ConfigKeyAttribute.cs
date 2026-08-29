using System;

namespace Framework.Foundation.Configs
{
    /// Помечает конкретный IConfig ключом его конфига: тип регистрируется в контейнере
    /// и грузится ConfigProvider-ом до создания потребителей.
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigKeyAttribute : Attribute
    {
        public string ConfigKey { get; }

        public ConfigKeyAttribute(string configKey)
        {
            ConfigKey = configKey;
        }
    }
}
