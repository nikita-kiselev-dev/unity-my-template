using System.Collections.Generic;

namespace Framework.Foundation.LiveOps.Offline
{
    public class EmptyRemoteConfigSource : IRemoteConfigSource
    {
        public IReadOnlyDictionary<string, string> GetValues()
        {
            return new Dictionary<string, string>();
        }
    }
}
