using System.Collections.Generic;
using Framework.Features.Clicker;

namespace Framework.Features.Tests.Fakes
{
    public class FakeClickerAnalytics : IClickerAnalytics
    {
        public List<int> LoggedUpgradeLevels { get; } = new();

        public void LogUpgrade(int level) => LoggedUpgradeLevels.Add(level);
    }
}
