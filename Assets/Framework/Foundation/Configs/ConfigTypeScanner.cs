using System;
using System.Collections.Generic;
using System.Reflection;

namespace Framework.Foundation.Configs
{
    public static class ConfigTypeScanner
    {
        private static readonly Type _configType = typeof(IConfig);

        public static ConfigTypeEntry[] Scan(IEnumerable<Type> types)
        {
            var result = new List<ConfigTypeEntry>();

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<ConfigKeyAttribute>(inherit: false);

                if (attribute == null)
                {
                    continue;
                }

                if (!_configType.IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"[{nameof(ConfigKeyAttribute)}] is applied to {type.Name}, which does not implement {nameof(IConfig)}.");
                }

                if (type.IsAbstract)
                {
                    continue;
                }

                result.Add(new ConfigTypeEntry(type, attribute.ConfigKey));
            }

            return result.ToArray();
        }
    }
}
