using System.Collections.Generic;
using Framework.Foundation.Configs;

namespace Framework.Foundation.Initialization
{
    public sealed class AutoTypeScanResult
    {
        public IReadOnlyList<AutoTypeEntry> AutoTypes { get; }
        public IReadOnlyList<ConfigTypeEntry> Configs { get; }

        public AutoTypeScanResult(AutoTypeEntry[] autoTypes, ConfigTypeEntry[] configs)
        {
            AutoTypes = autoTypes;
            Configs = configs;
        }
    }
}
