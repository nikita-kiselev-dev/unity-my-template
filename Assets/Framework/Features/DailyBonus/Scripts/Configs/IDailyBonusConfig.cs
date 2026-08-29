using System.Collections.Generic;
using Framework.Foundation.Configs;

namespace Framework.Features.DailyBonus.Configs
{
    public interface IDailyBonusConfig : IConfig
    {
        public IReadOnlyList<DailyBonusDayConfig> Days { get; }
    }
}
