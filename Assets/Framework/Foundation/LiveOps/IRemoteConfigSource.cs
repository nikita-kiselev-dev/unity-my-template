using System.Collections.Generic;

namespace Framework.Foundation.LiveOps
{
    public interface IRemoteConfigSource
    {
        public IReadOnlyDictionary<string, string> GetValues();
    }
}