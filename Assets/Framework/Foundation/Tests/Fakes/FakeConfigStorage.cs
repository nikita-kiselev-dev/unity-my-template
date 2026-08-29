using Framework.Foundation.Configs;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeConfigStorage : IConfigStorage
    {
        public string LoadedJson { get; set; }
        public string SavedJson { get; private set; }
        public int QuarantineCount { get; private set; }

        public string Description => nameof(FakeConfigStorage);

        public string Load() => LoadedJson;

        public void Save(string json) => SavedJson = json;

        public void Quarantine() => QuarantineCount++;
    }
}
