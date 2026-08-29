using System.Collections.Generic;
using Framework.Foundation.Configs;

namespace Framework.Features.Clicker.Configs
{
    public interface IClickerConfig : IConfig
    {
        IReadOnlyList<ClickerLevelConfig> Levels { get; }
    }
}