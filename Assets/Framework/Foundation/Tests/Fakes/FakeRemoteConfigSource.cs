using System.Collections.Generic;
using Framework.Foundation.LiveOps;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeRemoteConfigSource : IRemoteConfigSource
    {
        public Dictionary<string, string> Values = new();
        public int GetValuesCount { get; private set; }

        public IReadOnlyDictionary<string, string> GetValues()
        {
            GetValuesCount++;
            return Values;
        }
    }
}
